using System;
using System.Collections;
using System.Collections.Generic;
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
        /// The path of the current network scene.
        /// </summary>
        /// <remarks>
        /// <para>New clients that connect to a server will automatically load this scene.</para>
        /// <para>This is used to make sure that all scene changes are initialized by Mirror.</para>
        /// </remarks>
        public string NetworkScenePath => SceneManager.GetActiveScene().path;

        internal AsyncOperation asyncOperation;

        //Used by the server to track all additive scenes. To notify clients upon connection
        internal List<string> additiveSceneList = new List<string>();
        //Used by the client to load the full additive scene list that the server has upon connection
        internal List<string> pendingAdditiveSceneList = new List<string>();

        public void Start()
        {
            DontDestroyOnLoad(gameObject);

            if (client != null)
            {
                client.Authenticated.AddListener(OnClientAuthenticated);
            }
            if (server != null)
            {
                server.Authenticated.AddListener(OnServerAuthenticated);
            }
        }

        #region Client

        void RegisterClientMessages(INetworkConnection connection)
        {
            connection.RegisterHandler<SceneMessage>(ClientSceneMessage);
            if (!client.IsLocalClient)
            {
                connection.RegisterHandler<SceneReadyMessage>(ClientSceneReadyMessage);
                connection.RegisterHandler<NotReadyMessage>(ClientNotReadyMessage);
            }
        }

        // called after successful authentication
        void OnClientAuthenticated(INetworkConnection conn)
        {
            logger.Log("NetworkSceneManager.OnClientAuthenticated");
            RegisterClientMessages(conn);
        }

        void OnDestroy()
        {
            client?.Authenticated?.RemoveListener(OnClientAuthenticated);
        }

        internal void ClientSceneMessage(INetworkConnection conn, SceneMessage msg)
        {
            if (!client.IsConnected)
            {
                throw new InvalidOperationException("ClientSceneMessage: cannot change network scene while client is disconnected");
            }
            if (string.IsNullOrEmpty(msg.scenePath))
            {
                throw new ArgumentNullException(msg.scenePath, "ClientSceneMessage: " + msg.scenePath + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ClientSceneMessage: changing scenes from: " + NetworkScenePath + " to:" + msg.scenePath);

            // Let client prepare for scene change
            OnClientChangeScene(msg.scenePath, msg.sceneOperation);

            //Additive are scenes loaded on server and this client is not a host client
            if(msg.additiveScenes != null && msg.additiveScenes.Length > 0 && client && !client.IsLocalClient)
            {
                foreach (string scene in msg.additiveScenes)
                {
                    pendingAdditiveSceneList.Add(scene);
                }
            }

            StartCoroutine(ApplySceneOperation(msg.scenePath, msg.sceneOperation));
        }

        internal void ClientSceneReadyMessage(INetworkConnection conn, SceneReadyMessage msg)
        {
            logger.Log("ClientSceneReadyMessage");

            //Server has finished changing scene. Allow the client to finish.
            if(asyncOperation != null)
                asyncOperation.allowSceneActivation = true;
        }

        internal void ClientNotReadyMessage(INetworkConnection conn, NotReadyMessage msg)
        {
            logger.Log("NetworkSceneManager.OnClientNotReadyMessageInternal");

            client.Connection.IsReady = false;
        }

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="scenePath">Path of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        internal void OnClientChangeScene(string scenePath, SceneOperation sceneOperation)
        {
            ClientChangeScene.Invoke(scenePath, sceneOperation);
        }

        /// <summary>
        /// Called on clients when a scene has completed loading, when the scene load was initiated by the server.
        /// <para>Non-Additive Scene changes will cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkSceneManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        /// <param name="scenePath">Path of the scene that was just loaded</param>
        /// <param name="sceneOperation">Scene operation that was just  happen</param>
        internal void OnClientSceneChanged(string scenePath, SceneOperation sceneOperation)
        {
            ClientSceneChanged.Invoke(scenePath, sceneOperation);

            if (pendingAdditiveSceneList.Count > 0 && client && !client.IsLocalClient)
            {
                StartCoroutine(ApplySceneOperation(pendingAdditiveSceneList[0], SceneOperation.LoadAdditive));
                pendingAdditiveSceneList.RemoveAt(0);
                return;
            }

            //set ready after scene change has completed
            if (!client.Connection.IsReady)
                SetClientReady();
        }

        /// <summary>
        /// Signal that the client connection is ready to enter the game.
        /// <para>This could be for example when a client enters an ongoing game and has finished loading the current scene. The server should respond to the message with an appropriate handler which instantiates the players object for example.</para>
        /// </summary>
        public void SetClientReady()
        {
            if (!client || !client.Active)
                throw new InvalidOperationException("Ready() called with an null or disconnected client");

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

            conn.Send(new SceneMessage { scenePath = NetworkScenePath, additiveScenes = additiveSceneList.ToArray() });
            conn.Send(new SceneReadyMessage());
        }

        /// <summary>
        /// This causes the server to switch scenes and sets the NetworkScenePath.
        /// <para>Clients that connect to this server will automatically switch to this scene. This automatically sets clients to be not-ready. The clients must call Ready() again to participate in the new scene.</para>
        /// </summary>
        /// <param name="scenePath"></param>
        /// <param name="operation"></param>
        public void ChangeServerScene(string scenePath, SceneOperation sceneOperation = SceneOperation.Normal)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new ArgumentNullException(nameof(scenePath), "ServerChangeScene: " + nameof(scenePath) + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ServerChangeScene " + scenePath);

            // Let server prepare for scene change
            OnServerChangeScene(scenePath, sceneOperation);

            if(!server.LocalClientActive)
                StartCoroutine(ApplySceneOperation(scenePath, sceneOperation));

            // notify all clients about the new scene
            server.SendToAll(new SceneMessage { scenePath = scenePath, sceneOperation = sceneOperation });
        }

        /// <summary>
        /// Called from ChangeServerScene immediately before NetworkSceneManager's LoadSceneAsync is executed
        /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="scenePath">Path of the scene that's about to be loaded</param>
        internal void OnServerChangeScene(string scenePath, SceneOperation operation)
        {
            logger.Log("OnServerChangeScene");

            ServerChangeScene.Invoke(scenePath, operation);
        }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ChangeServerScene().
        /// </summary>
        /// <param name="scenePath">The name of the new scene.</param>
        internal void OnServerSceneChanged(string scenePath, SceneOperation operation)
        {
            logger.Log("OnServerSceneChanged");

            server.SendToAll(new SceneReadyMessage());

            ServerSceneChanged.Invoke(scenePath, operation);
        }

        #endregion

        IEnumerator ApplySceneOperation(string scenePath, SceneOperation sceneOperation = SceneOperation.Normal)
        {
            switch (sceneOperation)
            {
                case SceneOperation.Normal:
                    //Scene is already active.
                    if (NetworkScenePath.Equals(scenePath))
                    {
                        FinishLoadScene(scenePath, sceneOperation);
                    }
                    else
                    {
                        asyncOperation = SceneManager.LoadSceneAsync(scenePath);
                        asyncOperation.completed += OnAsyncComplete;

                        //If non host client. Wait for server to finish scene change
                        if (client && client.Active && !client.IsLocalClient)
                        {
                            asyncOperation.allowSceneActivation = false;
                        }

                        yield return asyncOperation;
                    }
                    
                    break;
                case SceneOperation.LoadAdditive:
                    // Ensure additive scene is not already loaded
                    if (!SceneManager.GetSceneByPath(scenePath).IsValid())
                    {
                        yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                        additiveSceneList.Add(scenePath);
                        FinishLoadScene(scenePath, sceneOperation);
                    }   
                    else
                    {
                        logger.LogWarning($"Scene {scenePath} is already loaded");
                    }
                    break;
                case SceneOperation.UnloadAdditive:
                    // Ensure additive scene is actually loaded
                    if (SceneManager.GetSceneByPath(scenePath).IsValid())
                    {
                        yield return SceneManager.UnloadSceneAsync(scenePath, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                        additiveSceneList.Remove(scenePath);
                        FinishLoadScene(scenePath, sceneOperation);
                    }
                    else
                    {
                        logger.LogWarning($"Cannot unload {scenePath} with UnloadAdditive operation");
                    }
                    break;
            }
        }

        void OnAsyncComplete(AsyncOperation asyncOperation)
        {
            //This is only called in a normal scene change
            FinishLoadScene(NetworkScenePath, SceneOperation.Normal);
        }

        internal void FinishLoadScene(string scenePath, SceneOperation sceneOperation)
        {
            // host mode?
            if (client && client.IsLocalClient)
            {
                if (logger.LogEnabled()) logger.Log("Host: " + sceneOperation.ToString() + " operation for scene: " + scenePath);

                // call OnServerSceneChanged
                OnServerSceneChanged(scenePath, sceneOperation);

                if (client.IsConnected)
                {
                    // let client know that we changed scene
                    OnClientSceneChanged(scenePath, sceneOperation);
                }
            }
            // server-only mode?
            else if (server && server.Active)
            {
                if (logger.LogEnabled()) logger.Log("Server: " + sceneOperation.ToString() + " operation for scene: " + scenePath);

                OnServerSceneChanged(scenePath, sceneOperation);
            }
            // client-only mode?
            else if (client && client.Active && !client.IsLocalClient)
            {
                if (logger.LogEnabled()) logger.Log("Client: " + sceneOperation.ToString() + " operation for scene: " + scenePath);

                OnClientSceneChanged(scenePath, sceneOperation);
            }
        }
    }
}
