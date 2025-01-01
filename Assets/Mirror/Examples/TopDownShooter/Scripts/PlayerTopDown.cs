using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.TopDownShooter
{
    public class PlayerTopDown : NetworkBehaviour
    {
        public readonly static List<PlayerTopDown> playerList = new List<PlayerTopDown>();

        private Camera mainCamera;
        private CameraTopDown cameraTopDown;
        private CanvasTopDown canvasTopDown;

        public float moveSpeed = 5f;
        public CharacterController characterController;

        public GameObject leftFoot, rightFoot;
        private Vector3 previousPosition;
        private Quaternion previousRotation;

        [SyncVar(hook = nameof(OnFlashLightChanged))]
        public bool flashLightStatus = true;
        public Light flashLight;

        [SyncVar(hook = nameof(OnKillsChanged))]
        public int kills = 0;

        [SyncVar(hook = nameof(OnPlayerStatusChanged))]
        public int playerStatus = 0;
        public GameObject[] objectsToHideOnDeath;

        public float shootDistance = 100f;
        public LayerMask hitLayers;
        public GameObject muzzleFlash;
        public AudioSource soundGunShot, soundDeath, soundFlashLight, soundLeftFoot, soundRightFoot;

#if !UNITY_SERVER
        public override void OnStartLocalPlayer()
        {
            // Grab and setup camera for local player only
            mainCamera = Camera.main;
            cameraTopDown = mainCamera.GetComponent<CameraTopDown>();
            cameraTopDown.playerTransform = this.transform;
            cameraTopDown.offset.y = 20.0f; // dramatic zoom out once players setup
            canvasTopDown.playerTopDown = this;

            // We want 3D audio effects to be around the player, not the camera 50 meters in the air
            // Otherwise it looks weird in-game, trust me
            mainCamera.GetComponent<AudioListener>().enabled = false;
            this.gameObject.AddComponent<AudioListener>();
        }
#endif

        void Awake()
        {
            // Allow all players to run this, they may need it for reference
#if UNITY_2022_2_OR_NEWER
            canvasTopDown = GameObject.FindAnyObjectByType<CanvasTopDown>();
#else
            canvasTopDown = GameObject.FindObjectOfType<CanvasTopDown>();
#endif
        }

        public void Start()
        {
            // If only server needs access to a player list, place the Add and Remove in public override void OnStartServer/OnStopServer
            playerList.Add(this);
            print("Player joined, total players: " + playerList.Count);

#if !UNITY_SERVER
            if (isClient)
            {
                InvokeRepeating("AnimatePlayer", 0.2f, 0.2f);
            }
#endif
        }

        public void OnDestroy()
        {
            playerList.Remove(this);
            print("Player removed, total players: " + playerList.Count);

            if (mainCamera) { mainCamera.GetComponent<AudioListener>().enabled = true; }
        }

#if !UNITY_SERVER
        [ClientCallback]
        void Update()
        {
            if (!Application.isFocused) return;
            if (isOwned == false) { return; }
            if (playerStatus != 0) { return; } // make sure we are alive

            // Handle movement
            float moveHorizontal = Input.GetAxis("Horizontal");
            float moveVertical = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical);
            if (movement.magnitude > 1f) movement.Normalize();  // Normalize to prevent faster diagonal movement
            characterController.Move(movement * moveSpeed * Time.deltaTime);

            RotatePlayerToMouse();

            if (Input.GetKeyUp(KeyCode.F))
            {
                // We could optionally call this locally too, to avoid minor delay in the command->sync var hook result
                CmdFlashLight();
            }

            // We currently have no shoot limiter, ideally thats a feature you would need to add.
            if (Input.GetMouseButtonDown(0))
            {
                Shoot();
            }
        }
#endif

#if !UNITY_SERVER
        [ClientCallback]
        void RotatePlayerToMouse()
        {
            Plane playerPlane = new Plane(Vector3.up, transform.position);
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (playerPlane.Raycast(ray, out float hitDist))
            {
                Vector3 targetPoint = ray.GetPoint(hitDist);
                Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, moveSpeed * Time.deltaTime);
            }
        }
#endif

#if !UNITY_SERVER
        [ClientCallback]
        void Shoot()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, shootDistance, hitLayers))
            {
                //print("Hit: " + hit.collider.gameObject.name);

                canvasTopDown.shotMarker.transform.position = hit.point;

                if (hit.collider.gameObject.GetComponent<NetworkIdentity>() != null)
                {
                    CmdShoot(hit.collider.gameObject);
                }
                else
                {
                    CmdShoot(null);
                }
            }
            else
            {
                //print("Missed");
            }
        }
#endif

