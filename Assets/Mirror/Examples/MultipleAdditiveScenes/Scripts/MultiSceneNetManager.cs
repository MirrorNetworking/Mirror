using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    [AddComponentMenu("")]
    public class MultiSceneNetManager : NetworkManager
    {
        [Header("Spawner Setup")]
        [Tooltip("Reward Prefab for the Spawner")]
        public GameObject rewardPrefab;
        public byte poolSize = 20;

        [Header("MultiScene Setup")]
        public int instances = 3;

        [Scene]
        public string gameScene;

        // This is set true after server loads all subscene instances
        bool subscenesLoaded;

        // subscenes are added to this list as they're loaded
        readonly List<Scene> subScenes = new List<Scene>();

        // Sequential index used in round-robin deployment of players into instances and score positioning
        int clientIndex;

        #region Server System Callbacks

        /// <summary>
        /// Called on the server when a client adds a new player with NetworkClient.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            StartCoroutine(OnServerAddPlayerDelayed(conn));
        }

        // This delay is mostly for the host player that loads too fast for the
        // server to have subscenes async loaded from OnStartServer ahead of it.
        IEnumerator OnServerAddPlayerDelayed(NetworkConnectionToClient conn)
        {
            // wait for server to async load all subscenes for game instances
            while (!subscenesLoaded)
                yield return null;

            // Send Scene message to client to additively load the game scene
            conn.Send(new SceneMessage { sceneName = gameScene, sceneOperation = SceneOperation.LoadAdditive });

            // Wait for end of frame before adding the player to ensure Scene Message goes first
            yield return new WaitForEndOfFrame();

            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            // instantiating a "Player" prefab gives it the name "Player(clone)"
            // => appending the connectionId is WAY more useful for debugging!
            player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";

            PlayerScore playerScore = player.GetComponent<PlayerScore>();
            playerScore.playerNumber = clientIndex;
            playerScore.scoreIndex = clientIndex / subScenes.Count;
            playerScore.matchIndex = clientIndex % subScenes.Count;

            // Do this only on server, not on clients
            // This is what allows Scene Interest Management
            // to isolate matches per scene instance on server.
            if (subScenes.Count > 0)
                SceneManager.MoveGameObjectToScene(player, subScenes[clientIndex % subScenes.Count]);

            NetworkServer.AddPlayerForConnection(conn, player);
            clientIndex++;
        }

        #endregion

        #region Start & Stop Callbacks

        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartServer()
        {
            StartCoroutine(ServerLoadSubScenes());
        }

        // We're additively loading scenes, so GetSceneAt(0) will return the main "container" scene,
        // therefore we start the index at one and loop through instances value inclusively.
        // If instances is zero, the loop is bypassed entirely.
        IEnumerator ServerLoadSubScenes()
        {
            for (int index = 1; index <= instances; index++)
            {
                yield return SceneManager.LoadSceneAsync(gameScene, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.Physics3D });

                Scene newScene = SceneManager.GetSceneAt(index);
                subScenes.Add(newScene);
            }

            Spawner.InitializePool(rewardPrefab, poolSize);

            foreach (Scene scene in subScenes)
                if (scene.IsValid())
                    Spawner.InitialSpawn(scene);

            subscenesLoaded = true;
        }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public override void OnStopServer()
        {
            NetworkServer.SendToAll(new SceneMessage { sceneName = gameScene, sceneOperation = SceneOperation.UnloadAdditive });
            
            if (gameObject.activeSelf) 
                StartCoroutine(ServerUnloadSubScenes());

            Spawner.ClearPool();
            clientIndex = 0;
        }

        // Unload the subScenes and unused assets and clear the subScenes list.
        IEnumerator ServerUnloadSubScenes()
        {
            for (int index = 0; index < subScenes.Count; index++)
                if (subScenes[index].IsValid())
                    yield return SceneManager.UnloadSceneAsync(subScenes[index]);

            subScenes.Clear();
            subscenesLoaded = false;

            yield return Resources.UnloadUnusedAssets();
        }

        public override void OnClientSceneChanged()
        {
            // Don't initialize the pool for host client because it's
            // already initialized in OnRoomServerSceneChanged
            if (!NetworkServer.active && SceneManager.sceneCount > 1)
                Spawner.InitializePool(rewardPrefab, poolSize);

            base.OnClientSceneChanged();
        }

        // Unload all but the active scene, which is the "container" scene
        IEnumerator ClientUnloadSubScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index) != SceneManager.GetActiveScene())
                    yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(index));
        }

        /// <summary>
        /// This is called when a client is stopped.
        /// </summary>
        public override void OnStopClient()
        {
            // Clear the pool when stopping client
            // Only do this if we're not the host client because
            // pool needs to remain active for remote clients
            if (!NetworkServer.active)
                Spawner.ClearPool();

            // Make sure we're not in ServerOnly mode now after stopping host client
            if (mode == NetworkManagerMode.Offline)
                if (gameObject.activeSelf) StartCoroutine(ClientUnloadSubScenes());
        }

        #endregion
    }
}
