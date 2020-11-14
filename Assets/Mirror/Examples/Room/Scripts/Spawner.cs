using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.Examples.NetworkRoom
{
    public class Spawner: MonoBehaviour
    {
        [FormerlySerializedAs("prizePrefab")]
        internal static GameObject rewardPrefab;

        internal static void InitialSpawn()
        {
            for (int i = 0; i < 10; i++)
                SpawnReward();
        }

        internal static void SpawnReward()
        {
            Vector3 spawnPosition = new Vector3(Random.Range(-19, 20), 1, Random.Range(-19, 20));
            NetworkServer.Spawn(Instantiate(rewardPrefab, spawnPosition, Quaternion.identity));
        }
    }
}
