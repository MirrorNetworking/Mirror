using System.Collections;
using System.Diagnostics;
using Mirror.Examples;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests
{
    [Category("Performance")]
    [Category("Benchmark")]
    public class BenchmarkPerformance
    {
        const string ScenePath = "Assets/Mirror/Examples/Benchmarks/10k/Scenes/Scene.unity";
        const float Warmup = 1f;
        const int MeasureCount = 120;

        readonly SampleGroup NetworkManagerSample = new SampleGroup("NetworkManagerLateUpdate", SampleUnit.Millisecond);
        readonly Stopwatch stopwatch = new Stopwatch();
        bool captureMeasurement;
        private BenchmarkNetworkManager benchmarker;

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
            benchmarker = Object.FindObjectOfType<BenchmarkNetworkManager>();

            if (benchmarker == null)
            {
                Assert.Fail("Could not find Benchmarker");
                yield break;
            }


            benchmarker.BeforeLateUpdate = BeforeLateUpdate;
            benchmarker.AfterLateUpdate = AfterLateUpdate;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // run benchmark
            yield return Measure.Frames().MeasurementCount(MeasureCount).Run();

            // shutdown
            benchmarker.StopHost();
            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);

            GameObject.Destroy(benchmarker.gameObject);
        }

        [UnityTest]
        [Performance]
        public IEnumerator Benchmark10k()
        {
            // warmup
            yield return new WaitForSecondsRealtime(Warmup);

            captureMeasurement = true;

            for (int i = 0; i < MeasureCount; i++)
            {
                yield return null;
            }

            captureMeasurement = false;
        }
    }
}
