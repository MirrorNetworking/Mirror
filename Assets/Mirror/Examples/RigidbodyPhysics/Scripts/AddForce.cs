using UnityEngine;

namespace Mirror.Examples.RigidbodyPhysics
{
    public class AddForce : NetworkBehaviour
    {
        [SerializeField] Rigidbody rigidbody3d;
        [SerializeField] float force = 500f;

        private void Start()
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
