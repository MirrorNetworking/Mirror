using System.Collections;
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
    public class BenchmarkPerformanceLight
    {
        const string ScenePath = "Assets/Examples/Benchmarks/10klight/Scenes/Scene.unity";

        private NetworkManager benchmarker;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // load scene
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive });
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);

            // wait for NetworkManager awake
            yield return null;
            // load host
            benchmarker = Object.FindObjectOfType<NetworkManager>();


            if (benchmarker == null)
            {
                Assert.Fail("Could not find Benchmarker");
                yield break;
            }

            System.Threading.Tasks.Task task = benchmarker.server.StartHost(benchmarker.client);

            while (!task.IsCompleted)
                yield return null;

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // shutdown
            benchmarker.server.StopHost();
            yield return null;

            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);

            GameObject.Destroy(benchmarker.gameObject);
        }

        [UnityTest]
        [Performance]
        public IEnumerator Benchmark10KLight()
        { 
            yield return Measure.Frames().MeasurementCount(240).WarmupCount(50).Run();
        }

    }
}

