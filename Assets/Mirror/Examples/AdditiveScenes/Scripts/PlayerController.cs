using UnityEngine;

namespace Mirror.Examples.Additive
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        public CharacterController characterController;

        void OnValidate()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            Camera.main.transform.SetParent(transform);
            Camera.main.transform.localPosition = new Vector3(0f, 3f, -8f);
            Camera.main.transform.localEulerAngles = new Vector3(10f, 0f, 0f);
        }

        void OnDisable()
        {
            if (isLocalPlayer)
            {
                Camera.main.transform.SetParent(null);
                Camera.main.transform.localPosition = new Vector3(0f, 50f, 0f);
                Camera.main.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            }
        }

        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float turnSpeedAccel = 5f;
        public float turnSpeedDecel = 5f;
        public float maxTurnSpeed = 150f;

        [Header("Jump Settings")]
        public float jumpSpeed = 0f;
        public float jumpFactor = .025F;

        [Header("Diagnostics")]
        public float horizontal = 0f;
        public float vertical = 0f;
        public float turn = 0f;
        public bool isGrounded = true;
        public bool isFalling = false;

        void Update()
        {
            if (!isLocalPlayer) return;

            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");

            if (Input.GetKey(KeyCode.Q) && (turn > -maxTurnSpeed))
                turn -= turnSpeedAccel;
            else if (Input.GetKey(KeyCode.E) && (turn < maxTurnSpeed))
                turn += turnSpeedAccel;
            else if (turn > turnSpeedDecel)
                turn -= turnSpeedDecel;
            else if (turn < -turnSpeedDecel)
                turn += turnSpeedDecel;
            else
                turn = 0f;

            if (!isFalling && Input.GetKey(KeyCode.Space) && (isGrounded || jumpSpeed < 1))
                jumpSpeed += jumpFactor;
            else if (isGrounded)
                isFalling = false;
            else
            {
                isFalling = true;
                jumpSpeed = 0;
            }
        }

        void FixedUpdate()
        {
            if (!isLocalPlayer || characterController == null) return;

            transform.Rotate(0f, turn * Time.fixedDeltaTime, 0f);

            Vector3 direction = new Vector3(horizontal, jumpSpeed, vertical);
            direction = Vector3.ClampMagnitude(direction, 1f);
            direction = transform.TransformDirection(direction);
            direction *= moveSpeed;

            if (jumpSpeed > 0)
                characterController.Move(direction * Time.fixedDeltaTime);
            else
                characterController.SimpleMove(direction);

            isGrounded = characterController.isGrounded;
        }
    }
}
