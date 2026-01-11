using UnityEngine;
using UnityEngine.AI;

namespace Mirror.Examples.TanksHybrid
{
    public class Tank : NetworkBehaviour
    {
        [Header("Components")]
        public NavMeshAgent agent;
        public Animator  animator;
        public TextMesh  healthBar;
        public Transform turret;

        [Header("Movement")]
        public float rotationSpeed = 100;

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public Transform  projectileMount;

        [Header("Stats")]
        public int health = 5; // not [SyncVar] because Weaver doesn't work with Hybrid yet. instead we sync it manually below.
        int lastHealth = 5;

        // naming for easier debugging
        public override void OnStartClient()
        {
            name = $"Player[{netId}|{(isLocalPlayer ? "local" : "remote")}]";
        }

        public override void OnStartServer()
        {
            name = $"Player[{netId}|server]";
        }

        void Update()
        {
            // manual setdirty test
            if (health != lastHealth)
            {
                SetDirty();
                lastHealth = health;
            }

            // always update health bar.
            // (SyncVar hook would only update on clients, not on server)
            healthBar.text = new string('-', health);

            // take input from focused window only
            if(!Application.isFocused) return;

            // movement for local player
            if (isLocalPlayer)
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

        //[ServerCallback]
        //void OnTriggerEnter(Collider other)
        //{
        //    if (other.GetComponent<Projectile>() != null)
        //    {
        //        --health;
        //        if (health == 0)
        //            NetworkServer.Destroy(gameObject);
        //    }
        //}

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

        // Health is serialized/deserialized manually.
        // setting the component to SyncMethod=Unreliable automatically takes care of the rest.
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // Debug.LogWarning($"Tank {name} OnSerialize {(initialState ? "full" : "delta")} health={health}");
            writer.WriteInt(health);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            health = reader.ReadInt();
            // Debug.LogWarning($"Tank {name} OnDeserialize {(initialState ? "full" : "delta")} health={health}");
        }
    }
}
