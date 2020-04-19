using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Additive
{
    [AddComponentMenu("")]
    public class AdditiveNetworkManager : NetworkManager
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(AdditiveNetworkManager));

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
            logger.Log("Loading Scenes");

            foreach (string sceneName in subScenes)
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (logger.LogEnabled()) logger.Log($"Loaded {sceneName}");
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
            logger.Log("Unloading Subscenes");

            foreach (string sceneName in subScenes)
                if (SceneManager.GetSceneByName(sceneName).IsValid() || SceneManager.GetSceneByPath(sceneName).IsValid())
                {
                    yield return SceneManager.UnloadSceneAsync(sceneName);
                    if (logger.LogEnabled()) logger.Log($"Unloaded {sceneName}");
                }

            yield return Resources.UnloadUnusedAssets();
        }
    }
}
