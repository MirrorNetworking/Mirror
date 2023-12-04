using Mirror;
using UnityEngine;

namespace StinkySteak.MirrorBenchmark
{
    public class GUIGame : BaseGUIGame
    {
        [SerializeField] private NetworkManager _networkManagerPrefab;
        private NetworkManager _networkManager;

        protected override void Initialize()
        {
            base.Initialize();
            _networkManager = Instantiate(_networkManagerPrefab);
            RegisterPrefabs(new StressTestEssential[] { _test_1, _test_2, _test_3 });
        }

        private void RegisterPrefabs(StressTestEssential[] stressTestEssential)
        {
            for (int i = 0; i < stressTestEssential.Length; i++)
            {
                _networkManager.spawnPrefabs.Add(stressTestEssential[i].Prefab);
            }
        }

        protected override void StartClient()
        {
            _networkManager.StartClient();
        }

        protected override void StartServer()
        {
            _networkManager.StartServer();
        }

        protected override void StressTest(StressTestEssential stressTest)
        {
            for (int i = 0; i < stressTest.SpawnCount; i++)
            {
                GameObject go = Instantiate(stressTest.Prefab);
                NetworkServer.Spawn(go);
            }
        }

        protected override void UpdateNetworkStats()
        {
            if (_networkManager == null) return;

            if (!_networkManager.isNetworkActive) return;

            if (_networkManager.mode == NetworkManagerMode.ServerOnly)
            {
                _textLatency.SetText("Latency: 0ms (Server)");
                return;
            }

            _textLatency.SetText("Latency: {0}ms", (float)NetworkTime.rtt * 1_000);
        }
    }
}
