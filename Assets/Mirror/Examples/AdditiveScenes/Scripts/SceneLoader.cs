using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Additive
{
    // This script is attached to the player prefab
    public class SceneLoader : NetworkBehaviour
    {
        public enum LoadAction
        {
            Load,
            Unload
        }

        // Tell the client to load a single subscene
        // This is called from ZoneHandler's server-only OnTrigger events
        [TargetRpc]
        public void TargetLoadUnloadScene(NetworkConnection networkConnection, string SceneName, LoadAction loadAction)
        {
            // Check if server here because we already pre-loaded the subscenes on the server
            if (!isServer) StartCoroutine(LoadUnloadScene(SceneName, loadAction));
        }

        // isBusy protects us from being overwhelmed by server messages to load several subscenes at once.
        bool isBusy = false;

        IEnumerator LoadUnloadScene(string sceneName, LoadAction loadAction)
        {
            while (isBusy) yield return null;

            isBusy = true;

            if (loadAction == LoadAction.Load)
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            else
            {
                yield return SceneManager.UnloadSceneAsync(sceneName);
                yield return Resources.UnloadUnusedAssets();
            }

            isBusy = false;
            Debug.LogFormat("{0} {1} Done", sceneName, loadAction.ToString());

            CmdSceneDone(sceneName, loadAction);
        }

        [Command]
        public void CmdSceneDone(string sceneName, LoadAction loadAction)
        {
            // The point of this is to show the client telling server it has loaded the subscene
            // so the server might take some further action, e.g. reposition the player.
            Debug.LogFormat("{0} {1} done on client", sceneName, loadAction.ToString());
        }
    }
}
