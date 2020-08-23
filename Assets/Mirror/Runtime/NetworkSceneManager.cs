using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Mirror
{
    /// <summary>
    /// Provides Scene Management to a NetworkServer and or NetworkClient.
    /// <para>The <see cref="NetworkClient">NetworkClient</see> loads scenes as instructed by the <see cref="NetworkServer">NetworkServer</see>.</para>
    /// <para>The <see cref="NetworkServer">NetworkServer</see> controls the currently active Scene and any additive Load/Unload.</para>
    /// </summary>
    [AddComponentMenu("Network/NetworkSceneManager")]
    [DisallowMultipleComponent]
    public class NetworkSceneManager : MonoBehaviour, INetworkSceneManager
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkSceneManager));

        [Serializable] public class ClientSceneChangeEvent : UnityEvent<string, SceneOperation> { }

        public NetworkClient client;
        public NetworkServer server;

        public NetworkConnectionEvent ClientNotReady = new NetworkConnectionEvent();

        /// <summary>
        /// Event fires when the Client starts changing scene.
        /// </summary>
        public ClientSceneChangeEvent ClientChangeScene = new ClientSceneChangeEvent();

        /// <summary>
        /// Event fires after the Client has completed its scene change.
        /// </summary>
        public ClientSceneChangeEvent ClientSceneChanged = new ClientSceneChangeEvent();

        /// <summary>
        /// Event fires before Server changes scene.
        /// </summary>
        public ClientSceneChangeEvent ServerChangeScene = new ClientSceneChangeEvent();

        /// <summary>
        /// Event fires after Server has completed scene change.
        /// </summary>
        public ClientSceneChangeEvent ServerSceneChanged = new ClientSceneChangeEvent();

        /// <summary>
        /// The name of the current network scene.
        /// </summary>
        /// <remarks>
        /// <para>This should not be changed directly. Calls to ServerChangeScene() cause this to change. New clients that connect to a server will automatically load this scene.</para>
        /// <para>This is used to make sure that all scene changes are initialized by Mirror.</para>
        /// <para>Loading a scene manually wont set networkSceneName, so Mirror would still load it again on start.</para>
        /// </remarks>
        public string NetworkSceneName { get; protected set; } = "";

        public void Start()
        {
            DontDestroyOnLoad(gameObject);

            if (client != null)
            {
                client.Authenticated.AddListener(OnClientAuthenticated);
                client.Disconnected.AddListener(OnClientDisconnected);
            }
            if (server != null)
            {
                server.Authenticated.AddListener(OnServerAuthenticated);
            }
        }

        public virtual void LateUpdate() { } //TODO: Remove/move this as its only used in BenchmarkNetworkManager

        #region Client

        void RegisterClientMessages(INetworkConnection connection)
        {
            connection.RegisterHandler<NotReadyMessage>(ClientNotReadyMessage);
            connection.RegisterHandler<SceneMessage>(ClientSceneMessage);
        }

        // called after successful authentication
        void OnClientAuthenticated(INetworkConnection conn)
        {
            //Dont register msg handlers in host mode
            if (!client.IsLocalClient)
                RegisterClientMessages(conn);

            logger.Log("NetworkSceneManager.OnClientAuthenticated");
        }

        void OnClientDisconnected()
        {
            client.Authenticated.RemoveListener(OnClientAuthenticated);
            client.Disconnected.RemoveListener(OnClientDisconnected);
        }

        internal void ClientSceneMessage(INetworkConnection conn, SceneMessage msg)
        {
            if (!client.IsConnected)
            {
                throw new InvalidOperationException("ClientSceneMessage: cannot change network scene while client is disconnected");
            }

            if (client.IsLocalClient)
            {
                throw new InvalidOperationException("ClientSceneMessage: cannot change client network scene while operating in host mode");
            }

            if (string.IsNullOrEmpty(msg.sceneName))
            {
                throw new ArgumentNullException(msg.sceneName, "ClientSceneMessage: " + msg.sceneName + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ClientSceneMessage: changing scenes from: " + NetworkSceneName + " to:" + msg.sceneName);

            // Let client prepare for scene change
            OnClientChangeScene(msg.sceneName, msg.sceneOperation);

            StartCoroutine(ApplySceneOperation(msg.sceneName, msg.sceneOperation));
        }

        internal void ClientNotReadyMessage(INetworkConnection conn, NotReadyMessage msg)
        {
            logger.Log("NetworkSceneManager.OnClientNotReadyMessageInternal");

            OnClientNotReady(conn);
        }

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        internal void OnClientChangeScene(string sceneName, SceneOperation sceneOperation)
        {
            ClientChangeScene.Invoke(sceneName, sceneOperation);
        }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Non-Additive Scene changes will cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkSceneManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        /// <param name="conn">The network connection that the scene change message arrived on.</param>
        internal void OnClientSceneChanged(string sceneName, SceneOperation sceneOperation)
        {
            //set ready after scene change has completed
            if (!client.Connection.IsReady)
                SetClientReady();

            ClientSceneChanged.Invoke(sceneName, sceneOperation);
        }

        /// <summary>
        /// Called on clients when a servers tells the client it is no longer ready.
        /// <para>This is commonly used when switching scenes.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        internal void OnClientNotReady(INetworkConnection conn)
        {
            client.Connection.IsReady = false;

            ClientNotReady.Invoke(conn);
        }

        /// <summary>
        /// Signal that the client connection is ready to enter the game.
        /// <para>This could be for example when a client enters an ongoing game and has finished loading the current scene. The server should respond to the message with an appropriate handler which instantiates the players object for example.</para>
        /// </summary>
        public void SetClientReady()
        {
            if (!client || !client.Active)
                throw new InvalidOperationException("Ready() called with an null or disconnected client");

            if (client.Connection.IsReady)
                throw new InvalidOperationException("A connection has already been set as ready. There can only be one.");

            if (logger.LogEnabled()) logger.Log("ClientScene.Ready() called.");

            // Set these before sending the ReadyMessage, otherwise host client
            // will fail in InternalAddPlayer with null readyConnection.
            client.Connection.IsReady = true;

            // Tell server we're ready to have a player object spawned
            client.Connection.Send(new ReadyMessage());
        }

        #endregion

        #region Server

        // called after successful authentication
        void OnServerAuthenticated(INetworkConnection conn)
        {
            logger.Log("NetworkSceneManager.OnServerAuthenticated");

            // proceed with the login handshake by calling OnServerConnect
            if (!string.IsNullOrEmpty(NetworkSceneName))
            {
                var msg = new SceneMessage { sceneName = NetworkSceneName };
                conn.Send(msg);
            }
        }

        /// <summary>
        /// This causes the server to switch scenes and sets the networkSceneName.
        /// <para>Clients that connect to this server will automatically switch to this scene. This automatically sets clients to be not-ready. The clients must call Ready() again to participate in the new scene.</para>
        /// </summary>
        /// <param name="newSceneName"></param>
        /// <param name="operation"></param>
        public void ChangeServerScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                throw new ArgumentNullException(nameof(newSceneName), "ServerChangeScene: " + nameof(newSceneName) + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ServerChangeScene " + newSceneName);
            server.SetAllClientsNotReady();

            // Let server prepare for scene change
            OnServerChangeScene(newSceneName, sceneOperation);

            StartCoroutine(ApplySceneOperation(newSceneName, sceneOperation));

            // notify all clients about the new scene
            server.SendToAll(new SceneMessage { sceneName = newSceneName, sceneOperation = sceneOperation });
        }

        /// <summary>
        /// Called from ChangeServerScene immediately before NetworkSceneManager's LoadSceneAsync is executed
        /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        internal void OnServerChangeScene(string newSceneName, SceneOperation operation)
        {
            ServerChangeScene.Invoke(newSceneName, operation);
        }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ChangeServerScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        internal void OnServerSceneChanged(string sceneName, SceneOperation operation)
        {
            ServerSceneChanged.Invoke(sceneName, operation);
        }

        #endregion

        IEnumerator ApplySceneOperation(string sceneName, SceneOperation sceneOperation = SceneOperation.Normal)
        {
            switch (sceneOperation)
            {
                case SceneOperation.Normal:
                    NetworkSceneName = sceneName;
                    yield return SceneManager.LoadSceneAsync(sceneName);
                    break;
                case SceneOperation.LoadAdditive:
                    // Ensure additive scene is not already loaded since we don't know which was passed in the Scene message
                    if (!SceneManager.GetSceneByName(sceneName).IsValid() && !SceneManager.GetSceneByPath(sceneName).IsValid())
                        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    else
                    {
                        logger.LogWarning($"Scene {sceneName} is already loaded");
                    }
                    break;
                case SceneOperation.UnloadAdditive:
                    // Ensure additive scene is actually loaded since we don't know which was passed in the Scene message
                    if (SceneManager.GetSceneByName(sceneName).IsValid() || SceneManager.GetSceneByPath(sceneName).IsValid())
                        yield return SceneManager.UnloadSceneAsync(sceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    else
                    {
                        logger.LogWarning($"Cannot unload {sceneName} with UnloadAdditive operation");
                    }
                    break;
            }

            FinishLoadScene(sceneName, sceneOperation);
        }

        internal void FinishLoadScene(string sceneName, SceneOperation sceneOperation)
        {
            // host mode?
            if (client && client.IsLocalClient)
            {
                logger.Log("Finished loading scene in host mode.");

                if (client.Connection != null && sceneOperation == SceneOperation.Normal)
                {
                    client.OnAuthenticated(client.Connection);
                }

                // server scene was loaded. now spawn all the objects
                server.ActivateHostScene();

                // call OnServerSceneChanged
                OnServerSceneChanged(sceneName, sceneOperation);

                if (client.IsConnected)
                {
                    // let client know that we changed scene
                    OnClientSceneChanged(sceneName, sceneOperation);
                }
            }
            // server-only mode?
            else if (server && server.Active)
            {
                logger.Log("Finished loading scene in server-only mode.");

                server.SpawnObjects();
                OnServerSceneChanged(sceneName, sceneOperation);
            }
            // client-only mode?
            else if (client && client.Active)
            {
                logger.Log("Finished loading scene in client-only mode.");

                if (client.Connection != null && sceneOperation == SceneOperation.Normal)
                {
                    client.OnAuthenticated(client.Connection);
                }

                OnClientSceneChanged(sceneName, sceneOperation);
            }
        }
    }
}
