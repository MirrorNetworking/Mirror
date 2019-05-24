using UnityEngine;

namespace Mirror.Examples.NetworkLobby
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        CharacterController characterController;

        public float moveSpeed = 300f;
        public float turnSpeedAccel = 30f;
        public float turnSpeedDecel = 30f;
        public float maxTurnSpeed = 100f;

        [SyncVar]
        public int Index;

        [SyncVar]
        public uint score;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            characterController = GetComponent<CharacterController>();

            // Turn off main camera because GamePlayer prefab has its own camera
            GetComponentInChildren<Camera>().enabled = true;
            Camera.main.enabled = false;
        }

        [SyncVar(hook = nameof(SetColor))]
        public Color playerColor = Color.black;

        // Unity makes a clone of the material when GetComponent<Renderer>().material is used
        // Cache it here and Destroy it in OnDestroy to prevent a memory leak
        Material materialClone;

        void SetColor(Color color)
        {
            if (materialClone == null) materialClone = GetComponent<Renderer>().material;
            materialClone.color = color;
        }

        private void OnDestroy()
        {
            Destroy(materialClone);
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
        public void CmdClaimPrize(GameObject hitObject)
        {
            // Null check is required, otherwise close timing of multiple claims could throw a null ref.
            if (hitObject != null)
            {
                hitObject.GetComponent<Reward>().ClaimPrize(gameObject);
            }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(10f + (Index * 110), 10f, 100f, 25f), score.ToString().PadLeft(10));
        }
    }
}
