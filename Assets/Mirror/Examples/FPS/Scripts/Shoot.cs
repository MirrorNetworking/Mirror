using UnityEngine;

namespace Mirror.Examples.FPS
{
    public class Shoot : NetworkBehaviour
    {
        public float damagePerShot = 10f;
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
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 100f))
            {
                hit.collider.GetComponent<Health>()?.DealDamage(damagePerShot);
            }
        }
    }
}
