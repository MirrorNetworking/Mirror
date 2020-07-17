using System;
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
        [Serializable] public class NetworkSceneEvent : UnityEvent<string> { }

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
        public NetworkConnectionEvent ClientSceneChanged = new NetworkConnectionEvent();

        /// <summary>
        /// Event fires before Server changes scene.
        /// </summary>
        public NetworkSceneEvent ServerChangeScene = new NetworkSceneEvent();

        /// <summary>
        /// Event fires after Server has completed scene change.
        /// </summary>
        public NetworkSceneEvent ServerSceneChanged = new NetworkSceneEvent();

        /// <summary>
        /// The name of the current network scene.
        /// </summary>
        /// <remarks>
        /// <para>This should not be changed directly. Calls to ServerChangeScene() cause this to change. New clients that connect to a server will automatically load this scene.</para>
        /// <para>This is used to make sure that all scene changes are initialized by Mirror.</para>
        /// <para>Loading a scene manually wont set networkSceneName, so Mirror would still load it again on start.</para>
        /// </remarks>
        public string networkSceneName = "";

        [NonSerialized]
        public AsyncOperation loadingSceneAsync;

        /// <summary>
        /// Returns true when a client's connection has been set to ready.
        /// <para>A client that is ready recieves state updates from the server, while a client that is not ready does not. This useful when the state of the game is not normal, such as a scene change or end-of-game.</para>
        /// <para>This is read-only. To change the ready state of a client, use ClientScene.Ready(). The server is able to set the ready state of clients using NetworkServer.SetClientReady(), NetworkServer.SetClientNotReady() and NetworkServer.SetAllClientsNotReady().</para>
        /// <para>This is done when changing scenes so that clients don't receive state update messages during scene loading.</para>
        /// </summary>
        public bool Ready { get; internal set; }

        public void Start()
        {
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;

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

        public virtual void LateUpdate()
        {
            UpdateScene();
        }

        void UpdateScene()
        {
            if (loadingSceneAsync != null && loadingSceneAsync.isDone)
            {
                FinishLoadScene();
                loadingSceneAsync.allowSceneActivation = true;
                loadingSceneAsync = null;
            }
        }

        void RegisterClientMessages(INetworkConnection connection)
        {
            connection.RegisterHandler<NotReadyMessage>(ClientNotReadyMessage);
            connection.RegisterHandler<SceneMessage>(ClientSceneMessage);
        }

        // support additive scene loads:
        //   * ClientScene.PrepareToSpawnSceneObjects enables them again on the
        //     client after the server sends ObjectSpawnStartedMessage to client
        //     in SpawnObserversForConnection. This is only called when the
        //     client joins, so we need to rebuild scene objects manually again.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                if(client.Active)
                {
                    client.PrepareToSpawnSceneObjects();
                    if (logger.LogEnabled()) logger.Log("Rebuild Client spawnableObjects after additive scene load: " + scene.name);
                }

                if (server.Active)
                {
                    server.SpawnObjects();
                    if (logger.LogEnabled()) logger.Log("Respawned Server objects after additive scene load: " + scene.name);
                }
            }
        }

        internal void FinishLoadScene()
        {
            // host mode?
            if (client && client.IsLocalClient)
            {
                FinishLoadSceneHost();
            }
            // server-only mode?
            else if (server && server.Active)
            {
                FinishLoadSceneServerOnly();
            }
            // client-only mode?
            else if (client && client.Active)
            {
                FinishLoadSceneClientOnly();
            }
        }

        void FinishStartHost()
        {
            // server scene was loaded. now spawn all the objects
            server.SpawnObjects();

            // DO NOT do this earlier. it would cause race conditions where a
            // client will do things before the server is even fully started.
            logger.Log("FinishStartHost called");

            server.ActivateHostScene();
        }

        void FinishLoadSceneHost()
        {
            logger.Log("Finished loading scene in host mode.");

            if (client.Connection != null)
            {
                client.OnAuthenticated(client.Connection);
            }

            FinishStartHost();

            // call OnServerSceneChanged
            OnServerSceneChanged(networkSceneName);

            if (client.IsConnected)
            {
                // let client know that we changed scene
                OnClientSceneChanged(client.Connection);
            }
        }

        void FinishLoadSceneClientOnly()
        {
            logger.Log("Finished loading scene in client-only mode.");

            if (client.Connection != null)
            {
                client.OnAuthenticated(client.Connection);
            }

            if (client.IsConnected)
            {
                OnClientSceneChanged(client.Connection);
            }
        }

        // finish load scene part for server-only. . makes code easier and is
        // necessary for FinishStartServer later.
        void FinishLoadSceneServerOnly()
        {
            logger.Log("Finished loading scene in server-only mode.");

            server.SpawnObjects();
            OnServerSceneChanged(networkSceneName);
        }

        #region Client

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
            Ready = false;
            client.Authenticated.RemoveListener(OnClientAuthenticated);
            client.Disconnected.RemoveListener(OnClientDisconnected);
        }

        internal void ClientSceneMessage(INetworkConnection conn, SceneMessage msg)
        {
            if (!client.IsConnected)
            {
                throw new InvalidOperationException("ClientSceneMessage: cannot change network scene while client is disconnected");
            }

            if (server && server.LocalClient)
            {
                throw new InvalidOperationException("ClientSceneMessage: cannot change client network scene while operating in host mode");
            }

            if (string.IsNullOrEmpty(msg.sceneName))
            {
                throw new ArgumentNullException(msg.sceneName, "ClientSceneMessage: " + msg.sceneName + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ClientSceneMessage: changing scenes from: " + networkSceneName + " to:" + msg.sceneName);

            // Let client prepare for scene change
            OnClientChangeScene(msg.sceneName, msg.sceneOperation);

            ApplySceneOperation(msg.sceneName, msg.sceneOperation);  
        }

        internal void ClientNotReadyMessage(INetworkConnection conn, NotReadyMessage msg)
        {
            logger.Log("NetworkSceneManager.OnClientNotReadyMessageInternal");

            Ready = false;
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
        internal void OnClientSceneChanged(INetworkConnection conn)
        {
            //set ready after scene change has completed
            if (!Ready)
                SetClientReady(conn);

            ClientSceneChanged.Invoke(conn);
        }

        /// <summary>
        /// Called on clients when a servers tells the client it is no longer ready.
        /// <para>This is commonly used when switching scenes.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        internal void OnClientNotReady(INetworkConnection conn)
        {
            ClientNotReady.Invoke(conn);
        }

        /// <summary>
        /// Signal that the client connection is ready to enter the game.
        /// <para>This could be for example when a client enters an ongoing game and has finished loading the current scene. The server should respond to the message with an appropriate handler which instantiates the players object for example.</para>
        /// </summary>
        /// <param name="conn">The client connection which is ready.</param>
        public void SetClientReady(INetworkConnection conn)
        {
            if (Ready)
            {
                throw new InvalidOperationException("A connection has already been set as ready. There can only be one.");
            }

            if (conn == null)
                throw new InvalidOperationException("Ready() called with invalid connection object: conn=null");

            if (logger.LogEnabled()) logger.Log("ClientScene.Ready() called with connection [" + conn + "]");

            // Set these before sending the ReadyMessage, otherwise host client
            // will fail in InternalAddPlayer with null readyConnection.
            Ready = true;
            client.Connection.IsReady = true;

            // Tell server we're ready to have a player object spawned
            conn.Send(new ReadyMessage());
        }

        #endregion

        #region Server

        // called after successful authentication
        void OnServerAuthenticated(INetworkConnection conn)
        {
            logger.Log("NetworkSceneManager.OnServerAuthenticated");

            // proceed with the login handshake by calling OnServerConnect
            if (!string.IsNullOrEmpty(networkSceneName))
            {
                var msg = new SceneMessage { sceneName = networkSceneName };
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
            if(sceneOperation == SceneOperation.Normal)
                OnServerChangeScene(newSceneName);

            ApplySceneOperation(newSceneName, sceneOperation);

            // notify all clients about the new scene
            server.SendToAll(new SceneMessage { sceneName = newSceneName, sceneOperation = sceneOperation });
        }

        /// <summary>
        /// Called from ChangeServerScene immediately before NetworkSceneManager's LoadSceneAsync is executed
        /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        internal void OnServerChangeScene(string newSceneName)
        {
            ServerChangeScene.Invoke(newSceneName);
        }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ChangeServerScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        internal void OnServerSceneChanged(string sceneName)
        {
            ServerSceneChanged.Invoke(sceneName);
        }

        #endregion

        void ApplySceneOperation(string sceneName, SceneOperation sceneOperation = SceneOperation.Normal)
        {
            switch (sceneOperation)
            {
                case SceneOperation.Normal:
                    networkSceneName = sceneName;
                    loadingSceneAsync = SceneManager.LoadSceneAsync(sceneName);
                    break;
                case SceneOperation.LoadAdditive:
                    // Ensure additive scene is not already loaded since we don't know which was passed in the Scene message
                    if (!SceneManager.GetSceneByName(sceneName).IsValid() && !SceneManager.GetSceneByPath(sceneName).IsValid())
                        loadingSceneAsync = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    else
                    {
                        logger.LogWarning($"Scene {sceneName} is already loaded");
                    }
                    break;
                case SceneOperation.UnloadAdditive:
                    // Ensure additive scene is actually loaded since we don't know which was passed in the Scene message
                    if (SceneManager.GetSceneByName(sceneName).IsValid() || SceneManager.GetSceneByPath(sceneName).IsValid())
                        loadingSceneAsync = SceneManager.UnloadSceneAsync(sceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    else
                    {
                        logger.LogWarning($"Cannot unload {sceneName} with UnloadAdditive operation");
                    }
                    break;
            }
        }
    }
}
