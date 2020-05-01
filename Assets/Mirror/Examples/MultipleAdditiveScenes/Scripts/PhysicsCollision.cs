using UnityEngine;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsCollision : NetworkBehaviour
    {
        [Tooltip("how forcefully to push this object")]
        public float force = 12;

        public Rigidbody rigidbody3D;

        private void OnValidate()
        {
            if (rigidbody3D == null)
                rigidbody3D = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            rigidbody3D.isKinematic = !isServer;
        }

        [ServerCallback]
        void OnCollisionStay(Collision other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                // get direction from which player is contacting object
                Vector3 direction = other.contacts[0].normal;

                // zero the y and normalize so we don't shove this through the floor or launch this over the wall
                direction.y = 0;
                direction = direction.normalized;

                // push this away from player...a bit less force for host player
                if (other.gameObject.GetComponent<NetworkIdentity>().connectionToClient.connectionId == 0)
                    rigidbody3D.AddForce(direction * force * .5f);
                else
                    rigidbody3D.AddForce(direction * force);
            }
        }
    }
}
