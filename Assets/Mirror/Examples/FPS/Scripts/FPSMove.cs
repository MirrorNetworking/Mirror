using UnityEngine;

namespace Mirror.Examples.FPS
{
    public class FPSMove : NetworkBehaviour
    {
        public CharacterController controller;
        public float speed = 300;
        public float rotationSpeed = 400;
        public Transform headTransform;
        public Transform cameraTransform;
        float pitch;

        void Start()
        {
            // Turn on camera for local player
            if (hasAuthority)
            {
                cameraTransform.gameObject.SetActive(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer) return;

            // rotate
            transform.Rotate(0, Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime, 0);

            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -90f, 90f);
            headTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // move
            Vector3 move = transform.forward * Input.GetAxisRaw("Vertical") + transform.right * Input.GetAxisRaw("Horizontal");
            controller.SimpleMove(move.normalized * speed * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
