using UnityEngine;
using Mirror;

namespace TestNT
{
    public class PlayerMinions : NetworkBehaviour
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            this.enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            this.enabled = true;
        }

        public override void OnStopLocalPlayer()
        {
            this.enabled = false;
        }

        void Update()
        {
            // Only spawn the minions once
            if (Input.GetKeyDown(KeyCode.M))
            {
                CmdSpawnMinions();
                this.enabled = false;
            }
        }

        [Command]
        void CmdSpawnMinions()
        {
            for (int z = 2; z < 10; z += 2)
                for (int x = -9; x < 10; x += 2)
                {
                    Vector3 spawnPos = new Vector3(transform.position.x + x, transform.position.y, transform.position.z + z);
                    GameObject minion = Instantiate(NetworkManager.singleton.playerPrefab, spawnPos, Quaternion.identity);
                    NetworkServer.Spawn(minion, connectionToClient);
                }
        }
    }
}
