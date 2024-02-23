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

        [Header("UI")]
        public TextMesh textName;
        public TextMesh textHealth;
        public TextMesh textAmmo;

        [Header("Stats")]
        [SyncVar(hook = nameof(OnNameChanged))] public string playerName = "";
        [SyncVar(hook = nameof(OnHealthChanged))] public int playerHealth = 10;
        [SyncVar(hook = nameof(OnAmmoChanged))] public int playerAmmo = 20;

        void OnNameChanged(string _old, string _new)
        {
            textName.text = playerName;
        }

        void OnHealthChanged(int _old, int _new)
        {
            if (playerHealth >= 0)
            {
                textHealth.text = new string('-', playerHealth);
            }
        }
        void OnAmmoChanged(int _old, int _new)
        {
            if (playerAmmo >= 0)
            {
                textAmmo.text = new string('-', playerAmmo);
            }
        }

        [Command]
        public void CmdSetupPlayer(string _playerName)
        {
            //print("Player joined, setup: " + _playerName);
            ApplyName(_playerName);
        }

        void ApplyName(string _playerName)
        {
            // here you can censor restricted words or add name length check incase player locally bypassed them
            // set a default if blank.
            if (_playerName == "")
            {
                playerName = "Player: " + netId;
            }
            else
            {
                playerName = _playerName;
            }
        }

        void Start()
        {
            if (isOwned)
            {
                // currently blank until we load it from ui or stored data
                CmdSetupPlayer("");
            }
            else
            {
                // we want other players to see this view, technically should be default
                playerWeapon.SetThirdPerson();
            }
        }
    }
}
