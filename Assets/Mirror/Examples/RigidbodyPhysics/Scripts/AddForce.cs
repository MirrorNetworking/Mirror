using UnityEngine;

namespace Mirror.Examples.RigidbodyPhysics
{
    public class AddForce : NetworkBehaviour
    {
        [SerializeField] Rigidbody rb;
        [SerializeField] float force = 500f;

        private void Start()
        {
            rb.isKinematic = !isServer;
        }

        void Update()
        {
            if (isServer && Input.GetKeyDown(KeyCode.Space))
            {
                rb.AddForce(Vector3.up * force);
            }
        }
    }
}
