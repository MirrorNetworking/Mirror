using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    [AddComponentMenu("")]
    public class MultiSceneNetManager : NetworkManager
    {
        [Header("MultiScene Setup")]
        public int instances = 3;

        [Scene]
        public string gameScene;

        readonly List<Scene> subScenes = new List<Scene>();

        #region Server System Callbacks

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            // This delay is really for the host player that loads too fast for the server to have subscene loaded
            StartCoroutine(AddPlayerDelayed(conn));
        }

        IEnumerator AddPlayerDelayed(NetworkConnection conn)
        {
            yield return new WaitForSeconds(.5f);
            conn.Send(new SceneMessage { sceneName = gameScene, sceneOperation = SceneOperation.LoadAdditive });

            base.OnServerAddPlayer(conn);

            PlayerScore playerScore = conn.identity.GetComponent<PlayerScore>();
            playerScore.playerNumber = conn.connectionId;
            playerScore.scoreIndex = conn.connectionId / subScenes.Count;
            playerScore.matchIndex = conn.connectionId % subScenes.Count;

            if (subScenes.Count > 0)
                SceneManager.MoveGameObjectToScene(conn.identity.gameObject, subScenes[conn.connectionId % subScenes.Count]);
        }

        #endregion

        #region Start & Stop Callbacks

        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartServer()
        {
            StartCoroutine(LoadSubScenes());
        }

        IEnumerator LoadSubScenes()
        {
            for (int index = 0; index < instances; index++)
            {
                yield return SceneManager.LoadSceneAsync(gameScene, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.Physics3D });
                subScenes.Add(SceneManager.GetSceneAt(index + 1));
            }
        }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public override void OnStopServer()
        {
            NetworkServer.SendToAll(new SceneMessage { sceneName = gameScene, sceneOperation = SceneOperation.UnloadAdditive });
            StartCoroutine(UnloadSubScenes());
        }

        public override void OnStopClient()
        {
            StartCoroutine(UnloadClientSubScenes());
        }

        IEnumerator UnloadClientSubScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index) != SceneManager.GetActiveScene())
                    yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(index));
            }
        }

        IEnumerator UnloadSubScenes()
        {
            for (int index = 0; index < subScenes.Count; index++)
                yield return SceneManager.UnloadSceneAsync(subScenes[index]);

            subScenes.Clear();

            yield return Resources.UnloadUnusedAssets();
        }

        #endregion
    }
}
