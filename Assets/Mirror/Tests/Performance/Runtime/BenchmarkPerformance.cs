using System.Collections;
using Mirror;
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

        [UnityTest]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceUnityTest]
#endif
        public IEnumerator Benchmark10k()
        {
            // load scene
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive });
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);

            // wait for NetworkManager awake
            yield return null;
            // load host
            NetworkManager.singleton.StartHost();

            // warmup
            yield return new WaitForSecondsRealtime(Warmup);

            // run benchmark
            yield return Measure.Frames().MeasurementCount(MeasureCount).Run();

            // unload scenne
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
