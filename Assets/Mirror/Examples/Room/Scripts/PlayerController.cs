using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [SyncVar]
        public int index;

        [SyncVar]
        public uint score;

        [SyncVar(hook = nameof(SetColor))]
        public Color playerColor = Color.black;

        // Unity clones the material when GetComponent<Renderer>().material is called
        // Cache it here and destroy it in OnDestroy to prevent a memory leak
        Material cachedMaterial;

        void SetColor(Color color)
        {
            if (cachedMaterial == null) cachedMaterial = GetComponent<Renderer>().material;
            cachedMaterial.color = color;
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

        void OnDestroy()
        {
            Destroy(cachedMaterial);
        }

        CharacterController characterController;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            characterController = GetComponent<CharacterController>();

            Camera.main.transform.SetParent(transform);
            Camera.main.transform.localPosition = new Vector3(0f, 3f, -8f);
            Camera.main.transform.localEulerAngles = new Vector3(10f, 0f, 0f);
        }

        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float turnSpeedAccel = 5f;
        public float turnSpeedDecel = 5f;
        public float maxTurnSpeed = 150f;

        [Header("Jump Settings")]
        public float jumpSpeed = 0f;
        public float maxJumpSpeed = 5F;
        public float jumpFactor = .05F;

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

            if (!isFalling && Input.GetKey(KeyCode.Space) && (isGrounded || jumpSpeed < maxJumpSpeed))
                jumpSpeed += maxJumpSpeed * jumpFactor;
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

        GameObject controllerColliderHitObject;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // If player and prize objects are on their own layer(s) with correct
            // collision matrix, we wouldn't have to validate the hit.gameobject.
            // Since this is just an example, project settings aren't included so we check the name.

            controllerColliderHitObject = hit.gameObject;

            if (isLocalPlayer && controllerColliderHitObject.name.StartsWith("Prize"))
            {
                if (LogFilter.Debug) Debug.LogFormat("OnControllerColliderHit {0}[{1}] with {2}[{3}]", name, netId, controllerColliderHitObject.name, controllerColliderHitObject.GetComponent<NetworkIdentity>().netId);

                // Disable the prize gameobject so it doesn't impede player movement
                // It's going to be destroyed in a few frames and we don't want to spam CmdClaimPrize.
                // OnControllerColliderHit will fire many times as the player slides against the object.
                controllerColliderHitObject.SetActive(false);

                CmdClaimPrize(controllerColliderHitObject);
            }
        }

        [Command]
        void CmdClaimPrize(GameObject hitObject)
        {
            // Null check is required, otherwise close timing of multiple claims could throw a null ref.
            if (hitObject != null)
            {
                hitObject.GetComponent<Reward>().ClaimPrize(gameObject);
            }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(10f + (index * 110), 10f, 100f, 25f), score.ToString().PadLeft(10));
        }
    }
}
