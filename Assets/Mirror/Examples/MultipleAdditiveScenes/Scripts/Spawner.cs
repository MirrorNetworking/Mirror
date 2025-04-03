using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    internal static class Spawner
    {
        static GameObject prefab;
        static byte poolSize = 10;
        static Pool<GameObject> pool;
        static ushort counter;

        internal static void InitializePool(GameObject poolPrefab, byte count)
        {
            prefab = poolPrefab;
            poolSize = count;

            NetworkClient.RegisterPrefab(prefab, SpawnHandler, UnspawnHandler);
            pool = new Pool<GameObject>(CreateNew, poolSize);
        }

        internal static void ClearPool()
        {
            if (prefab == null) return;

            NetworkClient.UnregisterPrefab(prefab);

            if (pool == null) return;

            // destroy all objects in pool
            while (pool.Count > 0)
                Object.Destroy(pool.Get());

            counter = 0;
            pool = null;
        }

        static GameObject SpawnHandler(SpawnMessage msg) => Get(msg.position, msg.rotation);

        static void UnspawnHandler(GameObject spawned) => Return(spawned);

        static GameObject CreateNew()
        {
            // use this object as parent so that objects dont crowd hierarchy
            GameObject next = Object.Instantiate(prefab);
            counter++;
            next.name = $"{prefab.name}_pooled_{counter:00}";
            next.SetActive(false);
            return next;
        }

        public static GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject next = pool.Get();

            // set position/rotation and set active
            next.transform.SetPositionAndRotation(position, rotation);
            next.SetActive(true);
            return next;
        }

        // Used to put object back into pool so they can b
        // Should be used on server after unspawning an object
        // Used on client by NetworkClient to unspawn objects
        public static void Return(GameObject spawned)
        {
            // disable object
            spawned.SetActive(false);

            // move the object out of reach so OnTriggerEnter doesn't get called
            spawned.transform.position = new Vector3(0, -1000, 0);

            // add back to pool
            pool.Return(spawned);
        }

        [ServerCallback]
        internal static void InitialSpawn(Scene scene)
        {
            for (int i = 0; i < 10; i++)
                SpawnReward(scene);
        }

        [ServerCallback]
        internal static void SpawnReward(Scene scene)
        {
            Vector3 spawnPosition = new Vector3(Random.Range(-19, 20), 1, Random.Range(-19, 20));
            GameObject reward = Get(spawnPosition, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(reward, scene);
            NetworkServer.Spawn(reward);
        }

        [ServerCallback]
        internal static async void RecycleReward(GameObject reward)
        {
            NetworkServer.UnSpawn(reward);
            await DelayedSpawn(reward.scene);
        }

        static async Task DelayedSpawn(Scene scene)
        {
            await Task.Delay(new System.TimeSpan(0, 0, 1));
            SpawnReward(scene);
        }
    }
}
