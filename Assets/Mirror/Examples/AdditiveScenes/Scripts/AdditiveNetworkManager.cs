using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Additive
{
    public class AdditiveNetworkManager : NetworkManager
    {
        [Scene]
        [Tooltip("Add all sub-scenes to this list")]
        public string[] subScenes;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("Loading Scenes");

            // load all subscenes on the server only
            foreach (string sceneName in subScenes)
            {
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Debug.LogFormat("Loaded {0}", sceneName);
            }
        }

        public override void OnStopServer()
        {
            Debug.Log("Stopping Server");
            base.OnStopServer();
            UnloadScenes();
        }

        public override void OnStopClient()
        {
            Debug.Log("Stopping Client");
            base.OnStopClient();
            UnloadScenes();
        }

        void UnloadScenes()
        {
            Debug.Log("Unloading Scenes");
            foreach (string sceneName in subScenes)
                if (SceneManager.GetSceneByName(sceneName).IsValid())
                    StartCoroutine(UnloadScene(sceneName));
        }

        IEnumerator UnloadScene(string sceneName)
        {
            yield return SceneManager.UnloadSceneAsync(sceneName);
            yield return Resources.UnloadUnusedAssets();
            Debug.LogFormat("Unloaded {0}", sceneName);
        }
    }
}
