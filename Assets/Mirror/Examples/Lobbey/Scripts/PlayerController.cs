using UnityEngine;

namespace Mirror.Examples.NetworkLobby
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [SyncVar]
        public int Index;

        [SyncVar]
        public uint score = 0;

        [SyncVar(hook = "SetColor")]
        public Color playerColor = Color.black;

        CharacterController characterController;

        public float moveSpeed = 300f;

        public float horiz = 0f;
        public float vert = 0f;
        public float turn = 0f;

        public float turnSpeedAccel = 30;
        public float turnSpeedDecel = 30;
        public float maxTurnSpeed = 100;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            characterController = GetComponent<CharacterController>();

            Camera.main.transform.parent = gameObject.transform;
            Camera.main.transform.localPosition = new Vector3(0, 3, -8);
            Camera.main.transform.localRotation = Quaternion.identity;
        }

        void SetColor(Color color)
        {
            //Debug.LogWarningFormat("PlayerController SetColor netId:{0} to {1}", netId, color);
            GetComponent<Renderer>().material.color = color;
        }

        private void Start()
        {
            // This is a workaround pending a fix for https://github.com/vis2k/Mirror/issues/372
            SetColor(playerColor);
        }

        void Update()
        {
            if (!isLocalPlayer) return;

            horiz = Input.GetAxis("Horizontal");
            vert = Input.GetAxis("Vertical");

            if ((Input.GetKey(KeyCode.Q)) && (turn > -maxTurnSpeed))
                turn = turn - turnSpeedAccel;
            else if ((Input.GetKey(KeyCode.E)) && (turn < maxTurnSpeed))
                turn = turn + turnSpeedAccel;
            else
            {
                if (turn > turnSpeedDecel)
                    turn = turn - turnSpeedDecel;
                else if (turn < -turnSpeedDecel)
                    turn = turn + turnSpeedDecel;
                else
                    turn = 0;
            }
        }

        void FixedUpdate()
        {
            if (!isLocalPlayer || characterController == null) return;

            transform.Rotate(0f, turn * Time.deltaTime, 0f);

            Vector3 forward = transform.TransformDirection((Vector3.ClampMagnitude(new Vector3(horiz, 0, vert), 1) * moveSpeed));
            characterController.SimpleMove(forward * Time.fixedDeltaTime);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // If player and prize objects are on their own layer(s) with correct
            // collision matrix, we wouldn't have to validate the hit.gameobject.
            // Since this is just an example, project settings aren't included so we check the name.

            GameObject hitGO = hit.gameObject;

            if (isLocalPlayer && hitGO.name == "Prize(Clone)")
            {
                if (LogFilter.Debug) Debug.LogFormat("OnControllerColliderHit {0}[{1}] with {2}[{3}]", name, netId, hitGO.name, hitGO.GetComponent<NetworkIdentity>().netId);
                CmdClaimPrize(hitGO);
            }
        }

        [Command]
        public void CmdClaimPrize(GameObject hitGO)
        {
            hitGO.GetComponent<Reward>().ClaimPrize(gameObject);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10 + (Index * 110), 10, 100, 25), score.ToString().PadLeft(10));
        }
    }
}
