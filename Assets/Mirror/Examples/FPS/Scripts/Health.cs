using UnityEngine;

namespace Mirror.Examples.FPS
{
    public class Health : NetworkBehaviour
    {
        public float startHealth = 100f;
        [SyncVar] float health;
        bool dead;

        public override void OnStartServer()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            health = startHealth;
        }

        public void DealDamage(float amount)
        {
            health -= amount;
            if (health < 0f)
            {
                RpcDespawn();
                SetDead(true);
                TargetGameOver(connectionToClient);
            }
        }

        [TargetRpc]
        void TargetGameOver(NetworkConnection conn)
        {
            CmdRespawn();
        }

        [Command]
        void CmdRespawn()
        {
            RpcRespawn();
            ResetHealth();
            SetDead(false);
        }

        [ClientRpc]
        void RpcDespawn()
        {
            SetDead(true);
        }

        [ClientRpc]
        void RpcRespawn()
        {
            SetDead(false);
        }

        void SetDead(bool dead)
        {
            this.dead = dead;
            GetComponent<MeshRenderer>().enabled = !dead;
            GetComponent<Collider>().enabled = !dead;
            GetComponent<CharacterController>().enabled = !dead;
        }
    }
}
