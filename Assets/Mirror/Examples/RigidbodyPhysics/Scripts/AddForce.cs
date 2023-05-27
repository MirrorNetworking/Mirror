using UnityEngine;

namespace Mirror.Examples.RigidbodyPhysics
{
    [RequireComponent(typeof(Rigidbody))]
    public class AddForce : NetworkBehaviour
    {
        public Rigidbody rigidbody3d;
        public float force = 500f;

        protected override void OnValidate()
        {
            base.OnValidate();

            rigidbody3d = GetComponent<Rigidbody>();
            rigidbody3d.isKinematic = true;
        }

        public override void OnStartServer()
        {
            rigidbody3d.isKinematic = false;
        }

        [ServerCallback]
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                rigidbody3d.AddForce(Vector3.up * force);
        }
    }
}
