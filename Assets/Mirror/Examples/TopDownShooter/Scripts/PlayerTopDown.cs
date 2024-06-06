using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerTopDown : NetworkBehaviour
{
    public readonly static List<PlayerTopDown> playerList = new List<PlayerTopDown>();

    private Camera mainCamera;
    private CameraTopDown cameraTopDown;
    private CanvasTopDown canvasTopDown;

    public float moveSpeed = 5f;
    public CharacterController characterController;
    public GameObject leftFoot, rightFoot;
    public Vector3 previousPosition;
    public Quaternion previousRotation;

    [SyncVar(hook = nameof(OnFlashLightChanged))]
    public bool flashLightStatus = true;
    public Light flashLight;

    [SyncVar(hook = nameof(OnKillsChanged))]
    public int kills = 0;

    public GameObject muzzleFlash;
    public float shootDistance = 100f;  // Maximum distance for the raycast
    public LayerMask hitLayers;  // Layers that can be hit by the raycast

    public override void OnStartLocalPlayer()
    {
        // grab and setup camera for local player only
        mainCamera = Camera.main;
        cameraTopDown = mainCamera.GetComponent<CameraTopDown>();
        cameraTopDown.playerTransform = this.transform;
        cameraTopDown.offset.y = 20.0f; // dramatic zoom out once players setup
    }

    void Awake()
    {
        //allow all players to run this, they may need it for reference
        canvasTopDown = GameObject.FindObjectOfType<CanvasTopDown>();
    }

    public void Start()
    {
        playerList.Add(this);
        print("Player joined, total players: " + playerList.Count);

        InvokeRepeating("AnimatePlayer",0.3f,0.3f);
    }

    public void OnDestroy()
    {
        playerList.Remove(this);
        print("Player removed, total players: " + playerList.Count);
    }

    [ClientCallback]
    void Update()
    {
        if (!Application.isFocused) return;
        if (isOwned == false) { return; }

        // Handle movement
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical);
        if (movement.magnitude > 1f) movement.Normalize();  // Normalize to prevent faster diagonal movement
        characterController.Move(movement * moveSpeed * Time.deltaTime);

        RotatePlayerToMouse();

        if (Input.GetKeyDown(KeyCode.F))
        {
            // we could optionally call this locally too, to avoid minor delay in the command->sync var hook result
            CmdFlashLight();
        }

        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    [ClientCallback]
    void RotatePlayerToMouse()
    {
        // Plane for raycast intersection
        Plane playerPlane = new Plane(Vector3.up, transform.position);

        // Ray from camera to mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (playerPlane.Raycast(ray, out float hitDist))
        {
            Vector3 targetPoint = ray.GetPoint(hitDist);
            Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, moveSpeed * Time.deltaTime);
        }
    }

    void Shoot()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, shootDistance, hitLayers))
        {
            Debug.Log("Hit: " + hit.collider.gameObject.name);

            canvasTopDown.shotMarker.transform.position = hit.point;

            // you should check for a tag, not name contains
            // this is a quick workaround to make sure the example works without custom tags that may not be in your project
            if (hit.collider.gameObject.name.Contains("Enemy"))
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
            Debug.Log("Missed");
        }
    }

    IEnumerator MuzzleFlashEffect()
    {
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

    [Command]
    public void CmdFlashLight()
    {
        if (flashLightStatus == true)
        {
            flashLightStatus = false;
        }
        else
        {
            flashLightStatus = true;
        }
    }

    // our sync var hook, which sets flashlight status to the same on all clients for this player
    void OnFlashLightChanged(bool _Old, bool _New)
    {
        flashLight.enabled = _New;
    }

    [Command]
    public void CmdShoot(GameObject target)
    {
        RpcShoot();
        if (target)
        {
            target.GetComponent<EnemyTopDown>().Kill();
            kills += 1; // update user kills
        }
    }

    [ClientRpc]
    void RpcShoot()
    {
        StartCoroutine(MuzzleFlashEffect());
    }

    // hook for sync var kills
    void OnKillsChanged(int _Old, int _New)
    {
        // all players get your latest kill data, however only local player updates their UI
        if (isLocalPlayer)
        {
            canvasTopDown.UpdateKillsUI(kills);
        }
    }

    void AnimatePlayer()
    {
        if (this.transform.position == previousPosition && Quaternion.Angle(this.transform.rotation, previousRotation) < 20.0f)
        {
            rightFoot.SetActive(false);
            leftFoot.SetActive(false);
        }
        else
        {
            if (leftFoot.activeInHierarchy)
            {
                rightFoot.SetActive(true);
                leftFoot.SetActive(false);
            }
            else
            {
                rightFoot.SetActive(false);
                leftFoot.SetActive(true);
            }
            previousPosition = this.transform.position;
            previousRotation = this.transform.rotation;
        }
    }
}
