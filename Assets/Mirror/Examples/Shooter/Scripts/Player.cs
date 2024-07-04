using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class Player : NetworkBehaviour
    {
        /*
        [Header("Camera")]
        public Transform cameraMount;
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;
        Camera cam;

        protected virtual void Awake()
        {
            // find main camera once
            cam = Camera.main;
        }

        public override void OnStartLocalPlayer()
        {
            // remember initial camera position/rotation
            initialCameraPosition = cam.transform.position;
            initialCameraRotation = cam.transform.rotation;

            // move main camera into camera mount
            cam.transform.SetParent(cameraMount, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
        }

        public override void OnStopLocalPlayer()
        {
            // move the camera back to the original point.
            // otherwise it would be destroyed when stopping the game (and player)
            cam.transform.SetParent(null, true);
            cam.transform.position = initialCameraPosition;
            cam.transform.rotation = initialCameraRotation;
        }*/

        [Header("Components")]
        public PlayerWeapon playerWeapon;
        public PlayerLook playerLook;
        public PlayerMovement playerMovement;
        public LagCompensator compensator;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public SceneScript sceneScript;
        private Material[] materials;
        public ShooterCharacterData characterData;
        public GameObject[] objectsToHideOnStatusChange;

        [Header("UI")]
        public TextMesh textName;
        public TextMesh textHealth;
        public TextMesh textAmmo;

        [Header("Stats")]
        [SyncVar(hook = nameof(OnNameChanged))] public string playerName = "";
        [SyncVar(hook = nameof(OnHealthChanged))] public int playerHealth = 10;
        [SyncVar(hook = nameof(OnAmmoChanged))] public int playerAmmo = 20;
        [SyncVar(hook = nameof(HookSetColor))] public Color playerColour;
        [SyncVar(hook = nameof(OnCharacterChanged))] public int playerCharacter = 1;
        [SyncVar(hook = nameof(OnPlayerStatusChanged))] public int playerStatus = 0; //used for alive, ready, unready, death, spectator

        public int playerKills = 0;
        public int playerDeaths = 0;
        public Bot bot;
        public readonly static List<Player> playersList = new List<Player>();

        private Material cachedMaterial;

        public void HookSetColor(Color _old, Color _new)
        {
            //Debug.Log("HookSetColor");
            materials = skinnedMeshRenderer.materials;
            materials[1].color = playerColour;
        }

        public void OnNameChanged(string _old, string _new)
        {
            //print("_new: " + _new);
            textName.text = playerName;
        }

        public void OnHealthChanged(int _old, int _new)
        {
            if (playerHealth >= 0)
            {
                textHealth.text = new string('-', playerHealth);
            }
            UpdateLocalUI();
        }

        void OnAmmoChanged(int _old, int _new)
        {
            if (playerAmmo >= 0)
            {
                textAmmo.text = new string('-', playerAmmo);
            }
            UpdateLocalUI();
        }

        void OnCharacterChanged(int _old, int _new)
        {
            if (playerCharacter == 2)
            {
                playerMovement.walkSpeed = playerMovement.walkSpeed/2; //or -3?
                playerMovement.runSpeed = playerMovement.runSpeed/2;
                playerMovement.jumpSpeed = playerMovement.jumpSpeed - 1;

                playerMovement.animator.speed = playerMovement.animator.speed / 2;
            }
        }

        public void UpdateLocalUI()
        {
            if (isLocalPlayer && sceneScript)
            {
                sceneScript.textHealth.text = "HP: " + playerHealth;
                sceneScript.textAmmo.text = "Ammo: " + playerAmmo;
            }
        }

        void Awake()
        {
            // allow all players to grab this
            // we use the following code to allow this Player script Find to work in scenes SceneScript does not exist in (character selection)
            SceneScript[] sceneScripts = FindObjectsOfType<SceneScript>();
            if (sceneScripts.Length > 0)
            {
                sceneScript = sceneScripts[0];
                characterData = sceneScript.characterData;
            }
        }

        void Start()
        {
            if (isOwned)
            {
                // local player adjustments go here
                if (sceneScript) { sceneScript.localPlayer = this; }
                UpdateLocalUI();
            }
            else
            {
                // we want other players to set this
                playerWeapon.SetThirdPerson();
            }
            playersList.Add(this);
            playerWeapon.SetupNewWeapon();
        }   

        void OnDestroy()
        {
            playersList.Remove(this);
            if (cachedMaterial) { Destroy(cachedMaterial); }
        }

        // our status sync var and hook, allows us to have different states regarding the ready, unready, alive, death, spectator
        // such as play animation and disable controls, wait, then hide car objects and show respawn UI, respawn cooldown wait, allow respawn button
        void OnPlayerStatusChanged(int _old, int _new)
        {
            if (playerStatus == 0) // alive
            {
                foreach (var obj in objectsToHideOnStatusChange)
                {
                    obj.SetActive(true);
                }
                if (isOwned)
                {
                    // Uses NetworkStartPosition feature, optional.
                    this.transform.position = NetworkManager.startPositions[UnityEngine.Random.Range(0, NetworkManager.startPositions.Count)].position;
                    sceneScript.respawnUI.SetActive(false);
                }
                playerMovement.controllerCollider.enabled = true;
            }
            else if (playerStatus == 1) // death
            {
                // have meshes hidden, disable movement and show respawn button
                foreach (var obj in objectsToHideOnStatusChange)
                {
                    obj.SetActive(false);
                }
                playerMovement.controllerCollider.enabled = false;
                if (isOwned)
                {
                    sceneScript.respawnUI.SetActive(true);
                }
            }
        }

        [Command]
        public void CmdReqestPlayerStatusChange(int _status)
        {
            playerHealth = characterData.characterHealths[playerCharacter];
            playerStatus = _status;
        }
    }
}
