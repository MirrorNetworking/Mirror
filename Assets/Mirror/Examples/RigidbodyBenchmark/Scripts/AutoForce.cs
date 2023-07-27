using UnityEngine;
using Random = UnityEngine.Random;

namespace Mirror.Examples.RigidbodyBenchmark
{
    [RequireComponent(typeof(Rigidbody))]
    public class AutoForce : NetworkBehaviour
    {
        public Rigidbody rigidbody3d;
        public float force = 500;
        public float forceProbability = 0.05f;

        protected override void OnValidate()
        {
            base.OnValidate();
            rigidbody3d = GetComponent<Rigidbody>();
        }

        [ServerCallback]
        void FixedUpdate()
        {
            // do we have authority over this?
            if (rigidbody3d.isKinematic) return;

            // time to apply force?
            if (Random.value < forceProbability * Time.deltaTime)
            {
                rigidbody3d.AddForce(Vector3.up * force);
            }
        }
    }
}
