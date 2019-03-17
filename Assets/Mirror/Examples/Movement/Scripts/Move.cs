using UnityEngine;
using UnityEngine.AI;

namespace Mirror.Examples.Movement
{
    public class Move : NetworkBehaviour
    {
        public NavMeshAgent agent;
        public float rotationSpeed = 100;

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer) return;

            // rotate
            transform.Rotate(0, Input.GetAxis("Horizontal") * rotationSpeed * Time.deltaTime, 0);

            // move
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            agent.velocity = forward * Input.GetAxis("Vertical") * agent.speed;
        }
    }
}
