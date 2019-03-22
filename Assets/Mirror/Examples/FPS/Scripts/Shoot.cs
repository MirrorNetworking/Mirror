using UnityEngine;

namespace Mirror.Examples.FPS
{
    public class Shoot : NetworkBehaviour
    {
        public float damagePerShot = 10f;
        public float range = 100f;
        public float pushStrength = 1000f;
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
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, range))
            {
                // Deal damage if it has health
                hit.collider.GetComponent<Health>()?.DealDamage(damagePerShot);

                // Push it if it has a rigidbody
                hit.collider.GetComponent<Rigidbody>()?.AddForceAtPosition(transform.forward * pushStrength, hit.point);
            }
        }
    }
}
