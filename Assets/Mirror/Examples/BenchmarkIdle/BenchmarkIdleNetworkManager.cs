using UnityEngine;

namespace Mirror.Examples.BenchmarkIdle
{
    [AddComponentMenu("")]
    public class BenchmarkIdleNetworkManager : NetworkManager
    {
        [Header("Spawns")]
        public int spawnAmount = 10_000;
        public float      interleave = 1;
        public GameObject spawnPrefab;

        // player spawn positions should be spread across the world.
        // not all at one place.
        // but _some_ at the same place.
        // => deterministic random is ideal
        [Range(0, 1)] public float spawnPositionRatio = 0.01f;

        System.Random random = new System.Random(42);

        void SpawnAll()
        {
            // clear previous player spawn positions in case we start twice
            foreach (Transform position in startPositions)
                Destroy(position.gameObject);

            startPositions.Clear();

            // calculate sqrt so we can spawn N * N = Amount
            float sqrt = Mathf.Sqrt(spawnAmount);

            // calculate spawn xz start positions
            // based on spawnAmount * distance
            float offset = -sqrt / 2 * interleave;

            // spawn exactly the amount, not one more.
            int spawned = 0;
            for (int spawnX = 0; spawnX < sqrt; ++spawnX)
            {
                for (int spawnZ = 0; spawnZ < sqrt; ++spawnZ)
                {
                    // spawn exactly the amount, not any more
                    // (our sqrt method isn't 100% precise)
                    if (spawned < spawnAmount)
                    {
                        // spawn & position
                        GameObject go = Instantiate(spawnPrefab);
                        float x = offset + spawnX * interleave;
                        float z = offset + spawnZ * interleave;
                        Vector3 position = new Vector3(x, 0, z);
                        go.transform.position = position;

                        // spawn
                        NetworkServer.Spawn(go);
                        ++spawned;

                        // add random spawn position for players.
                        // don't have them all in the same place.
                        if (random.NextDouble() <= spawnPositionRatio)
                        {
                            GameObject spawnGO = new GameObject("Spawn");
                            spawnGO.transform.position = position;
                            spawnGO.AddComponent<NetworkStartPosition>();
                        }
                    }
                }
            }
        }

        // overwrite random spawn position selection:
        // - needs to be deterministic so every CCU test results in the same
        // - needs to be random so not only are the spawn positions spread out
        //   randomly, we also have a random amount of players per spawn position
        public override Transform GetStartPosition()
        {
            // first remove any dead transforms
            startPositions.RemoveAll(t => t == null);

            if (startPositions.Count == 0)
                return null;

            // pick a random one
            int index = random.Next(0, startPositions.Count); // DETERMINISTIC
            return startPositions[index];
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SpawnAll();

            // disable rendering on server to reduce noise in profiling.
            // keep enabled in host mode though.
            if (mode == NetworkManagerMode.ServerOnly)
                Camera.main.enabled = false;
        }
    }
}
