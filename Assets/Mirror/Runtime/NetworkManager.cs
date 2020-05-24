using System;
using System.Threading.Tasks;
using Mirror.AsyncTcp;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{

    [AddComponentMenu("Network/NetworkManager")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkManager.html")]
    [RequireComponent(typeof(NetworkServer))]
    [RequireComponent(typeof(NetworkClient))]
    [DisallowMultipleComponent]
    public class NetworkManager : MonoBehaviour, INetworkManager
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkManager));

        /// <summary>
        /// A flag to control whether the NetworkManager object is destroyed when the scene changes.
        /// <para>This should be set if your game has a single NetworkManager that exists for the lifetime of the process. If there is a NetworkManager in each scene, then this should not be set.</para>
        /// </summary>
        [Header("Configuration")]
        [FormerlySerializedAs("m_DontDestroyOnLoad")]
        [Tooltip("Should the Network Manager object be persisted through scene changes?")]
        public bool dontDestroyOnLoad = true;

        /// <summary>
        /// Automatically invoke StartServer()
        /// <para>If the application is a Server Build or run with the -batchMode command line arguement, StartServer is automatically invoked.</para>
        /// </summary>
        [Tooltip("Should the server auto-start when the game is started in a headless build?")]
        public bool startOnHeadless = true;

        /// <summary>
        /// Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.
        /// </summary>
        [Tooltip("Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        public int serverTickRate = 30;

        public NetworkServer server;
        public NetworkClient client;

        // transport layer
        [Header("Network Info")]
        [Tooltip("Transport component attached to this object that server and client will use to connect")]
        [SerializeField]
        protected AsyncTransport transport;

        /// <summary>
        /// True if the server or client is started and running
        /// <para>This is set True in StartServer / StartClient, and set False in StopServer / StopClient</para>
        /// </summary>
        public bool IsNetworkActive => server.Active || client.Active;

        /// <summary>
        /// This is true if the client loaded a new scene when connecting to the server.
        /// <para>This is set before OnClientConnect is called, so it can be checked there to perform different logic if a scene load occurred.</para>
        /// </summary>
        [NonSerialized]
        public bool clientLoadedScene;

        /// <summary>
        /// This is invoked when a host is started.
        /// <para>StartHost has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public UnityEvent OnStartHost = new UnityEvent();

        /// <summary>
        /// This is called when a host is stopped.
        /// </summary>
        public UnityEvent OnStopHost = new UnityEvent();

        #region Unity Callbacks

        /// <summary>
        /// virtual so that inheriting classes' OnValidate() can call base.OnValidate() too
        /// </summary>
        public virtual void OnValidate()
        {
            // add transport if there is none yet. makes upgrading easier.
            if (transport == null)
            {
                // was a transport added yet? if not, add one
                transport = GetComponent<AsyncTransport>();
                if (transport == null)
                {
                    transport = gameObject.AddComponent<AsyncTcpTransport>();
                    logger.Log("NetworkManager: added default Transport because there was none yet.");
                }
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(gameObject, "Added default Transport");
#endif
            }

            // add NetworkServer if there is none yet. makes upgrading easier.
            if (GetComponent<NetworkServer>() == null)
            {
                server = gameObject.AddComponent<NetworkServer>();
                logger.Log("NetworkManager: added NetworkServer because there was none yet.");
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(gameObject, "Added NetworkServer");
#endif
            }

            // add NetworkClient if there is none yet. makes upgrading easier.
            if (GetComponent<NetworkClient>() == null)
            {
                client = gameObject.AddComponent<NetworkClient>();
                logger.Log("NetworkManager: added NetworkClient because there was none yet.");
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(gameObject, "Added NetworkClient");
#endif
            }
        }

        /// <summary>
        /// virtual so that inheriting classes' Start() can call base.Start() too
        /// </summary>
        public virtual void Start()
        {
            logger.Log("Thank you for using Mirror! https://mirror-networking.com");

            // Set the networkSceneName to prevent a scene reload
            // if client connection to server fails.
            networkSceneName = null;

            Initialize();

            // setup OnSceneLoaded callback
            SceneManager.sceneLoaded += OnSceneLoaded;

            // headless mode? then start the server
            // can't do this in Awake because Awake is for initialization.
            // some transports might not be ready until Start.
            //
            // (tick rate is applied in StartServer!)
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null && startOnHeadless)
            {
                _ = StartServer();
            }
        }

        // NetworkIdentity.UNetStaticUpdate is called from UnityEngine while LLAPI network is active.
        // If we want TCP then we need to call it manually. Probably best from NetworkManager, although this means that we can't use NetworkServer/NetworkClient without a NetworkManager invoking Update anymore.
        /// <summary>
        /// virtual so that inheriting classes' LateUpdate() can call base.LateUpdate() too
        /// </summary>
        public virtual void LateUpdate()
        {
            UpdateScene();
        }

        #endregion

        #region Start & Stop

        // full server setup code, without spawning objects yet
        async Task SetupServer()
        {
            logger.Log("NetworkManager SetupServer");

            ConfigureServerFrameRate();

            // start listening to network connections
            await server.ListenAsync();
        }

        /// <summary>
        /// This starts a new server.
        /// </summary>
        /// <returns></returns>
        public async Task StartServer()
        {
            // StartServer is inherently ASYNCHRONOUS (=doesn't finish immediately)
            //
            // Here is what it does:
            //   Listen
            //   if onlineScene:
            //       LoadSceneAsync
            //       ...
            //       FinishLoadSceneServerOnly
            //           SpawnObjects
            //   else:
            //       SpawnObjects
            //
            // there is NO WAY to make it synchronous because both LoadSceneAsync
            // and LoadScene do not finish loading immediately. as long as we
            // have the onlineScene feature, it will be asynchronous!

            await SetupServer();

            server.SpawnObjects();
        }

        /// <summary>
        /// This starts a network client. It uses the networkAddress property as the address to connect to.
        /// <para>This makes the newly created client connect to the server immediately.</para>
        /// </summary>
        public Task StartClient(string serverIp)
        {
            if (logger.LogEnabled()) logger.Log("NetworkManager StartClient address:" + serverIp);

            var builder = new UriBuilder
            {
                Host = serverIp,
                Scheme = transport.Scheme,
            };

            return client.ConnectAsync(builder.Uri);
        }

        /// <summary>
        /// This starts a network client. It uses the Uri parameter as the address to connect to.
        /// <para>This makes the newly created client connect to the server immediately.</para>
        /// </summary>
        /// <param name="uri">location of the server to connect to</param>
        public void StartClient(Uri uri)
        {
            if (logger.LogEnabled()) logger.Log("NetworkManager StartClient address:" + uri);

            _ = client.ConnectAsync(uri);
        }

        /// <summary>
        /// This starts a network "host" - a server and client in the same application.
        /// <para>The client returned from StartHost() is a special "local" client that communicates to the in-process server using a message queue instead of the real network. But in almost all other cases, it can be treated as a normal client.</para>
        /// </summary>
        public async Task StartHost()
        {
            // StartHost is inherently ASYNCHRONOUS (=doesn't finish immediately)
            //
            // Here is what it does:
            //   Listen
            //   ConnectHost
            //   if onlineScene:
            //       LoadSceneAsync
            //       ...
            //       FinishLoadSceneHost
            //           FinishStartHost
            //               SpawnObjects
            //               StartHostClient      <= not guaranteed to happen after SpawnObjects if onlineScene is set!
            //                   ClientAuth
            //                       success: server sends changescene msg to client
            //   else:
            //       FinishStartHost
            //
            // there is NO WAY to make it synchronous because both LoadSceneAsync
            // and LoadScene do not finish loading immediately. as long as we
            // have the onlineScene feature, it will be asynchronous!

            // setup server first
            await SetupServer();

            client.ConnectHost(server);

            // call OnStartHost AFTER SetupServer. this way we can use
            // NetworkServer.Spawn etc. in there too. just like OnStartServer
            // is called after the server is actually properly started.
            OnStartHost.Invoke();

            FinishStartHost();
        }

        // FinishStartHost is guaranteed to be called after the host server was
        // fully started and all the asynchronous StartHost magic is finished
        // (= scene loading), or immediately if there was no asynchronous magic.
        //
        // note: we don't really need FinishStartClient/FinishStartServer. the
        //       host version is enough.
        void FinishStartHost()
        {
            // ConnectHost needs to be called BEFORE SpawnObjects:
            // https://github.com/vis2k/Mirror/pull/1249/
            // -> this sets NetworkServer.localConnection.
            // -> localConnection needs to be set before SpawnObjects because:
            //    -> SpawnObjects calls OnStartServer in all NetworkBehaviours
            //       -> OnStartServer might spawn an object and set [SyncVar(hook="OnColorChanged")] object.color = green;
            //          -> this calls SyncVar.set (generated by Weaver), which has
            //             a custom case for host mode (because host mode doesn't
            //             get OnDeserialize calls, where SyncVar hooks are usually
            //             called):
            //
            //               if (!SyncVarEqual(value, ref color))
            //               {
            //                   if (NetworkServer.localClientActive && !getSyncVarHookGuard(1uL))
            //                   {
            //                       setSyncVarHookGuard(1uL, value: true);
            //                       OnColorChangedHook(value);
            //                       setSyncVarHookGuard(1uL, value: false);
            //                   }
            //                   SetSyncVar(value, ref color, 1uL);
            //               }
            //
            //          -> localClientActive needs to be true, otherwise the hook
            //             isn't called in host mode!
            //


            // server scene was loaded. now spawn all the objects
            server.SpawnObjects();

            // connect client and call OnStartClient AFTER server scene was
            // loaded and all objects were spawned.
            // DO NOT do this earlier. it would cause race conditions where a
            // client will do things before the server is even fully started.
            logger.Log("StartHostClient called");
            StartHostClient();
        }

        void StartHostClient()
        {
            logger.Log("NetworkManager ConnectLocalClient");

            server.ActivateHostScene();

            RegisterClientMessages(client.Connection);
        }

        /// <summary>
        /// This stops both the client and the server that the manager is using.
        /// </summary>
        public void StopHost()
        {
            OnStopHost.Invoke();
            StopClient();
            StopServer();
        }

        /// <summary>
        /// Stops the server that the manager is using.
        /// </summary>
        public void StopServer()
        {
            server.Disconnect();
        }

        /// <summary>
        /// Stops the client that the manager is using.
        /// </summary>
        public void StopClient()
        {
            client.Disconnect();
        }

        /// <summary>
        /// Set the frame rate for a headless server.
        /// <para>Override if you wish to disable the behavior or set your own tick rate.</para>
        /// </summary>
        public virtual void ConfigureServerFrameRate()
        {
            // set a fixed tick rate instead of updating as often as possible
            // * if not in Editor (it doesn't work in the Editor)
            // * if not in Host mode
#if !UNITY_EDITOR
            if (!client.Active && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Application.targetFrameRate = serverTickRate;
                logger.Log("Server Tick Rate set to: " + Application.targetFrameRate + " Hz.");
            }
#endif
        }

        void Initialize()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            // subscribe to the server
            if (server != null)
                server.Authenticated.AddListener(OnServerAuthenticated);

            // subscribe to the client
            if (client != null)
                client.Authenticated.AddListener(OnClientAuthenticated);
        }

        /// <summary>
        /// virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        /// </summary>
        public virtual void OnDestroy()
        {
            logger.Log("NetworkManager destroyed");
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// The name of the current network scene.
        /// </summary>
        /// <remarks>
        /// <para>This is populated if the NetworkManager is doing scene management. This should not be changed directly. Calls to ServerChangeScene() cause this to change. New clients that connect to a server will automatically load this scene.</para>
        /// <para>This is used to make sure that all scene changes are initialized by Mirror.</para>
        /// <para>Loading a scene manually wont set networkSceneName, so Mirror would still load it again on start.</para>
        /// </remarks>
        public string networkSceneName = "";

        public AsyncOperation loadingSceneAsync;

        /// <summary>
        /// This causes the server to switch scenes and sets the networkSceneName.
        /// <para>Clients that connect to this server will automatically switch to this scene. This is called autmatically if onlineScene or offlineScene are set, but it can be called from user code to switch scenes again while the game is in progress. This automatically sets clients to be not-ready. The clients must call NetworkClient.Ready() again to participate in the new scene.</para>
        /// </summary>
        /// <param name="newSceneName"></param>
        public virtual void ServerChangeScene(string newSceneName)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                throw new ArgumentNullException(nameof(newSceneName), "ServerChangeScene: " + nameof(newSceneName) + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ServerChangeScene " + newSceneName);
            server.SetAllClientsNotReady();
            networkSceneName = newSceneName;

            // Let server prepare for scene change
            server.OnServerChangeScene(newSceneName);

            loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);

            // notify all clients about the new scene
            server.SendToAll(new SceneMessage { sceneName = newSceneName });
        }

        internal void ClientChangeScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal, bool customHandling = false)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                throw new ArgumentNullException(nameof(newSceneName), "ClientChangeScene: " + nameof(newSceneName) + " cannot be empty or null");
            }

            if (logger.LogEnabled()) logger.Log("ClientChangeScene newSceneName:" + newSceneName + " networkSceneName:" + networkSceneName);

            // vis2k: pause message handling while loading scene. otherwise we will process messages and then lose all
            // the state as soon as the load is finishing, causing all kinds of bugs because of missing state.
            // (client may be null after StopClient etc.)
            logger.Log("ClientChangeScene: pausing handlers while scene is loading to avoid data loss after scene was loaded.");
            // Let client prepare for scene change
            client.OnClientChangeScene(newSceneName, sceneOperation, customHandling);

            // scene handling will happen in overrides of OnClientChangeScene and/or OnClientSceneChanged
            if (customHandling)
            {
                FinishLoadScene();
                return;
            }

            switch (sceneOperation)
            {
                case SceneOperation.Normal:
                    loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);
                    break;
                case SceneOperation.LoadAdditive:
                    // Ensure additive scene is not already loaded on client by name or path
                    // since we don't know which was passed in the Scene message
                    if (!SceneManager.GetSceneByName(newSceneName).IsValid() && !SceneManager.GetSceneByPath(newSceneName).IsValid())
                        loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName, LoadSceneMode.Additive);
                    else
                    {
                        logger.LogWarning($"Scene {newSceneName} is already loaded");
                    }
                    break;
                case SceneOperation.UnloadAdditive:
                    // Ensure additive scene is actually loaded on client by name or path
                    // since we don't know which was passed in the Scene message
                    if (SceneManager.GetSceneByName(newSceneName).IsValid() || SceneManager.GetSceneByPath(newSceneName).IsValid())
                        loadingSceneAsync = SceneManager.UnloadSceneAsync(newSceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    else
                    {
                        logger.LogWarning($"Cannot unload {newSceneName} with UnloadAdditive operation");
                    }
                    break;
            }

            // don't change the client's current networkSceneName when loading additive scene content
            if (sceneOperation == SceneOperation.Normal)
                networkSceneName = newSceneName;
        }

        // support additive scene loads:
        //   NetworkScenePostProcess disables all scene objects on load, and
        //   * NetworkServer.SpawnObjects enables them again on the server when
        //     calling OnStartServer
        //   * ClientScene.PrepareToSpawnSceneObjects enables them again on the
        //     client after the server sends ObjectSpawnStartedMessage to client
        //     in SpawnObserversForConnection. this is only called when the
        //     client joins, so we need to rebuild scene objects manually again
        // TODO merge this with FinishLoadScene()?
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                if (server.Active)
                {
                    // TODO only respawn the server objects from that scene later!
                    server.SpawnObjects();
                    if (logger.LogEnabled()) logger.Log("Respawned Server objects after additive scene load: " + scene.name);
                }
                if (client.Active)
                {
                    client.PrepareToSpawnSceneObjects();
                    if (logger.LogEnabled()) logger.Log("Rebuild Client spawnableObjects after additive scene load: " + scene.name);
                }
            }
        }

        void UpdateScene()
        {
            if (loadingSceneAsync != null && loadingSceneAsync.isDone)
            {
                if (logger.LogEnabled()) logger.Log("ClientChangeScene done readyCon:" + client.Connection);
                FinishLoadScene();
                loadingSceneAsync.allowSceneActivation = true;
                loadingSceneAsync = null;
            }
        }

        void FinishLoadScene()
        {
            // NOTE: this cannot use NetworkClient.allClients[0] - that client may be for a completely different purpose.

            // process queued messages that we received while loading the scene
            logger.Log("FinishLoadScene: resuming handlers after scene was loading.");
            // host mode?
            if (client.IsLocalClient)
            {
                FinishLoadSceneHost();
            }
            // server-only mode?
            else if (server.Active)
            {
                FinishLoadSceneServerOnly();
            }
            // client-only mode?
            else if (client.Active)
            {
                FinishLoadSceneClientOnly();
            }
        }

        // finish load scene part for host mode. makes code easier and is
        // necessary for FinishStartHost later.
        // (the 3 things have to happen in that exact order)
        void FinishLoadSceneHost()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            logger.Log("Finished loading scene in host mode.");

            if (client.Connection != null)
            {
                client.OnAuthenticated(client.Connection);
                clientLoadedScene = true;
            }

            FinishStartHost();

            // call OnServerSceneChanged
            server.OnServerSceneChanged(networkSceneName);

            if (client.IsConnected)
            {
                // let client know that we changed scene
                client.OnClientSceneChanged(client.Connection);
            }
        }

        // finish load scene part for client-only. makes code easier and is
        // necessary for FinishStartClient later.
        void FinishLoadSceneClientOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            logger.Log("Finished loading scene in client-only mode.");

            if (client.Connection != null)
            {
                client.OnAuthenticated(client.Connection);
                clientLoadedScene = true;
            }

            if (client.IsConnected)
            {
                client.OnClientSceneChanged(client.Connection);
            }
        }

        // finish load scene part for server-only. . makes code easier and is
        // necessary for FinishStartServer later.
        void FinishLoadSceneServerOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            logger.Log("Finished loading scene in server-only mode.");

            server.SpawnObjects();
            server.OnServerSceneChanged(networkSceneName);
        }

        #endregion

        #region Server Internal Message Handlers

        void RegisterServerMessages(INetworkConnection connection)
        {
            //Currently no messages are regsitered to the Server from NetworkManager
        }

        // called after successful authentication
        void OnServerAuthenticated(INetworkConnection conn)
        {
            // a connection has been established,  register for our messages
            RegisterServerMessages(conn);

            logger.Log("NetworkManager.OnServerAuthenticated");

            // proceed with the login handshake by calling OnServerConnect
            if (!string.IsNullOrEmpty(networkSceneName))
            {
                var msg = new SceneMessage { sceneName = networkSceneName };
                conn.Send(msg);
            }
        }

        #endregion

        #region Client Internal Message Handlers

        void RegisterClientMessages(INetworkConnection connection)
        {
            connection.RegisterHandler<NotReadyMessage>(OnClientNotReadyMessageInternal);
            connection.RegisterHandler<SceneMessage>(OnClientSceneInternal);
        }

        // called after successful authentication
        void OnClientAuthenticated(INetworkConnection conn)
        {
            RegisterClientMessages(conn);

            logger.Log("NetworkManager.OnClientAuthenticated");

            // will wait for scene id to come from the server.
            clientLoadedScene = true;
        }

        void OnClientNotReadyMessageInternal(INetworkConnection conn, NotReadyMessage msg)
        {
            logger.Log("NetworkManager.OnClientNotReadyMessageInternal");

            client.ready = false;
            client.OnClientNotReady(conn);

            // NOTE: clientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        void OnClientSceneInternal(INetworkConnection conn, SceneMessage msg)
        {
            logger.Log("NetworkManager.OnClientSceneInternal");

            if (client.IsConnected && !server.Active)
            {
                ClientChangeScene(msg.sceneName, msg.sceneOperation, msg.customHandling);
            }
        }

        #endregion

    }
}
