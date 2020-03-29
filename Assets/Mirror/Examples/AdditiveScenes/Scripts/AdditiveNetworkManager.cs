using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Additive
{
    [AddComponentMenu("")]
    public class AdditiveNetworkManager : NetworkManager
    {
        [Scene]
        [Tooltip("Add all sub-scenes to this list")]
        public string[] subScenes;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // load all subscenes on the server only
            StartCoroutine(LoadSubScenes());
        }

        IEnumerator LoadSubScenes()
        {
            if (LogFilter.Debug) MirrorLog.DebugLog("Loading Scenes");

            foreach (string sceneName in subScenes)
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (LogFilter.Debug) MirrorLog.DebugLog($"Loaded {sceneName}");
            }
        }

        public override void OnStopServer()
        {
            StartCoroutine(UnloadScenes());
        }

        public override void OnStopClient()
        {
            StartCoroutine(UnloadScenes());
        }

        IEnumerator UnloadScenes()
        {
            if (LogFilter.Debug) MirrorLog.DebugLog("Unloading Subscenes");

            foreach (string sceneName in subScenes)
                if (SceneManager.GetSceneByName(sceneName).IsValid())
                {
                    yield return SceneManager.UnloadSceneAsync(sceneName);
                    if (LogFilter.Debug) MirrorLog.DebugLog($"Unloaded {sceneName}");
                }

            yield return Resources.UnloadUnusedAssets();
        }
    }
}
