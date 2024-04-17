using UnityEngine;

namespace Mirror.Examples.PredictionBenchmark
{
    [AddComponentMenu("")]
    public class NetworkManagerStackedPrediction : NetworkManager
    {
        [Header("Spawns")]
        public int spawnAmount = 1000;
        public GameObject spawnPrefab;
        public float interleave = 1;

        // 500 objects need around 100 iterations to be stable
        [Tooltip("Stacked Cubes are only stable if solver iterations are high enough!")]
        public int solverIterations = 200;

        public override void Awake()
        {
            base.Awake();

            // ensure vsync is disabled for the benchmark, otherwise results are capped
            QualitySettings.vSyncCount = 0;

            // stacked cubes are only stable if solver iteration is high enough!
            int before = Physics.defaultSolverIterations;
            Physics.defaultSolverIterations = solverIterations;
            Debug.Log($"Physics.defaultSolverIterations: {before} -> {Physics.defaultSolverIterations}");
        }

        void SpawnAll()
        {
            // calculate sqrt so we can spawn N * N = Amount
            float sqrt = Mathf.Sqrt(spawnAmount);

            // calculate spawn xz start positions
            // based on spawnAmount * distance
            float offset = -sqrt / 2 * interleave;

            // spawn exactly the amount, not one more.
            int spawned = 0;
            for (int spawnX = 0; spawnX < sqrt; ++spawnX)
            {
                for (int spawnY = 0; spawnY < sqrt; ++spawnY)
                {
                    // spawn exactly the amount, not any more
                    // (our sqrt method isn't 100% precise)
                    if (spawned < spawnAmount)
                    {
                        // it's important to have them at least 'Physics.defaultContactOffset' apart.
                        // otherwise the physics engine will detect collisions and make them unstable.
                        float spacing = interleave + Physics.defaultContactOffset;
                        float x = offset + spawnX * spacing;
                        float y = spawnY * spacing;

                        // instantiate & position
                        GameObject go = Instantiate(spawnPrefab);
                        go.transform.position = new Vector3(x, y, 0);

                        // spawn
                        NetworkServer.Spawn(go);
                        ++spawned;
                    }
                }
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SpawnAll();

            // disable rendering on server to reduce noise in profiling.
            // keep enabled in host mode though.
            // if (mode == NetworkManagerMode.ServerOnly)
            //     Camera.main.enabled = false;
        }
    }
}
