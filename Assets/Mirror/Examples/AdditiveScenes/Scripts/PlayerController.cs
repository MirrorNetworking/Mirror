using UnityEngine;

namespace Mirror.Examples.Additive
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        CharacterController characterController;

        public float moveSpeed = 300f;
        public float maxTurnSpeed = 90f;
        public float turnSpeedAccel = 30f;
        public float turnSpeedDecel = 30f;

        public override void OnStartServer()
        {
            base.OnStartServer();
            playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        [SyncVar(hook = nameof(SetColor))]
        public Color playerColor = Color.black;

        void SetColor(Color color)
        {
            GetComponent<Renderer>().material.color = color;
        }

        Camera mainCam;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            characterController = GetComponent<CharacterController>();

            // Grab a refernce to the main camera so we can enable it again in OnDisable
            mainCam = Camera.main;

            // Turn off the main camera because the Player prefab has its own camera
            mainCam.enabled = false;

            // Enable the local player's camera
            GetComponentInChildren<Camera>().enabled = true;
        }

        private void OnDisable()
        {
            if (isLocalPlayer)
            {
                // Disable the local player's camera
                GetComponentInChildren<Camera>().enabled = false;

                // Re-enable the main camera when Stop is pressed in the HUD
                if (mainCam != null) mainCam.enabled = true;
            }
        }

        float horizontal = 0f;
        float vertical = 0f;
        float turn = 0f;

        void Update()
        {
            if (!isLocalPlayer) return;

            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");

            if (Input.GetKey(KeyCode.Q) && (turn > -maxTurnSpeed))
                turn -= turnSpeedAccel;
            else if (Input.GetKey(KeyCode.E) && (turn < maxTurnSpeed))
                turn += turnSpeedAccel;
            else
            {
                if (turn > turnSpeedDecel)
                    turn -= turnSpeedDecel;
                else if (turn < -turnSpeedDecel)
                    turn += turnSpeedDecel;
                else
                    turn = 0f;
            }
        }

        void FixedUpdate()
        {
            if (!isLocalPlayer || characterController == null) return;

            transform.Rotate(0f, turn * Time.fixedDeltaTime, 0f);

            Vector3 direction = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f) * moveSpeed;
            direction = transform.TransformDirection(direction);
            characterController.SimpleMove(direction * Time.fixedDeltaTime);
        }
    }
}