#if !UNITY_SERVER
        IEnumerator GunShotEffect()
        {
            soundGunShot.Play();
            muzzleFlash.SetActive(true);
            if (isLocalPlayer)
            {
                canvasTopDown.shotMarker.SetActive(true);
            }
            yield return new WaitForSeconds(0.1f);
            muzzleFlash.SetActive(false);
            if (isLocalPlayer)
            {
                canvasTopDown.shotMarker.SetActive(false);
            }
        }
#endif

        [Command]
        public void CmdFlashLight()
        {
            flashLightStatus = !flashLightStatus;
        }

        // our sync var hook, which sets flashlight status to the same on all clients for this player
        void OnFlashLightChanged(bool _Old, bool _New)
        {
#if !UNITY_SERVER
            Debug.Log($"OnFlashLightChanged: {_New}");
            flashLight.enabled = _New;
            soundFlashLight.Play();
#endif
        }

        [Command]
        public void CmdShoot(GameObject target)
        {
            RpcShoot();
            if (target)
            {
                // you should check for a tag, not name contains
                // this is a quick workaround to make sure the example works without custom tags that may not be in your project
                if (target.name.Contains("Enemy"))
                {
                    target.GetComponent<EnemyTopDown>().Kill();
                }
                else if (CompareTag("Player") == true) // Player tag exists in unity by default, so we should be good to use it here
                {
                    // Make sure they are alive/dont shoot themself
                    if (target.GetComponent<PlayerTopDown>().playerStatus != 0 || target == this.gameObject) { return; }
                    target.GetComponent<PlayerTopDown>().Kill();
                }
                kills += 1; // update user kills sync var
            }
        }

        [ClientRpc]
        void RpcShoot()
        {
#if !UNITY_SERVER
            StartCoroutine(GunShotEffect());
#endif
        }

        // hook for sync var kills
        void OnKillsChanged(int _Old, int _New)
        {
#if !UNITY_SERVER
            // all players get your latest kill data, however only local player updates their UI
            if (isLocalPlayer)
            {
                canvasTopDown.UpdateKillsUI(kills);
            }
#endif
        }

        [ClientCallback]
        void AnimatePlayer()
        {
#if !UNITY_SERVER
            // A simple way to change sprite animation, without networking it
            // If not moving or rotating, show no feet animation or sound, if moving, flick through footstep animations and sound effects.
            if (this.transform.position == previousPosition && Quaternion.Angle(this.transform.rotation, previousRotation) < 20.0f)
            {
                rightFoot.SetActive(false);
                leftFoot.SetActive(false);
            }
            else
            {
                if (rightFoot.activeInHierarchy)
                {
                    leftFoot.SetActive(true);
                    rightFoot.SetActive(false);
                    soundLeftFoot.Play();
                }
                else
                {
                    leftFoot.SetActive(false);
                    rightFoot.SetActive(true);
                    soundRightFoot.Play();
                }
                previousPosition = this.transform.position;
                previousRotation = this.transform.rotation;
            }
#endif
        }

        [Command]
        public void CmdRespawnPlayer()
        {
            // We use a number playerStatus here, rather than bool, as you can use it for other things such as delayed respawn, respawn armour, spectating etc
            if (playerStatus == 0)
            {
                playerStatus = 1;
            }
            else
            {
                playerStatus = 0;
            }
        }

        // Our sync var hook for death and alive
        void OnPlayerStatusChanged(int _Old, int _New)
        {
#if !UNITY_SERVER
            if (playerStatus == 0) // default/show
            {
                foreach (var obj in objectsToHideOnDeath)
                {
                    obj.SetActive(true);
                }
                characterController.enabled = true;
                if (isLocalPlayer)
                {
                    this.transform.position = NetworkManager.startPositions[Random.Range(0, NetworkManager.startPositions.Count)].position;
                    canvasTopDown.buttonRespawnPlayer.gameObject.SetActive(false);
                }
            }
            else if (playerStatus == 1) // death
            {
                // have meshes hidden, disable movement and show respawn button
                foreach (var obj in objectsToHideOnDeath)
                {
                    obj.SetActive(false);
                }
                characterController.enabled = false;
                if (isLocalPlayer)
                {
                    canvasTopDown.buttonRespawnPlayer.gameObject.SetActive(true);
                }
            }
            // else if (playerStatus == 2) // can be used for other features, such as spectator, make local camera follow another player 
#endif
        }

        [ServerCallback]
        public void Kill()
        {
            //print("Kill Player");
            playerStatus = 1;
            RpcKill();
        }

        [ClientRpc]
        void RpcKill()
        {
#if !UNITY_SERVER
            soundDeath.Play();
            GameObject splatter = Instantiate(canvasTopDown.deathSplatter, this.transform.position, this.transform.rotation);
            Destroy(splatter, 5.0f);
#endif
        }
    }
}