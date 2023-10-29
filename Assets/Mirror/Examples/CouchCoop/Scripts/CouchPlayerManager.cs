using UnityEngine;
using Mirror;

namespace Mirror.Examples.CouchCoop
{
    public class CouchPlayerManager : NetworkBehaviour
    {
        // reference to UI that should be in the scene
        public CanvasScript canvasScript;
        // for multiple player prefabs, currently not implemented, remember to add these into Network Managers Prefab array.
        public GameObject[] playerPrefabs;
        public int totalCouchPlayers = 0;

        // ignore key controls 0, we will always start at 1
        public KeyCode[] playerKeyJump;
        public KeyCode[] playerKeyLeft;
        public KeyCode[] playerKeyRight;

        // store a list of players so we know which to remove later
        // can be non sync-list, but may be useful for future features
        readonly SyncList<GameObject> couchPlayersList = new SyncList<GameObject>();

        public override void OnStartAuthority()
        {
            // hook up UI to local player, for cmd communication
#if UNITY_2021_3_OR_NEWER
            canvasScript = GameObject.FindAnyObjectByType<CanvasScript>();
#else
            // Deprecated in Unity 2023.1
            canvasScript = GameObject.FindObjectOfType<CanvasScript>();
#endif
            canvasScript.couchPlayerManager = this;
        }

        [Command]
        public void CmdAddPlayer()
        {
            if (totalCouchPlayers >= playerKeyJump.Length-1)
            {
                Debug.Log(name + " - No controls setup for further players.");
                return;
            }

            totalCouchPlayers += 1;
            Transform spawnObj = NetworkManager.startPositions[Random.Range(0, NetworkManager.startPositions.Count)];
            GameObject playerObj = Instantiate(playerPrefabs[0], spawnObj.position, spawnObj.rotation);
            CouchPlayer couchPlayer = playerObj.GetComponent<CouchPlayer>();
            couchPlayer.playerNumber = totalCouchPlayers;
            NetworkServer.Spawn(playerObj, connectionToClient);
            couchPlayersList.Add(playerObj);
        }

        [Command]
        public void CmdRemovePlayer()
        {
            if (totalCouchPlayers <= 0)
            {
                Debug.Log(name + " - No players to remove for that connection.");
                return;
            }

            totalCouchPlayers -= 1;
            NetworkServer.Destroy(couchPlayersList[couchPlayersList.Count - 1]);
            couchPlayersList.RemoveAt(couchPlayersList.Count - 1);
     
        }
    }

}
