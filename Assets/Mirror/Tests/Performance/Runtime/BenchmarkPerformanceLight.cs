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
    public class BenchmarkPerformanceLight
    {
        const string ScenePath = "Assets/Mirror/Examples/Benchmarks/10klight/Scenes/Scene.unity";

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
            _ = benchmarker.StartHost();

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // shutdown
            benchmarker.StopHost();
            yield return null;

            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);

            GameObject.Destroy(benchmarker.gameObject);
        }

        [UnityTest]
        [Performance]
        public IEnumerator Benchmark10kLight()
        { 
            yield return Measure.Frames().MeasurementCount(120).WarmupCount(10).Run();
        }

    }
}

