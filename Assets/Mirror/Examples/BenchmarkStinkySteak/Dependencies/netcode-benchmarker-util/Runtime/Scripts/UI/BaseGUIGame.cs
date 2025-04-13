// using TMPro; // MIRROR CHANGE
using UnityEngine;
using UnityEngine.UI;

namespace StinkySteak.NetcodeBenchmark
{
    public class BaseGUIGame : MonoBehaviour
    {
        // [SerializeField] private Button _buttonStartServer; // MIRROR CHANGE: Canvas + TextMeshPro -> OnGUI
        // [SerializeField] private Button _buttonStartClient; // MIRROR CHANGE: Canvas + TextMeshPro -> OnGUI

        [Space]
        // MIRROR CHANGE
        protected string _textLatency = ""; // [SerializeField] protected TextMesh _textLatency; // MIRROR CHANGE: Canvas + TextMeshPro -> OnGUI
        [SerializeField] private float _updateLatencyTextInterval = 1f;
        private SimulationTimer.SimulationTimer _timerUpdateLatencyText;

        [Header("Stress Test 1: Move Y")]
        [SerializeField] protected StressTestEssential _test_1;

        [Header("Stress Test 2: Move All Axis")]
        [SerializeField] protected StressTestEssential _test_2;

        [Header("Stress Test 3: Move Wander")]
        [SerializeField] protected StressTestEssential _test_3;

        [System.Serializable]
        public struct StressTestEssential
        {
            // public Button ButtonExecute; // MIRROR CHANGE: Canvas + TextMeshPro -> OnGUI
            public int SpawnCount;
            public GameObject Prefab;
        }

        private void Start()
        {
            Initialize();
        }

        // MIRROR CHANGE: OnGUI instead of Canvas + TextMeshPro
        protected virtual void Initialize()
        {
        //     _test_1.ButtonExecute.onClick.AddListener(StressTest_1);
        //     _test_2.ButtonExecute.onClick.AddListener(StressTest_2);
        //     _test_3.ButtonExecute.onClick.AddListener(StressTest_3);
        //
        //     _buttonStartServer.onClick.AddListener(StartServer);
        //     _buttonStartClient.onClick.AddListener(StartClient);
        }
        protected virtual void OnCustomGUI() {}

#if !UNITY_SERVER
        protected virtual void OnGUI()
        {
            GUILayout.BeginArea(new Rect(100, 100, 300, 400));

            if (GUILayout.Button("Stress Test 1"))
            {
                StressTest_1();
            }
            if (GUILayout.Button("Stress Test 2"))
            {
                StressTest_2();
            }
            if (GUILayout.Button("Stress Test 3"))
            {
                StressTest_3();
            }

            OnCustomGUI();

            GUILayout.Label(_textLatency);

            GUILayout.EndArea();
        }
#endif
        // END MIRROR CHANGE

        protected virtual void StartClient() { }
        protected virtual void StartServer() { }
        private void StressTest_1() => StressTest(_test_1);
        private void StressTest_2() => StressTest(_test_2);
        private void StressTest_3() => StressTest(_test_3);
        protected virtual void StressTest(StressTestEssential stressTest) { }


        private void LateUpdate()
        {
            if (!_timerUpdateLatencyText.IsExpiredOrNotRunning()) return;

            UpdateNetworkStats();
            _timerUpdateLatencyText = SimulationTimer.SimulationTimer.CreateFromSeconds(_updateLatencyTextInterval);
        }

        protected virtual void UpdateNetworkStats() { }
    }
}
