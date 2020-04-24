#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using System.Collections;
using System.Diagnostics;
using Mirror.Examples;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mirror.Tests.Performance.Runtime
{
    [Category("Performance")]
    [Category("Benchmark")]
    public class BenchmarkPerformance
    {
        const string ScenePath = "Assets/Mirror/Examples/Benchmarks/10k/Scenes/Scene.unity";
        const float Warmup = 1f;
        const int MeasureCount = 120;

        readonly SampleGroupDefinition NetworkManagerSample = new SampleGroupDefinition("NetworkManagerLateUpdate", SampleUnit.Millisecond, AggregationType.Average);
        readonly Stopwatch stopwatch = new Stopwatch();

        bool captureMeasurement;
        void BeforeLateUpdate()
        {
            if (!captureMeasurement)
                return;

            stopwatch.Start();
        }
        void AfterLateUpdate()
        {
            if (!captureMeasurement)
                return;

            stopwatch.Stop();
            double time = stopwatch.Elapsed.TotalMilliseconds;
            Measure.Custom(NetworkManagerSample, time);
            stopwatch.Reset();
        }



        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureMeasurement = false;
            // load scene
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive });
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);

            // wait for NetworkManager awake
            yield return null;
            // load host
            NetworkManager.singleton.StartHost();

            BenchmarkNetworkManager benchmarker = NetworkManager.singleton as BenchmarkNetworkManager;
            if (benchmarker == null)
            {
                Assert.Fail("Could not find Benchmarker");
                yield break;
            }


            benchmarker.BeforeLateUpdate = BeforeLateUpdate;
            benchmarker.AfterLateUpdate = AfterLateUpdate;
        }

        IEnumerator RunBenchmark()
        {
            // warmup
            yield return new WaitForSecondsRealtime(Warmup);

            // run benchmark
            // capture frames and LateUpdate time

            captureMeasurement = true;

            yield return Measure.Frames().MeasurementCount(MeasureCount).Run();

            captureMeasurement = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // shutdown
            GameObject go = NetworkManager.singleton.gameObject;
            NetworkManager.Shutdown();
            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);

            // we must destroy networkmanager because it is marked as DontDestroyOnLoad
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }

        static void EnableHealth(bool value)
        {
            Health[] all = Health.FindObjectsOfType<Health>();
            foreach (Health health in all)
            {
                health.enabled = value;
            }
        }

        [UnityTest]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceUnityTest]
#endif
        public IEnumerator Benchmark10k()
        {
            EnableHealth(true);

            yield return RunBenchmark();
        }

        [UnityTest]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceUnityTest]
#endif
        public IEnumerator Benchmark10kIdle()
        {
            EnableHealth(false);

            yield return RunBenchmark();
        }
    }
}
#endif
