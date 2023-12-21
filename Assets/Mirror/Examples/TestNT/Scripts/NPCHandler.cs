using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace TestNT
{
    public class NPCHandler : NetworkBehaviour
    {
        readonly static List<GameObject> NpcList = new List<GameObject>();

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
            if (Input.GetKeyDown(KeyCode.N))
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    CmdKillNPC();
                    CmdKillNPC();
                }
                else
                    CmdSpawnNPC();
        }

        [Command]
        void CmdSpawnNPC()
        {
            StartCoroutine(SpawnNPC());
        }

        IEnumerator SpawnNPC()
        {
            GameObject npc = Instantiate(TestNTNetworkManager.singleton.npcPrefab);
            npc.GetComponent<PlayerName>().playerName = "NPC";
            npc.GetComponent<CharacterController>().enabled = true;
            npc.GetComponent<PlayerMove>().enabled = true;
            NetworkServer.Spawn(npc);
            NpcList.Add(npc);

            yield return new WaitForSeconds(.3f);

            GameObject npcNinja = Instantiate(TestNTNetworkManager.singleton.npcNinjaPrefab);
            npcNinja.GetComponent<PlayerName>().playerName = "NPC-Ninja";
            npcNinja.GetComponent<CharacterController>().enabled = true;
            npcNinja.GetComponent<PlayerMove>().enabled = true;
            NetworkServer.Spawn(npcNinja);
            NpcList.Add(npcNinja);
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
