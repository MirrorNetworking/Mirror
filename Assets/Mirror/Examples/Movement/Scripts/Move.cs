using UnityEngine;
using UnityEngine.AI;

namespace Mirror.Examples.Movement
{
    public class Move : NetworkBehaviour
    {
        public NavMeshAgent agent;
        public Animator animator;
        public float rotationSpeed = 100;
        public KeyCode shootKey = KeyCode.Space;

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer) return;

            // rotate
            float horizontal = Input.GetAxis("Horizontal");
            transform.Rotate(0, horizontal * rotationSpeed * Time.deltaTime, 0);

            // move
            float vertical = Input.GetAxis("Vertical");
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            agent.velocity = forward * Mathf.Max(vertical, 0) * agent.speed;
            animator.SetBool("Moving", agent.velocity != Vector3.zero);

            // shoot
            if (Input.GetKeyDown(shootKey))
            {
                animator.SetTrigger("Shoot");
            }
        }
    }
}
