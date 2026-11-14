using UnityEngine;

namespace Mirror.Examples.PredictionBenchmark
{
    [AddComponentMenu("")]
    public class NetworkManagerPredictionBenchmark : NetworkManager
    {
        [Header("Spawns")]
        public int spawnAmount = 1000;
        public GameObject spawnPrefab;
        public Bounds spawnArea = new Bounds(new Vector3(0, 2.5f, 0), new Vector3(10f, 5f, 10f));

        public override void Awake()
        {
            base.Awake();

            // ensure vsync is disabled for the benchmark, otherwise results are capped
            QualitySettings.vSyncCount = 0;
        }

        void SpawnAll()
        {
            // spawn randomly inside the cage
            for (int i = 0; i < spawnAmount; ++i)
            {
                // choose a random point within the cage
                float x = Random.Range(spawnArea.min.x, spawnArea.max.x);
                float y = Random.Range(spawnArea.min.y, spawnArea.max.y);
                float z = Random.Range(spawnArea.min.z, spawnArea.max.z);
                Vector3 position = new Vector3(x, y, z);

                // spawn & position
                GameObject go = Instantiate(spawnPrefab);
                go.transform.position = position;
                NetworkServer.Spawn(go);
            }
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
