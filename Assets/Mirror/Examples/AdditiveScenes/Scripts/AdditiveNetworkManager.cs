using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Additive
{
    [AddComponentMenu("")]
    public class AdditiveNetworkManager : NetworkManager
    {
        [Tooltip("Add all sub-scenes to this list")]
        public SceneField[] subScenes;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // load all subscenes on the server only
            StartCoroutine(LoadSubScenes());
        }

        IEnumerator LoadSubScenes()
        {
            if (LogFilter.Debug) Debug.Log("Loading Scenes");

            foreach (SceneField scene in subScenes)
            {
                if (scene.HasValue())
                {
                    yield return SceneManager.LoadSceneAsync(scene.Path, LoadSceneMode.Additive);
                    if (LogFilter.Debug) Debug.Log($"Loaded {scene.Path}");
                }
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
            if (LogFilter.Debug) Debug.Log("Unloading Subscenes");

            foreach (SceneField scene in subScenes)
            {
                if (SceneManager.GetSceneByPath(scene.Path).IsValid())
                {
                    yield return SceneManager.UnloadSceneAsync(scene.Path);
                    if (LogFilter.Debug) Debug.Log($"Unloaded {scene.Path}");
                }
            }

            yield return Resources.UnloadUnusedAssets();
        }
    }
}
