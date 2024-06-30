using UnityEngine;

namespace Mirror.Examples.Common
{
    public class TankProjectile : NetworkBehaviour
    {
        public float destroyAfter = 2;
        public Rigidbody rigidBody;
        public float force = 1000;

        // set velocity for server and client. this way we don't have to sync the
        // position, because both the server and the client simulate it.
        void Start()
        {
            rigidBody.AddForce(transform.forward * force);
            Destroy(gameObject, destroyAfter);
        }

        void OnTriggerEnter(Collider co)
        {
            Debug.Log("Hit: " + co.name);
            Destroy(gameObject);
        }
    }
}
