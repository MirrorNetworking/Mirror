using UnityEngine;

namespace Mirror.Examples.RigidbodyPhysics
{
    public class AddForce : NetworkBehaviour
    {
        public Rigidbody rigidbody3d;
        public float force = 500f;

        void Start()
        {
            rigidbody3d.isKinematic = !isServer;
        }

        void Update()
        {
            if (isServer && Input.GetKeyDown(KeyCode.Space))
            {
                rigidbody3d.AddForce(Vector3.up * force);
            }
        }
    }
}
