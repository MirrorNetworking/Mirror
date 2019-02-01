using UnityEngine;
using Mirror;

namespace Mirror.Examples.Lobby
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [SyncVar(hook = "SetColor")]
        public Color playerColor = Color.black;

        CharacterController characterController;

        public float moveSpeed = 300f;
        public float turnSpeed = 100f;

        float horiz = 0f;
        float vert = 0f;

        void Update()
        {
            if (!isLocalPlayer) return;

            horiz = Input.GetAxis("Horizontal");
            vert = Input.GetAxis("Vertical");
        }

        void FixedUpdate()
        {
            if (!isLocalPlayer || characterController == null) return;

            transform.Rotate(0f, horiz * turnSpeed * Time.deltaTime, 0f);

            Vector3 forward = transform.TransformDirection(Vector3.forward);
            characterController.SimpleMove(forward * vert * moveSpeed * Time.deltaTime);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            characterController = GetComponent<CharacterController>();

            Camera.main.transform.parent = gameObject.transform;
            Camera.main.transform.localPosition = new Vector3(0, 2, -6);
            Camera.main.transform.localRotation = new Quaternion(0, 0, 0, 0);
        }

        void SetColor(Color color)
        {
            GetComponent<Renderer>().material.color = color;
        }
    }
}
