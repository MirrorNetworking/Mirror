using System.Collections;
using Cysharp.Threading.Tasks;
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
        const string ScenePath = "Assets/Tests/Performance/Runtime/10K/Scenes/Scene.unity";
        const int Warmup = 50;
        const int MeasureCount = 120;

        private NetworkManager benchmarker;

        [UnitySetUp]
        public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
        {
            // load scene
            await EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive });
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);

            // load host
            benchmarker = Object.FindObjectOfType<NetworkManager>();

            benchmarker.server.StartHost(benchmarker.client).Forget();

        });

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // shutdown
            benchmarker.server.StopHost();
            yield return null;

            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);

            Object.Destroy(benchmarker.gameObject);
        }

        static void EnableHealth(bool value)
        {
            Health[] all = Object.FindObjectsOfType<Health>();
            foreach (Health health in all)
            {
                health.enabled = value;
            }
        }

        [UnityTest]
        [Performance]
        public IEnumerator Benchmark10K()
        {
            EnableHealth(true);

            yield return Measure.Frames().MeasurementCount(MeasureCount).WarmupCount(Warmup).Run();
        }

        [UnityTest]
        [Performance]
        public IEnumerator Benchmark10KIdle()
        {
            EnableHealth(false);

            yield return Measure.Frames().MeasurementCount(MeasureCount).WarmupCount(Warmup).Run();
        }
    }
}

