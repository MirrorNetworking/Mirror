using UnityEngine;
using Mirror;

namespace Mirror.Examples.Movement
{
    public class Move : NetworkBehaviour
    {
        public CharacterController controller;
        public float speed = 300;
        public float rotationSpeed = 400;

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer) return;

            // rotate
            transform.Rotate(0, Input.GetAxis("Horizontal") * rotationSpeed * Time.deltaTime, 0);

            // move
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            controller.SimpleMove(forward * Input.GetAxis("Vertical") * speed * Time.deltaTime);
        }
    }
}