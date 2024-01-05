using UnityEngine;
using UnityEngine.AI;

namespace Mirror.Examples.TankTheftAuto
{
    public class TankController : NetworkBehaviour
    {
        [Header("Components")]
        public NavMeshAgent agent;
        public Animator animator;
        public Transform turret;

        [Header("Movement")]
        public float rotationSpeed = 100;

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public Transform projectileMount;

        void Update()
        {

            // take input from focused window only
            if (!Application.isFocused) return;

            // movement for local player
            if (isOwned)
            {
                // rotate
                float horizontal = Input.GetAxis("Horizontal");
                transform.Rotate(0, horizontal * rotationSpeed * Time.deltaTime, 0);

                // move
                float vertical = Input.GetAxis("Vertical");
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                agent.velocity = forward * Mathf.Max(vertical, 0) * agent.speed;
                animator.SetBool("Moving", agent.velocity != Vector3.zero);

                // shoot
                if (Input.GetKeyDown(shootKey))
                {
                    CmdFire();
                }

                RotateTurret();
            }
        }

        // this is called on the server
        [Command]
        void CmdFire()
        {
            GameObject projectile = Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
            NetworkServer.Spawn(projectile);
            RpcOnFire();
        }

        // this is called on the tank that fired for all observers
        [ClientRpc]
        void RpcOnFire()
        {
            animator.SetTrigger("Shoot");
        }

        void RotateTurret()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100))
            {
                Debug.DrawLine(ray.origin, hit.point);
                Vector3 lookRotation = new Vector3(hit.point.x, turret.transform.position.y, hit.point.z);
                turret.transform.LookAt(lookRotation);
            }
        }

        public PlayerController playerController;
        public Transform seatPosition;
        
        [SyncVar(hook = nameof(OnOwnerChangedHook))]
        public NetworkIdentity objectOwner;

        void OnOwnerChangedHook(NetworkIdentity _old, NetworkIdentity _new)
        {
            //Debug.Log("OnOwnerChangedHook: " + objectOwner);

            // not ideal to adjust local players control status (or character model being hidden) via this hook, but it works for now
            if (objectOwner)
            {
                playerController = _new.GetComponent<PlayerController>();
                if (playerController) { playerController.canControlPlayer = false; }
            }
            else if(_old)
            {
                playerController = _old.GetComponent<PlayerController>();
                if (playerController) { playerController.canControlPlayer = true; }
            }

        }

        public override void OnStopServer()
        {
            // To prevent a bug that can be caused on client host, when scenes do not reset during play,
            // tank variables are set as "missing", which Unity does not count as null/empty.
            objectOwner = null;
            playerController = null;
        }
    }
}
