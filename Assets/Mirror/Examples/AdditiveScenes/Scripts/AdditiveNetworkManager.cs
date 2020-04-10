using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Additive
{
    [AddComponentMenu("")]
    public class AdditiveNetworkManager : NetworkManager
    {
        [Scene]
        [Tooltip("Add all sub-scenes to this list")]
        public string[] subScenes;

        void Awake()
        {
            server.Started.AddListener(Started);
            server.Stopped.AddListener(Stopped);
            client.Disconnected.AddListener(Disconnected);
        }

        public void Started()
        {
            // load all subscenes on the server only
            StartCoroutine(LoadSubScenes());
        }

        IEnumerator LoadSubScenes()
        {
            if (LogFilter.Debug) Debug.Log("Loading Scenes");

            foreach (string sceneName in subScenes)
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (LogFilter.Debug) Debug.Log($"Loaded {sceneName}");
            }
        }

        public void Stopped()
        {
            StartCoroutine(UnloadScenes());
        }

        public void Disconnected()
        {
            StartCoroutine(UnloadScenes());
        }

        IEnumerator UnloadScenes()
        {
            if (LogFilter.Debug) Debug.Log("Unloading Subscenes");

            foreach (string sceneName in subScenes)
                if (SceneManager.GetSceneByName(sceneName).IsValid() || SceneManager.GetSceneByPath(sceneName).IsValid())
                {
                    yield return SceneManager.UnloadSceneAsync(sceneName);
                    if (LogFilter.Debug) Debug.Log($"Unloaded {sceneName}");
                }

            yield return Resources.UnloadUnusedAssets();
        }
    }
}
