using UnityEngine;
using UnityEngine.AI;

namespace Mirror.Examples.Tanks
{
    public class Tank : NetworkBehaviour
    {
        [Header("Components")]
        public NavMeshAgent agent;
        public Animator animator;

        [Header("Movement")]
        public float rotationSpeed = 100;

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public Transform projectileMount;

        [Header("Game Stats")]
        [SyncVar]
        public int health;
        [SyncVar]
        public int lives;
        [SyncVar]
        public int score;
        [SyncVar]
        public bool isDead;

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer)
                return;

            if (health <= 0)
            {
                isDead = true;
                return;
            }

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
        }

        // this is called on the server
        [Command]
        void CmdFire()
        {
            GameObject projectile = Instantiate(projectilePrefab, projectileMount.position, transform.rotation);
            projectile.GetComponent<Projectile>().source = gameObject;
            NetworkServer.Spawn(projectile);
            RpcOnFire();
        }

        // this is called on the tank that fired for all observers
        [ClientRpc]
        void RpcOnFire()
        {
            animator.SetTrigger("Shoot");
        }

        public void RespawnButtonHandler()
        {
            if (!isLocalPlayer && isDead)
                return;

            CmdRespawn();
        }

        [ClientRpc]
        void RpcRespawn()
        {
            if (!isLocalPlayer && isDead)
                return;

            CmdRespawn();
        }

        [Command]
        void CmdRespawn()
        {
            lives--;

            Transform startPos = NetworkManager.singleton.GetStartPosition();
            GameObject player = Instantiate(NetworkManager.singleton.playerPrefab, startPos.position, startPos.rotation);
            NetworkServer.ReplacePlayerForConnection(connectionToClient, player);
            NetworkServer.Destroy(gameObject);
        }
    }
}
