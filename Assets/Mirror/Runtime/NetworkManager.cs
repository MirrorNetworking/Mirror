using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    /// <summary>
    /// Enumeration of methods of where to spawn player objects in multiplayer games.
    /// </summary>
    public enum PlayerSpawnMethod { Random, RoundRobin }

    /// <summary>
    /// Enumeration of methods of current Network Manager state at runtime.
    /// </summary>
    public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly, Host }

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkManager")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkManager.html")]
    public class NetworkManager : MonoBehaviour
    {
        /// <summary>
        /// A flag to control whether the NetworkManager object is destroyed when the scene changes.
        /// <para>This should be set if your game has a single NetworkManager that exists for the lifetime of the process. If there is a NetworkManager in each scene, then this should not be set.</para>
        /// </summary>
        [Header("Configuration")]
        [FormerlySerializedAs("m_DontDestroyOnLoad")]
        [Tooltip("Should the Network Manager object be persisted through scene changes?")]
        public bool dontDestroyOnLoad = true;

        /// <summary>
        /// Controls whether the program runs when it is in the background.
        /// <para>This is required when multiple instances of a program using networking are running on the same machine, such as when testing using localhost. But this is not recommended when deploying to mobile platforms.</para>
        /// </summary>
        [FormerlySerializedAs("m_RunInBackground")]
        [Tooltip("Should the server or client keep running in the background?")]
        public bool runInBackground = true;

        /// <summary>
        /// Automatically invoke StartServer()
        /// <para>If the application is a Server Build or run with the -batchMode command line arguement, StartServer is automatically invoked.</para>
        /// </summary>
        [Tooltip("Should the server auto-start when the game is started in a headless build?")]
        public bool startOnHeadless = true;

        /// <summary>
        /// Enables verbose debug messages in the console
        /// </summary>
        [FormerlySerializedAs("m_ShowDebugMessages")]
        [Tooltip("This will enable verbose debug messages in the Unity Editor console")]
        public bool showDebugMessages;

        /// <summary>
        /// Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.
        /// </summary>
        [Tooltip("Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        public int serverTickRate = 30;

        /// <summary>
        /// The scene to switch to when offline.
        /// <para>Setting this makes the NetworkManager do scene management. This scene will be switched to when a network session is completed - such as a client disconnect, or a server shutdown.</para>
        /// </summary>
        [Header("Scene Management")]
        [Scene]
        [FormerlySerializedAs("m_OfflineScene")]
        [Tooltip("Scene that Mirror will switch to when the client or server is stopped")]
        public string offlineScene = "";

        /// <summary>
        /// The scene to switch to when online.
        /// <para>Setting this makes the NetworkManager do scene management. This scene will be switched to when a network session is started - such as a client connect, or a server listen.</para>
        /// </summary>
        [Scene]
        [FormerlySerializedAs("m_OnlineScene")]
        [Tooltip("Scene that Mirror will switch to when the server is started. Clients will recieve a Scene Message to load the server's current scene when they connect.")]
        public string onlineScene = "";

        // transport layer
        [Header("Network Info")]
        [Tooltip("Transport component attached to this object that server and client will use to connect")]
        [SerializeField]
        protected Transport transport;

        /// <summary>
        /// The network address currently in use.
        /// <para>For clients, this is the address of the server that is connected to. For servers, this is the local address.</para>
        /// </summary>
        [FormerlySerializedAs("m_NetworkAddress")]
        [Tooltip("Network Address where the client should connect to the server. Server does not use this for anything.")]
        public string networkAddress = "localhost";

        /// <summary>
        /// The maximum number of concurrent network connections to support.
        /// <para>This effects the memory usage of the network layer.</para>
        /// </summary>
        [FormerlySerializedAs("m_MaxConnections")]
        [Tooltip("Maximum number of concurrent connections.")]
        public int maxConnections = 4;

        // This value is passed to NetworkServer in SetupServer
        /// <summary>
        /// Should the server disconnect remote connections that have gone silent for more than Server Idle Timeout?
        /// </summary>
        [Tooltip("Server Only - Disconnects remote connections that have been silent for more than Server Idle Timeout")]
        public bool disconnectInactiveConnections;

        // This value is passed to NetworkServer in SetupServer
        /// <summary>
        /// Timeout in seconds since last message from a client after which server will auto-disconnect.
        /// <para>By default, clients send at least a Ping message every 2 seconds.</para>
        /// <para>The Host client is immune from idle timeout disconnection.</para>
        /// <para>Default value is 60 seconds.</para>
        /// </summary>
        [Tooltip("Timeout in seconds since last message from a client after which server will auto-disconnect if Disconnect Inactive Connections is enabled.")]
        public float disconnectInactiveTimeout = 60f;

        [Header("Authentication")]
        [Tooltip("Authentication component attached to this object")]
        public NetworkAuthenticator authenticator;

        /// <summary>
        /// The default prefab to be used to create player objects on the server.
        /// <para>Player objects are created in the default handler for AddPlayer() on the server. Implementing OnServerAddPlayer overrides this behaviour.</para>
        /// </summary>
        [Header("Player Object")]
        [FormerlySerializedAs("m_PlayerPrefab")]
        [Tooltip("Prefab of the player object. Prefab must have a Network Identity component. May be an empty game object or a full avatar.")]
        public GameObject playerPrefab;

        /// <summary>
        /// A flag to control whether or not player objects are automatically created on connect, and on scene change.
        /// </summary>
        [FormerlySerializedAs("m_AutoCreatePlayer")]
        [Tooltip("Should Mirror automatically spawn the player after scene change?")]
        public bool autoCreatePlayer = true;

        /// <summary>
        /// The current method of spawning players used by the NetworkManager.
        /// </summary>
        [FormerlySerializedAs("m_PlayerSpawnMethod")]
        [Tooltip("Round Robin or Random order of Start Position selection")]
        public PlayerSpawnMethod playerSpawnMethod;

        /// <summary>
        /// List of prefabs that will be registered with the spawning system.
        /// <para>For each of these prefabs, ClientManager.RegisterPrefab() will be automatically invoke.</para>
        /// </summary>
        [FormerlySerializedAs("m_SpawnPrefabs"), HideInInspector]
        public List<GameObject> spawnPrefabs = new List<GameObject>();

        /// <summary>
        /// NetworkManager singleton
        /// </summary>
        public static NetworkManager singleton { get; private set; }

        /// <summary>
        /// Number of active player objects across all connections on the server.
        /// <para>This is only valid on the host / server.</para>
        /// </summary>
        public int numPlayers => NetworkServer.connections.Count(kv => kv.Value.identity != null);

        /// <summary>
        /// True if the server or client is started and running
        /// <para>This is set True in StartServer / StartClient, and set False in StopServer / StopClient</para>
        /// </summary>
        [NonSerialized]
        public bool isNetworkActive;

        static NetworkConnection clientReadyConnection;

        /// <summary>
        /// This is true if the client loaded a new scene when connecting to the server.
        /// <para>This is set before OnClientConnect is called, so it can be checked there to perform different logic if a scene load occurred.</para>
        /// </summary>
        [NonSerialized]
        public bool clientLoadedScene;

        /// <summary>
        /// headless mode detection
        /// </summary>
        public static bool isHeadless => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        // helper enum to know if we started the networkmanager as server/client/host.
        // -> this is necessary because when StartHost changes server scene to
        //    online scene, FinishLoadScene is called and the host client isn't
        //    connected yet (no need to connect it before server was fully set up).
        //    in other words, we need this to know which mode we are running in
        //    during FinishLoadScene.
        public NetworkManagerMode mode { get; private set; }

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
                transport = GetComponent<Transport>();
                if (transport == null)
                {
                    transport = gameObject.AddComponent<TelepathyTransport>();
                    Debug.Log("NetworkManager: added default Transport because there was none yet.");
                }
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(gameObject, "Added default Transport");
#endif
            }

            // always >= 0
            maxConnections = Mathf.Max(maxConnections, 0);

            if (playerPrefab != null && playerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError("NetworkManager - playerPrefab must have a NetworkIdentity.");
                playerPrefab = null;
            }
        }

        /// <summary>
        /// virtual so that inheriting classes' Awake() can call base.Awake() too
        /// </summary>
        public virtual void Awake()
        {
            // Don't allow collision-destroyed second instance to continue.
            if (!InitializeSingleton()) return;

            Debug.Log("Thank you for using Mirror! https://mirror-networking.com");

            // Set the networkSceneName to prevent a scene reload
            // if client connection to server fails.
            networkSceneName = offlineScene;

            // setup OnSceneLoaded callback
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// virtual so that inheriting classes' Start() can call base.Start() too
        /// </summary>
        public virtual void Start()
        {
            // headless mode? then start the server
            // can't do this in Awake because Awake is for initialization.
            // some transports might not be ready until Start.
            //
            // (tick rate is applied in StartServer!)
            if (isHeadless && startOnHeadless)
            {
                StartServer();
            }
        }

        // NetworkIdentity.UNetStaticUpdate is called from UnityEngine while LLAPI network is active.
        // If we want TCP then we need to call it manually. Probably best from NetworkManager, although this means that we can't use NetworkServer/NetworkClient without a NetworkManager invoking Update anymore.
        /// <summary>
        /// virtual so that inheriting classes' LateUpdate() can call base.LateUpdate() too
        /// </summary>
        public virtual void LateUpdate()
        {
            // call it while the NetworkManager exists.
            // -> we don't only call while Client/Server.Connected, because then we would stop if disconnected and the
            //    NetworkClient wouldn't receive the last Disconnect event, result in all kinds of issues
            NetworkServer.Update();
            NetworkClient.Update();
            UpdateScene();
        }

        #endregion

        #region Start & Stop

        // keep the online scene change check in a separate function
        bool IsServerOnlineSceneChangeNeeded()
        {
            // Only change scene if the requested online scene is not blank, and is not already loaded
            return !string.IsNullOrEmpty(onlineScene) && !IsSceneActive(onlineScene) && onlineScene != offlineScene;
        }

        public static bool IsSceneActive(string scene)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.path == scene || activeScene.name == scene;
        }

        // full server setup code, without spawning objects yet
        void SetupServer()
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager SetupServer");
            InitializeSingleton();

            if (runInBackground)
                Application.runInBackground = true;

            if (authenticator != null)
            {
                authenticator.OnStartServer();
                authenticator.OnServerAuthenticated.AddListener(OnServerAuthenticated);
            }

            ConfigureServerFrameRate();

            // Copy auto-disconnect settings to NetworkServer
            NetworkServer.disconnectInactiveTimeout = disconnectInactiveTimeout;
            NetworkServer.disconnectInactiveConnections = disconnectInactiveConnections;

            // start listening to network connections
            NetworkServer.Listen(maxConnections);

            // call OnStartServer AFTER Listen, so that NetworkServer.active is
            // true and we can call NetworkServer.Spawn in OnStartServer
            // overrides.
            // (useful for loading & spawning stuff from database etc.)
            //
            // note: there is no risk of someone connecting after Listen() and
            //       before OnStartServer() because this all runs in one thread
            //       and we don't start processing connects until Update.
            OnStartServer();

            // this must be after Listen(), since that registers the default message handlers
            RegisterServerMessages();

            isNetworkActive = true;
        }

        /// <summary>
        /// This starts a new server.
        /// </summary>
        /// <returns></returns>
        public void StartServer()
        {
            mode = NetworkManagerMode.ServerOnly;

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

            SetupServer();

            // scene change needed? then change scene and spawn afterwards.
            if (IsServerOnlineSceneChangeNeeded())
            {
                ServerChangeScene(onlineScene);
            }
            // otherwise spawn directly
            else
            {
                NetworkServer.SpawnObjects();
            }
        }

        /// <summary>
        /// This starts a network client. It uses the networkAddress property as the address to connect to.
        /// <para>This makes the newly created client connect to the server immediately.</para>
        /// </summary>
        public void StartClient()
        {
            mode = NetworkManagerMode.ClientOnly;

            InitializeSingleton();

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated);
            }

            if (runInBackground)
                Application.runInBackground = true;

            isNetworkActive = true;

            RegisterClientMessages();

            if (string.IsNullOrEmpty(networkAddress))
            {
                Debug.LogError("Must set the Network Address field in the manager");
                return;
            }
            if (LogFilter.Debug) Debug.Log("NetworkManager StartClient address:" + networkAddress);

            NetworkClient.Connect(networkAddress);

            OnStartClient();
        }

        /// <summary>
        /// This starts a network client. It uses the Uri parameter as the address to connect to.
        /// <para>This makes the newly created client connect to the server immediately.</para>
        /// </summary>
        /// <param name="uri">location of the server to connect to</param>
        public void StartClient(Uri uri)
        {
            mode = NetworkManagerMode.ClientOnly;

            InitializeSingleton();

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated);
            }

            if (runInBackground)
            {
                Application.runInBackground = true;
            }

            isNetworkActive = true;

            RegisterClientMessages();

            if (LogFilter.Debug) Debug.Log("NetworkManager StartClient address:" + uri);
            networkAddress = uri.Host;

            NetworkClient.Connect(uri);

            OnStartClient();
        }

        /// <summary>
        /// This starts a network "host" - a server and client in the same application.
        /// <para>The client returned from StartHost() is a special "local" client that communicates to the in-process server using a message queue instead of the real network. But in almost all other cases, it can be treated as a normal client.</para>
        /// </summary>
        public void StartHost()
        {
            mode = NetworkManagerMode.Host;

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
            SetupServer();

            // call OnStartHost AFTER SetupServer. this way we can use
            // NetworkServer.Spawn etc. in there too. just like OnStartServer
            // is called after the server is actually properly started.
            OnStartHost();

            // scene change needed? then change scene and spawn afterwards.
            // => BEFORE host client connects. if client auth succeeds then the
            //    server tells it to load 'onlineScene'. we can't do that if
            //    server is still in 'offlineScene'. so load on server first.
            if (IsServerOnlineSceneChangeNeeded())
            {
                // call FinishStartHost after changing scene.
                finishStartHostPending = true;
                ServerChangeScene(onlineScene);
            }
            // otherwise call FinishStartHost directly
            else
            {
                FinishStartHost();
            }
        }

        // This may be set true in StartHost and is evaluated in FinishStartHost
        bool finishStartHostPending;

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
            // TODO call this after spawnobjects and worry about the syncvar hook fix later?
            NetworkClient.ConnectHost();

            // server scene was loaded. now spawn all the objects
            NetworkServer.SpawnObjects();

            // connect client and call OnStartClient AFTER server scene was
            // loaded and all objects were spawned.
            // DO NOT do this earlier. it would cause race conditions where a
            // client will do things before the server is even fully started.
            if (LogFilter.Debug) Debug.Log("StartHostClient called");
            StartHostClient();
        }

        void StartHostClient()
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager ConnectLocalClient");

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated);
            }

            networkAddress = "localhost";
            NetworkServer.ActivateHostScene();
            RegisterClientMessages();

            // ConnectLocalServer needs to be called AFTER RegisterClientMessages
            // (https://github.com/vis2k/Mirror/pull/1249/)
            NetworkClient.ConnectLocalServer();

            OnStartClient();
        }

        /// <summary>
        /// This stops both the client and the server that the manager is using.
        /// </summary>
        public void StopHost()
        {
            OnStopHost();

            // TODO try to move DisconnectLocalServer into StopClient(), and
            // then call StopClient() before StopServer(). needs testing!.

            // DisconnectLocalServer needs to be called so that the host client
            // receives a DisconnectMessage too.
            // fixes: https://github.com/vis2k/Mirror/issues/1515
            NetworkClient.DisconnectLocalServer();

            StopClient();
            StopServer();
        }

        /// <summary>
        /// Stops the server that the manager is using.
        /// </summary>
        public void StopServer()
        {
            if (!NetworkServer.active)
                return;

            if (authenticator != null)
                authenticator.OnServerAuthenticated.RemoveListener(OnServerAuthenticated);

            OnStopServer();

            if (LogFilter.Debug) Debug.Log("NetworkManager StopServer");
            isNetworkActive = false;
            NetworkServer.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;

            if (!string.IsNullOrEmpty(offlineScene))
            {
                ServerChangeScene(offlineScene);
            }

            CleanupNetworkIdentities();

            startPositionIndex = 0;

            networkSceneName = "";
        }

        /// <summary>
        /// Stops the client that the manager is using.
        /// </summary>
        public void StopClient()
        {
            if (authenticator != null)
                authenticator.OnClientAuthenticated.RemoveListener(OnClientAuthenticated);

            OnStopClient();

            if (LogFilter.Debug) Debug.Log("NetworkManager StopClient");
            isNetworkActive = false;

            // shutdown client
            NetworkClient.Disconnect();
            NetworkClient.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;

            // If this is the host player, StopServer will already be changing scenes.
            // Check loadingSceneAsync to ensure we don't double-invoke the scene change.
            if (!string.IsNullOrEmpty(offlineScene) && !IsSceneActive(offlineScene) && loadingSceneAsync == null)
            {
                ClientChangeScene(offlineScene, SceneOperation.Normal);
            }

            CleanupNetworkIdentities();

            networkSceneName = "";
        }

        /// <summary>
        /// called when quitting the application by closing the window / pressing stop in the editor
        /// <para>virtual so that inheriting classes' OnApplicationQuit() can call base.OnApplicationQuit() too</para>
        /// </summary>
        public virtual void OnApplicationQuit()
        {
            // stop client first
            // (we want to send the quit packet to the server instead of waiting
            //  for a timeout)
            if (NetworkClient.isConnected)
            {
                StopClient();
                print("OnApplicationQuit: stopped client");
            }

            // stop server after stopping client (for proper host mode stopping)
            if (NetworkServer.active)
            {
                StopServer();
                print("OnApplicationQuit: stopped server");
            }
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
            if (!NetworkClient.active && isHeadless)
            {
                Application.targetFrameRate = serverTickRate;
                Debug.Log("Server Tick Rate set to: " + Application.targetFrameRate + " Hz.");
            }
#endif
        }

        bool InitializeSingleton()
        {
            if (singleton != null && singleton == this) return true;

            // do this early
            LogFilter.Debug = showDebugMessages;
            if (LogFilter.Debug)
            {
                LogFactory.EnableDebugMode();
            }

            if (dontDestroyOnLoad)
            {
                if (singleton != null)
                {
                    Debug.LogWarning("Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time. The duplicate NetworkManager will be destroyed.");
                    Destroy(gameObject);

                    // Return false to not allow collision-destroyed second instance to continue.
                    return false;
                }
                if (LogFilter.Debug) Debug.Log("NetworkManager created singleton (DontDestroyOnLoad)");
                singleton = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else
            {
                if (LogFilter.Debug) Debug.Log("NetworkManager created singleton (ForScene)");
                singleton = this;
            }

            // set active transport AFTER setting singleton.
            // so only if we didn't destroy ourselves.
            Transport.activeTransport = transport;

            return true;
        }

        void RegisterServerMessages()
        {
            NetworkServer.RegisterHandler<ConnectMessage>(OnServerConnectInternal, false);
            NetworkServer.RegisterHandler<DisconnectMessage>(OnServerDisconnectInternal, false);
            NetworkServer.RegisterHandler<AddPlayerMessage>(OnServerAddPlayerInternal);
            NetworkServer.RegisterHandler<ErrorMessage>(OnServerErrorInternal, false);

            // Network Server initially registers it's own handlers for these, so we replace them here.
            NetworkServer.ReplaceHandler<ReadyMessage>(OnServerReadyMessageInternal);
        }

        void RegisterClientMessages()
        {
            // Network Client initially registers it's own handlers for these, so we replace them here.
            NetworkClient.ReplaceHandler<ConnectMessage>(OnClientConnectInternal, false);
            NetworkClient.ReplaceHandler<DisconnectMessage>(OnClientDisconnectInternal, false);
            NetworkClient.ReplaceHandler<NotReadyMessage>(OnClientNotReadyMessageInternal);
            NetworkClient.ReplaceHandler<ErrorMessage>(OnClientErrorInternal, false);
            NetworkClient.ReplaceHandler<SceneMessage>(OnClientSceneInternal, false);

            if (playerPrefab != null)
            {
                ClientScene.RegisterPrefab(playerPrefab);
            }
            for (int i = 0; i < spawnPrefabs.Count; i++)
            {
                GameObject prefab = spawnPrefabs[i];
                if (prefab != null)
                {
                    ClientScene.RegisterPrefab(prefab);
                }
            }
        }

        void CleanupNetworkIdentities()
        {
            foreach (NetworkIdentity identity in Resources.FindObjectsOfTypeAll<NetworkIdentity>())
            {
                identity.Reset();
            }
        }

        /// <summary>
        /// This is the only way to clear the singleton, so another instance can be created.
        /// </summary>
        public static void Shutdown()
        {
            if (singleton == null)
                return;

            startPositions.Clear();
            startPositionIndex = 0;
            clientReadyConnection = null;

            singleton.StopHost();
            singleton = null;
        }

        /// <summary>
        /// virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        /// </summary>
        public virtual void OnDestroy()
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager destroyed");
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
        public static string networkSceneName = "";

        public static UnityEngine.AsyncOperation loadingSceneAsync;

        /// <summary>
        /// This causes the server to switch scenes and sets the networkSceneName.
        /// <para>Clients that connect to this server will automatically switch to this scene. This is called autmatically if onlineScene or offlineScene are set, but it can be called from user code to switch scenes again while the game is in progress. This automatically sets clients to be not-ready. The clients must call NetworkClient.Ready() again to participate in the new scene.</para>
        /// </summary>
        /// <param name="newSceneName"></param>
        public virtual void ServerChangeScene(string newSceneName)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                Debug.LogError("ServerChangeScene empty scene name");
                return;
            }

            if (LogFilter.Debug) Debug.Log("ServerChangeScene " + newSceneName);
            NetworkServer.SetAllClientsNotReady();
            networkSceneName = newSceneName;

            // Let server prepare for scene change
            OnServerChangeScene(newSceneName);

            // Suspend the server's transport while changing scenes
            // It will be re-enabled in FinishScene.
            Transport.activeTransport.enabled = false;

            loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);

            // notify all clients about the new scene
            NetworkServer.SendToAll(new SceneMessage { sceneName = newSceneName });

            startPositionIndex = 0;
            startPositions.Clear();
        }

        // This is only set in ClientChangeScene below...never on server.
        // We need to check this in OnClientSceneChanged called from FinishLoadSceneClientOnly
        // to prevent AddPlayer message after loading/unloading additive scenes
        SceneOperation clientSceneOperation = SceneOperation.Normal;

        internal void ClientChangeScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal, bool customHandling = false)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                Debug.LogError("ClientChangeScene empty scene name");
                return;
            }

            if (LogFilter.Debug) Debug.Log("ClientChangeScene newSceneName:" + newSceneName + " networkSceneName:" + networkSceneName);

            // vis2k: pause message handling while loading scene. otherwise we will process messages and then lose all
            // the state as soon as the load is finishing, causing all kinds of bugs because of missing state.
            // (client may be null after StopClient etc.)
            if (LogFilter.Debug) Debug.Log("ClientChangeScene: pausing handlers while scene is loading to avoid data loss after scene was loaded.");
            Transport.activeTransport.enabled = false;

            // Let client prepare for scene change
            OnClientChangeScene(newSceneName, sceneOperation, customHandling);

            // scene handling will happen in overrides of OnClientChangeScene and/or OnClientSceneChanged
            if (customHandling)
            {
                FinishLoadScene();
                return;
            }

            // cache sceneOperation so we know what was done in OnClientSceneChanged called from FinishLoadSceneClientOnly
            clientSceneOperation = sceneOperation;

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
                        Debug.LogWarning($"Scene {newSceneName} is already loaded");

                        // Re-enable the transport that we disabled before entering this switch
                        Transport.activeTransport.enabled = true;
                    }
                    break;
                case SceneOperation.UnloadAdditive:
                    // Ensure additive scene is actually loaded on client by name or path
                    // since we don't know which was passed in the Scene message
                    if (SceneManager.GetSceneByName(newSceneName).IsValid() || SceneManager.GetSceneByPath(newSceneName).IsValid())
                        loadingSceneAsync = SceneManager.UnloadSceneAsync(newSceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    else
                    {
                        Debug.LogWarning($"Cannot unload {newSceneName} with UnloadAdditive operation");

                        // Re-enable the transport that we disabled before entering this switch
                        Transport.activeTransport.enabled = true;
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
                if (NetworkServer.active)
                {
                    // TODO only respawn the server objects from that scene later!
                    NetworkServer.SpawnObjects();
                    if (LogFilter.Debug) Debug.Log("Respawned Server objects after additive scene load: " + scene.name);
                }
                if (NetworkClient.active)
                {
                    ClientScene.PrepareToSpawnSceneObjects();
                    if (LogFilter.Debug) Debug.Log("Rebuild Client spawnableObjects after additive scene load: " + scene.name);
                }
            }
        }

        static void UpdateScene()
        {
            if (singleton != null && loadingSceneAsync != null && loadingSceneAsync.isDone)
            {
                if (LogFilter.Debug) Debug.Log("ClientChangeScene done readyCon:" + clientReadyConnection);
                singleton.FinishLoadScene();
                loadingSceneAsync.allowSceneActivation = true;
                loadingSceneAsync = null;
            }
        }

        void FinishLoadScene()
        {
            // NOTE: this cannot use NetworkClient.allClients[0] - that client may be for a completely different purpose.

            // process queued messages that we received while loading the scene
            if (LogFilter.Debug) Debug.Log("FinishLoadScene: resuming handlers after scene was loading.");
            Transport.activeTransport.enabled = true;

            // host mode?
            if (mode == NetworkManagerMode.Host)
            {
                FinishLoadSceneHost();
            }
            // server-only mode?
            else if (mode == NetworkManagerMode.ServerOnly)
            {
                FinishLoadSceneServerOnly();
            }
            // client-only mode?
            else if (mode == NetworkManagerMode.ClientOnly)
            {
                FinishLoadSceneClientOnly();
            }
            // otherwise we called it after stopping when loading offline scene.
            // do nothing then.
        }

        // finish load scene part for host mode. makes code easier and is
        // necessary for FinishStartHost later.
        // (the 3 things have to happen in that exact order)
        void FinishLoadSceneHost()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            if (LogFilter.Debug) Debug.Log("Finished loading scene in host mode.");

            if (clientReadyConnection != null)
            {
                OnClientConnect(clientReadyConnection);
                clientLoadedScene = true;
                clientReadyConnection = null;
            }

            // do we need to finish a StartHost() call?
            // then call FinishStartHost and let it take care of spawning etc.
            if (finishStartHostPending)
            {
                finishStartHostPending = false;
                FinishStartHost();

                // call OnServerSceneChanged
                OnServerSceneChanged(networkSceneName);

                if (NetworkClient.isConnected)
                {
                    RegisterClientMessages();

                    // DO NOT call OnClientSceneChanged here.
                    // the scene change happened because StartHost loaded the
                    // server's online scene. it has nothing to do with the client.
                    // this was not meant as a client scene load, so don't call it.
                    //
                    // otherwise AddPlayer would be called twice:
                    // -> once for client OnConnected
                    // -> once in OnClientSceneChanged
                }
            }
            // otherwise we just changed a scene in host mode
            else
            {
                // spawn server objects
                NetworkServer.SpawnObjects();

                // call OnServerSceneChanged
                OnServerSceneChanged(networkSceneName);

                if (NetworkClient.isConnected)
                {
                    RegisterClientMessages();

                    // let client know that we changed scene
                    OnClientSceneChanged(NetworkClient.connection);
                }
            }
        }

        // finish load scene part for client-only. makes code easier and is
        // necessary for FinishStartClient later.
        void FinishLoadSceneClientOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            if (LogFilter.Debug) Debug.Log("Finished loading scene in client-only mode.");

            if (clientReadyConnection != null)
            {
                OnClientConnect(clientReadyConnection);
                clientLoadedScene = true;
                clientReadyConnection = null;
            }

            if (NetworkClient.isConnected)
            {
                OnClientSceneChanged(NetworkClient.connection);
            }
        }

        // finish load scene part for server-only. . makes code easier and is
        // necessary for FinishStartServer later.
        void FinishLoadSceneServerOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            if (LogFilter.Debug) Debug.Log("Finished loading scene in server-only mode.");

            NetworkServer.SpawnObjects();
            OnServerSceneChanged(networkSceneName);
        }

        #endregion

        #region Start Positions

        public static int startPositionIndex;

        /// <summary>
        /// List of transforms populted by NetworkStartPosition components found in the scene.
        /// </summary>
        public static List<Transform> startPositions = new List<Transform>();

        /// <summary>
        /// Registers the transform of a game object as a player spawn location.
        /// <para>This is done automatically by NetworkStartPosition components, but can be done manually from user script code.</para>
        /// </summary>
        /// <param name="start">Transform to register.</param>
        public static void RegisterStartPosition(Transform start)
        {
            if (LogFilter.Debug) Debug.Log("RegisterStartPosition: (" + start.gameObject.name + ") " + start.position);
            startPositions.Add(start);

            // reorder the list so that round-robin spawning uses the start positions
            // in hierarchy order.  This assumes all objects with NetworkStartPosition
            // component are siblings, either in the scene root or together as children
            // under a single parent in the scene.
            startPositions = startPositions.OrderBy(transform => transform.GetSiblingIndex()).ToList();
        }

        /// <summary>
        /// Unregisters the transform of a game object as a player spawn location.
        /// <para>This is done automatically by the <see cref="NetworkStartPosition">NetworkStartPosition</see> component, but can be done manually from user code.</para>
        /// </summary>
        /// <param name="start">Transform to unregister.</param>
        public static void UnRegisterStartPosition(Transform start)
        {
            if (LogFilter.Debug) Debug.Log("UnRegisterStartPosition: (" + start.gameObject.name + ") " + start.position);
            startPositions.Remove(start);
        }

        #endregion

        #region Server Internal Message Handlers

        void OnServerConnectInternal(NetworkConnection conn, ConnectMessage connectMsg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnServerConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnServerAuthenticateInternal(conn);
            }
            else
            {
                // authenticate immediately
                OnServerAuthenticated(conn);
            }
        }

        // called after successful authentication
        void OnServerAuthenticated(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnServerAuthenticated");

            // set connection to authenticated
            conn.isAuthenticated = true;

            // proceed with the login handshake by calling OnServerConnect
            if (networkSceneName != "" && networkSceneName != offlineScene)
            {
                SceneMessage msg = new SceneMessage() { sceneName = networkSceneName };
                conn.Send(msg);
            }

            OnServerConnect(conn);
        }

        void OnServerDisconnectInternal(NetworkConnection conn, DisconnectMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnServerDisconnectInternal");
            OnServerDisconnect(conn);
        }

        void OnServerReadyMessageInternal(NetworkConnection conn, ReadyMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnServerReadyMessageInternal");
            OnServerReady(conn);
        }

        void OnServerAddPlayerInternal(NetworkConnection conn, AddPlayerMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnServerAddPlayer");

            if (autoCreatePlayer && playerPrefab == null)
            {
                Debug.LogError("The PlayerPrefab is empty on the NetworkManager. Please setup a PlayerPrefab object.");
                return;
            }

            if (autoCreatePlayer && playerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError("The PlayerPrefab does not have a NetworkIdentity. Please add a NetworkIdentity to the player prefab.");
                return;
            }

            if (conn.identity != null)
            {
                Debug.LogError("There is already a player for this connection.");
                return;
            }

            OnServerAddPlayer(conn);
        }

        // Deprecated 5/2/2020
        /// <summary>
        /// Obsolete: Removed as a security risk. Use <see cref="NetworkServer.RemovePlayerForConnection(NetworkConnection, GameObject, bool)"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Removed as a security risk. Use NetworkServer.RemovePlayerForConnection(NetworkConnection conn, GameObject player, bool keepAuthority = false) instead", true)]
        void OnServerRemovePlayerMessageInternal(NetworkConnection conn, RemovePlayerMessage msg) { }

        void OnServerErrorInternal(NetworkConnection conn, ErrorMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnServerErrorInternal");
            OnServerError(conn, msg.value);
        }

        #endregion

        #region Client Internal Message Handlers

        void OnClientConnectInternal(NetworkConnection conn, ConnectMessage message)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnClientConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnClientAuthenticateInternal(conn);
            }
            else
            {
                // authenticate immediately
                OnClientAuthenticated(conn);
            }
        }

        // called after successful authentication
        void OnClientAuthenticated(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnClientAuthenticated");

            // set connection to authenticated
            conn.isAuthenticated = true;

            // proceed with the login handshake by calling OnClientConnect
            if (string.IsNullOrEmpty(onlineScene) || onlineScene == offlineScene || IsSceneActive(onlineScene))
            {
                clientLoadedScene = false;
                OnClientConnect(conn);
            }
            else
            {
                // will wait for scene id to come from the server.
                clientLoadedScene = true;
                clientReadyConnection = conn;
            }
        }

        void OnClientDisconnectInternal(NetworkConnection conn, DisconnectMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnClientDisconnectInternal");
            OnClientDisconnect(conn);
        }

        void OnClientNotReadyMessageInternal(NetworkConnection conn, NotReadyMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnClientNotReadyMessageInternal");

            ClientScene.ready = false;
            OnClientNotReady(conn);

            // NOTE: clientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        void OnClientErrorInternal(NetworkConnection conn, ErrorMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager:OnClientErrorInternal");
            OnClientError(conn, msg.value);
        }

        void OnClientSceneInternal(NetworkConnection conn, SceneMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("NetworkManager.OnClientSceneInternal");

            if (NetworkClient.isConnected && !NetworkServer.active)
            {
                ClientChangeScene(msg.sceneName, msg.sceneOperation, msg.customHandling);
            }
        }

        #endregion

        #region Server System Callbacks

        /// <summary>
        /// Called on the server when a new client connects.
        /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerConnect(NetworkConnection conn) { }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerDisconnect(NetworkConnection conn)
        {
            NetworkServer.DestroyPlayerForConnection(conn);
            if (LogFilter.Debug) Debug.Log("OnServerDisconnect: Client disconnected.");
        }

        /// <summary>
        /// Called on the server when a client is ready.
        /// <para>The default implementation of this function calls NetworkServer.SetClientReady() to continue the network setup process.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerReady(NetworkConnection conn)
        {
            if (conn.identity == null)
            {
                // this is now allowed (was not for a while)
                if (LogFilter.Debug) Debug.Log("Ready with no player object");
            }
            NetworkServer.SetClientReady(conn);
        }

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerAddPlayer(NetworkConnection conn)
        {
            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        /// <summary>
        /// This finds a spawn position based on NetworkStartPosition objects in the scene.
        /// <para>This is used by the default implementation of OnServerAddPlayer.</para>
        /// </summary>
        /// <returns>Returns the transform to spawn a player at, or null.</returns>
        public Transform GetStartPosition()
        {
            // first remove any dead transforms
            startPositions.RemoveAll(t => t == null);

            if (startPositions.Count == 0)
                return null;

            if (playerSpawnMethod == PlayerSpawnMethod.Random)
            {
                return startPositions[UnityEngine.Random.Range(0, startPositions.Count)];
            }
            else
            {
                Transform startPosition = startPositions[startPositionIndex];
                startPositionIndex = (startPositionIndex + 1) % startPositions.Count;
                return startPosition;
            }
        }

        // Deprecated 5/2/2020
        /// <summary>
        /// Obsolete: Removed as a security risk. Use <see cref="NetworkServer.RemovePlayerForConnection(NetworkConnection, GameObject, bool)"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Removed as a security risk. Use NetworkServer.RemovePlayerForConnection(NetworkConnection conn, GameObject player, bool keepAuthority = false) instead", true)]
        public virtual void OnServerRemovePlayer(NetworkConnection conn, NetworkIdentity player) { }

        /// <summary>
        /// Called on the server when a network error occurs for a client connection.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        /// <param name="errorCode">Error code.</param>
        public virtual void OnServerError(NetworkConnection conn, int errorCode) { }

        /// <summary>
        /// Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        public virtual void OnServerChangeScene(string newSceneName) { }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ServerChangeScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        public virtual void OnServerSceneChanged(string sceneName) { }

        #endregion

        #region Client System Callbacks

        /// <summary>
        /// Called on the client when connected to a server.
        /// <para>The default implementation of this function sets the client as ready and adds a player. Override the function to dictate what happens when the client connects.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        public virtual void OnClientConnect(NetworkConnection conn)
        {
            // OnClientConnect by default calls AddPlayer but it should not do
            // that when we have online/offline scenes. so we need the
            // clientLoadedScene flag to prevent it.
            if (!clientLoadedScene)
            {
                // Ready/AddPlayer is usually triggered by a scene load completing. if no scene was loaded, then Ready/AddPlayer it here instead.
                if (!ClientScene.ready) ClientScene.Ready(conn);
                if (autoCreatePlayer)
                {
                    ClientScene.AddPlayer(conn);
                }
            }
        }

        /// <summary>
        /// Called on clients when disconnected from a server.
        /// <para>This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        public virtual void OnClientDisconnect(NetworkConnection conn)
        {
            StopClient();
        }

        /// <summary>
        /// Called on clients when a network error occurs.
        /// </summary>
        /// <param name="conn">Connection to a server.</param>
        /// <param name="errorCode">Error code.</param>
        public virtual void OnClientError(NetworkConnection conn, int errorCode) { }

        /// <summary>
        /// Called on clients when a servers tells the client it is no longer ready.
        /// <para>This is commonly used when switching scenes.</para>
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        public virtual void OnClientNotReady(NetworkConnection conn) { }

        // Deprecated 12/22/2019
        /// <summary>
        /// Obsolete: Use <see cref="OnClientChangeScene(string, SceneOperation, bool)"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Override OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) instead", true)]
        public virtual void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation)
        {
            OnClientChangeScene(newSceneName, sceneOperation, false);
        }

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        /// <param name="customHandling">true to indicate that scene loading will be handled through overrides</param>
        public virtual void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        /// <param name="conn">The network connection that the scene change message arrived on.</param>
        public virtual void OnClientSceneChanged(NetworkConnection conn)
        {
            // always become ready.
            if (!ClientScene.ready) ClientScene.Ready(conn);

            // Only call AddPlayer for normal scene changes, not additive load/unload
            if (clientSceneOperation == SceneOperation.Normal && autoCreatePlayer && ClientScene.localPlayer == null)
            {
                // add player if existing one is null
                ClientScene.AddPlayer(conn);
            }
        }

        #endregion

        #region Start & Stop callbacks

        // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
        // their functionality, users would need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

        /// <summary>
        /// This is invoked when a host is started.
        /// <para>StartHost has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public virtual void OnStartHost() { }

        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public virtual void OnStartServer() { }

        /// <summary>
        /// This is invoked when the client is started.
        /// </summary>
        public virtual void OnStartClient() { }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public virtual void OnStopServer() { }

        /// <summary>
        /// This is called when a client is stopped.
        /// </summary>
        public virtual void OnStopClient() { }

        /// <summary>
        /// This is called when a host is stopped.
        /// </summary>
        public virtual void OnStopHost() { }

        #endregion
    }
}
