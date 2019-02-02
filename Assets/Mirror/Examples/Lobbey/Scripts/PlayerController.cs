using UnityEngine;
using Mirror;
using Mirror.Components.NetworkLobby;

namespace Mirror.Examples.NetworkLobby
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [SyncVar]
        public int Index;

        [SyncVar(hook = "SetColor")]
        public Color playerColor = Color.black;

        [SyncVar]
        public uint score = 0;

        CharacterController characterController;

        public float moveSpeed = 300f;
        //public float turnSpeed = 1;

        public float horiz = 0f;
        public float vert = 0f;
        public float turn = 0f;

        public float turnSpeedAccel = 30;
        public float turnSpeedDecel = 30;
        public float maxTurnSpeed = 100;

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

        public override void OnStartServer()
        {
            base.OnStartServer();
            playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
            var lobby = NetworkManager.singleton as NetworkLobbyManager;
            Index = lobby.playerIndex;
            lobby.playerIndex++;
        }

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
            GetComponent<Renderer>().material.color = color;
        }

        [TargetRpc]
        public void TargetAddPoints(NetworkConnection networkConnection, uint points)
        {
            score += points;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10 + (Index * 110), 10, 100, 25), score.ToString().PadLeft(10));
        }
    }
}
