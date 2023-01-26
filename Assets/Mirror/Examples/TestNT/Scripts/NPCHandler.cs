using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace TestNT
{
    public class NPCHandler : NetworkBehaviour
    {
        readonly static List<GameObject> NpcList = new List<GameObject>();

        void OnValidate()
        {
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
            if (Input.GetKeyDown(KeyCode.N))
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    CmdKillNPC();
                else
                    CmdSpawnNPC();
        }

        [Command]
        void CmdSpawnNPC()
        {
            Vector3 spawnPos = transform.position + Vector3.forward;
            GameObject npc = Instantiate(NetworkManager.singleton.spawnPrefabs[0], spawnPos, Quaternion.identity);
            npc.GetComponent<PlayerName>().playerName = "NPC";
            npc.GetComponent<CharacterController>().enabled = true;
            npc.GetComponent<PlayerMove>().enabled = true;
            NetworkServer.Spawn(npc);
            NpcList.Add(npc);
        }

        [Command]
        void CmdKillNPC()
        {
            if (NpcList.Count > 0)
            {
                NetworkServer.Destroy(NpcList[0]);
                NpcList.RemoveAt(0);
            }
        }
    }
}
