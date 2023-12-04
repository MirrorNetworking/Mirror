using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.AdditiveLevels
{
    [AddComponentMenu("")]
    public class AdditiveLevelsNetworkManager : NetworkManager
    {
        public static new AdditiveLevelsNetworkManager singleton { get; private set; }

        /// <summary>
        /// Runs on both Server and Client
        /// Networking is NOT initialized when this fires
        /// </summary>
        public override void Awake()
        {
            base.Awake();
            singleton = this;
        }

        [Header("Additive Scenes - First is start scene")]

        [Scene, Tooltip("Add additive scenes here.\nFirst entry will be players' start scene")]
        public string[] additiveScenes;

        [Header("Fade Control - See child FadeCanvas")]

        [Tooltip("Reference to FadeInOut script on child FadeCanvas")]
        public FadeInOut fadeInOut;

        // This is set true after server loads all subscene instances
        bool subscenesLoaded;

        // This is managed in LoadAdditive, UnloadAdditive, and checked in OnClientSceneChanged
        bool isInTransition;

        #region Scene Management

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ServerChangeScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        public override void OnServerSceneChanged(string sceneName)
        {
            // This fires after server fully changes scenes, e.g. offline to online
            // If server has just loaded the Container (online) scene, load the subscenes on server
            if (sceneName == onlineScene)
                StartCoroutine(ServerLoadSubScenes());
        }

        IEnumerator ServerLoadSubScenes()
        {
            foreach (string additiveScene in additiveScenes)
                yield return SceneManager.LoadSceneAsync(additiveScene, new LoadSceneParameters
                {
                    loadSceneMode = LoadSceneMode.Additive,
                    localPhysicsMode = LocalPhysicsMode.Physics3D // change this to .Physics2D for a 2D game
                });

            subscenesLoaded = true;
        }

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="sceneName">Name of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        /// <param name="customHandling">true to indicate that scene loading will be handled through overrides</param>
        public override void OnClientChangeScene(string sceneName, SceneOperation sceneOperation, bool customHandling)
        {
            //Debug.Log($"{System.DateTime.Now:HH:mm:ss:fff} OnClientChangeScene {sceneName} {sceneOperation}");

            if (sceneOperation == SceneOperation.UnloadAdditive)
                StartCoroutine(UnloadAdditive(sceneName));

            if (sceneOperation == SceneOperation.LoadAdditive)
                StartCoroutine(LoadAdditive(sceneName));
        }

        IEnumerator LoadAdditive(string sceneName)
        {
            isInTransition = true;

            // This will return immediately if already faded in
            // e.g. by UnloadAdditive or by default startup state
            yield return fadeInOut.FadeIn();

            // host client is on server...don't load the additive scene again
            if (mode == NetworkManagerMode.ClientOnly)
            {
                // Start loading the additive subscene
                loadingSceneAsync = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                while (loadingSceneAsync != null && !loadingSceneAsync.isDone)
                    yield return null;
            }

            // Reset these to false when ready to proceed
            NetworkClient.isLoadingScene = false;
            isInTransition = false;

            OnClientSceneChanged();

            // Reveal the new scene content.
            yield return fadeInOut.FadeOut();
        }

        IEnumerator UnloadAdditive(string sceneName)
        {
            isInTransition = true;

            // This will return immediately if already faded in
            // e.g. by LoadAdditive above or by default startup state.
            yield return fadeInOut.FadeIn();

            // host client is on server...don't unload the additive scene here.
            if (mode == NetworkManagerMode.ClientOnly)
            {
                yield return SceneManager.UnloadSceneAsync(sceneName);
                yield return Resources.UnloadUnusedAssets();
            }

            // Reset these to false when ready to proceed
            NetworkClient.isLoadingScene = false;
            isInTransition = false;

            OnClientSceneChanged();

            // There is no call to FadeOut here on purpose.
            // Expectation is that a LoadAdditive or full scene change
            // will follow that will call FadeOut after that scene loads.
        }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        /// <param name="conn">The network connection that the scene change message arrived on.</param>
        public override void OnClientSceneChanged()
        {
            // Only call the base method if not in a transition.
            // This will be called from LoadAdditive / UnloadAdditive after setting isInTransition to false
            // but will also be called first by Mirror when the scene loading finishes.
            if (!isInTransition)
                base.OnClientSceneChanged();
        }

        #endregion

        #region Server System Callbacks

        /// <summary>
        /// Called on the server when a client is ready.
        /// <para>The default implementation of this function calls NetworkServer.SetClientReady() to continue the network setup process.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            // This fires from a Ready message client sends to server after loading the online scene
            base.OnServerReady(conn);

            if (conn.identity == null)
                StartCoroutine(AddPlayerDelayed(conn));
        }

        // This delay is mostly for the host player that loads too fast for the
        // server to have subscenes async loaded from OnServerSceneChanged ahead of it.
        IEnumerator AddPlayerDelayed(NetworkConnectionToClient conn)
        {
            // Wait for server to async load all subscenes for game instances
            while (!subscenesLoaded)
                yield return null;

            // Send Scene msg to client telling it to load the first additive scene
            conn.Send(new SceneMessage { sceneName = additiveScenes[0], sceneOperation = SceneOperation.LoadAdditive, customHandling = true });

            // We have Network Start Positions in first additive scene...pick one
            Transform start = GetStartPosition();

            // Instantiate player as child of start position - this will place it in the additive scene
            // This also lets player object "inherit" pos and rot from start position transform
            GameObject player = Instantiate(playerPrefab, start);
            // now set parent null to get it out from under the Start Position object
            player.transform.SetParent(null);

            // Wait for end of frame before adding the player to ensure Scene Message goes first
            yield return new WaitForEndOfFrame();

            // Finally spawn the player object for this connection
            NetworkServer.AddPlayerForConnection(conn, player);
        }

        #endregion
    }
}
