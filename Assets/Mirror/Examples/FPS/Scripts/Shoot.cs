using UnityEngine;

namespace Mirror.Examples.FPS
{
    public class Shoot : NetworkBehaviour
    {
        public float damagePerShot = 10f;
        public float range = 100f;
        public float pushStrength = 1000f;
        public float upStrength = 600f;
        public Transform head;
        void Update()
        {
            if (!isLocalPlayer) return;
            if (Input.GetMouseButtonDown(0))
            {
                CmdShoot();
            }
        }

        [Command]
        void CmdShoot()
        {
            // Find what was shot at
            if (Physics.Raycast(head.position, head.forward, out RaycastHit hit, range))
            {
                // Deal damage if it has health
                hit.collider.GetComponent<Health>()?.DealDamage(damagePerShot);

                // Push it if it has a rigidbody
                Vector3 force = head.forward * pushStrength + Vector3.up * upStrength;
                hit.collider.GetComponent<Rigidbody>()?.AddForceAtPosition(force, hit.point);
            }
        }
    }
}
