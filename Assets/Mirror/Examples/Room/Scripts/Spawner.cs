using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    internal class Spawner
    {
        internal static void InitialSpawn()
        {
            if (!NetworkServer.active) return;

            for (int i = 0; i < 10; i++)
                SpawnReward();
        }

        internal static void SpawnReward()
        {
            if (!NetworkServer.active) return;

            Vector3 spawnPosition = new Vector3(Random.Range(-19, 20), 1, Random.Range(-19, 20));
            NetworkServer.Spawn(Object.Instantiate(((NetworkRoomManagerExt)NetworkManager.singleton).rewardPrefab, spawnPosition, Quaternion.identity));
        }
    }
}
