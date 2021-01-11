using UnityEngine;

namespace Mirror.Examples.RigidbodyPhysics
{
    public class AddForce : NetworkBehaviour
    {
        [SerializeField] float force = 500f;

        void Update()
        {
            if (IsServer && Input.GetKeyDown(KeyCode.Space))
            {
                GetComponent<Rigidbody>().AddForce(Vector3.up * force);
            }
        }
    }
}
