using Mirror;
using StinkySteak.NetcodeBenchmark;
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

        // MIRROR CHANGE: OnGUI instead of Canvas + TextMeshPro
        protected override void OnCustomGUI()
        {
            if (GUILayout.Button("Start Client"))
            {
                _networkManager.StartClient();
            }
            if (GUILayout.Button("Start Server"))
            {
                _networkManager.StartServer();
            }
        }
        // END MIRROR CHANGE

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
                _textLatency = ("Latency: 0ms (Server)"); // MIRROR CHANGE: Canvas + TextMeshPro -> OnGUI
                return;
            }

            _textLatency = ($"Latency: {NetworkTime.rtt * 1_000}ms"); // MIRROR CHANGE: Canvas + TextMeshPro -> OnGUI
        }
    }
}
