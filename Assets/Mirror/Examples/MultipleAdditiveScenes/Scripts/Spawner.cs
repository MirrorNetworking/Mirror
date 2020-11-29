using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    internal class Spawner : MonoBehaviour
    {
        internal static void InitialSpawn(Scene scene)
        {
            if (!NetworkServer.active) return;

            for (int i = 0; i < 10; i++)
                SpawnReward(scene);
        }

        internal static void SpawnReward(Scene scene)
        {
            if (!NetworkServer.active) return;

            Vector3 spawnPosition = new Vector3(Random.Range(-19, 20), 1, Random.Range(-19, 20));
            GameObject reward = Instantiate(((MultiSceneNetManager)NetworkManager.singleton).rewardPrefab, spawnPosition, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(reward, scene);
            NetworkServer.Spawn(reward);
        }
    }
}
