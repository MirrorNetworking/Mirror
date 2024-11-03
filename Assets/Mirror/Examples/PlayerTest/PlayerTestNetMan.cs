using UnityEngine;

namespace Mirror.Examples.PlayerTest
{
    public class PlayerTestNetMan : NetworkManager
    {
        public static new PlayerTestNetMan singleton => (PlayerTestNetMan)NetworkManager.singleton;

        [Header("Henchman")]
        public bool spawnHenchman;
        public GameObject henchmanPrefab;

        public override void OnValidate()
        {
            base.OnValidate();

            // don't use same prefab for player and henchman
            if (henchmanPrefab != null && playerPrefab == henchmanPrefab)
                henchmanPrefab = null;
        }

        public override void OnStartClient()
        {
            if (spawnHenchman && henchmanPrefab != null)
                NetworkClient.RegisterPrefab(henchmanPrefab);
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);

            if (spawnHenchman && henchmanPrefab != null)
            {
                Transform t = conn.identity.transform;
                Vector3 startPos = t.position + t.forward * 2;
                GameObject henchaman = Instantiate(henchmanPrefab, startPos, Quaternion.identity);
                NetworkServer.Spawn(henchaman, conn);
            }
        }
    }
}
