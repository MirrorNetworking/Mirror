using UnityEngine;

namespace Mirror.Examples.FPS
{
    public class FPSMove : NetworkBehaviour
    {
        public CharacterController controller;
        public float speed = 300;
        public float rotationSpeed = 400;
        public Transform head;
        float pitch;

        void Start()
        {
            // Turn on camera for local player
            if (hasAuthority) head.gameObject.SetActive(true);
        }

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer) return;

            // rotate
            transform.Rotate(0, Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime, 0);

            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -90f, 90f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // move
            Vector3 move = transform.forward * Input.GetAxis("Vertical") + transform.right * Input.GetAxis("Horizontal");
            controller.SimpleMove(move * speed * Time.deltaTime);
        }
    }
}
