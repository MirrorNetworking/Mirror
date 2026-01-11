using System.Threading.Tasks;
using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    internal static class Spawner
    {
        static GameObject prefab;
        static byte poolSize = 10;
        static Pool<GameObject> pool;
        static ushort counter;

        // Called from custom network manager on both server and client
        internal static void InitializePool(GameObject poolPrefab, byte count)
        {
            prefab = poolPrefab;
            poolSize = count;

            NetworkClient.RegisterPrefab(prefab, SpawnHandler, UnspawnHandler);
            pool = new Pool<GameObject>(CreateNew, poolSize);
        }

        // Called from custom network manager on both server and client
        internal static void ClearPool()
        {
            if (prefab == null) return;

            NetworkClient.UnregisterPrefab(prefab);

            if (pool == null) return;

            while (pool.Count > 0)
                Object.Destroy(pool.Get());

            counter = 0;
            pool = null;
        }

        static GameObject SpawnHandler(SpawnMessage msg) => Get(msg.position, msg.rotation);

        static void UnspawnHandler(GameObject spawned)
        {
            // disable object
            spawned.SetActive(false);

            // move the object out of reach so OnTriggerEnter doesn't get called
            spawned.transform.position = new Vector3(0, -1000, 0);

            // add back to pool
            pool.Return(spawned);
        }

        static GameObject CreateNew()
        {
            GameObject next = Object.Instantiate(prefab);
            counter++;
            next.name = $"{prefab.name}_pooled_{counter:00}";
            next.SetActive(false);
            return next;
        }

        static GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject next = pool.Get();

            // set position/rotation and set active
            next.transform.SetPositionAndRotation(position, rotation);
            next.SetActive(true);
            return next;
        }

        // Called from custom network manager
        [ServerCallback]
        internal static void InitialSpawn()
        {
            for (byte i = 0; i < poolSize; i++)
                SpawnReward();
        }

        // Called from the Reward script
        [ServerCallback]
        internal static async void RecycleReward(GameObject reward)
        {
            NetworkServer.UnSpawn(reward);
            await DelayedSpawn();
        }

        [ServerCallback]
        static async Task DelayedSpawn()
        {
            await Task.Delay(new System.TimeSpan(0, 0, 1));
            SpawnReward();
        }

        [ServerCallback]
        static void SpawnReward()
        {
            Vector3 spawnPosition = new Vector3(Random.Range(-19, 20), 1, Random.Range(-19, 20));
            NetworkServer.Spawn(Get(spawnPosition, Quaternion.identity));
        }
    }
}
