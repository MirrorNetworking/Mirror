using System;
using System.Collections.Generic;
using System.Linq;
using kcp2k;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    public enum PlayerSpawnMethod { Random, RoundRobin }
    public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly, Host }

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkManager")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-manager")]
    public class NetworkManager : MonoBehaviour
    {
        /// <summary>Enable to keep NetworkManager alive when changing scenes.</summary>
        // This should be set if your game has a single NetworkManager that exists for the lifetime of the process. If there is a NetworkManager in each scene, then this should not be set.</para>
        [Header("Configuration")]
        [FormerlySerializedAs("m_DontDestroyOnLoad")]
        [Tooltip("Should the Network Manager object be persisted through scene changes?")]
        public bool dontDestroyOnLoad = true;

        // Deprecated 2021-03-10
        // Temporary bool to allow Network Manager to persist to offline scene
        // Based on Discord convo, BigBox is invoking StopHost in startup sequence, bouncing the server and clients back to offline scene, which resets Network Manager.
        // Request is for a checkbox to persist Network Manager to offline scene, despite the collision and warning.
        [Obsolete("This was added temporarily and will be removed in a future release.")]
        [Tooltip("Should the Network Manager object be persisted through scene change to the offline scene?")]
        public bool PersistNetworkManagerToOfflineScene;

        /// <summary>Multiplayer games should always run in the background so the network doesn't time out.</summary>
        [FormerlySerializedAs("m_RunInBackground")]
        [Tooltip("Multiplayer games should always run in the background so the network doesn't time out.")]
        public bool runInBackground = true;

        /// <summary>Should the server auto-start when 'Server Build' is checked in build settings</summary>
        [Tooltip("Should the server auto-start when 'Server Build' is checked in build settings")]
        [FormerlySerializedAs("startOnHeadless")]
        public bool autoStartServerBuild = true;

        /// <summary>Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.</summary>
        [Tooltip("Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        public int serverTickRate = 30;

        /// <summary>batch messages and send them out in LateUpdate (or after batchInterval)</summary>
        [Tooltip("Batch message and send them out in LateUpdate (or after batchInterval). This is pretty much always a good idea.")]
        public bool serverBatching = true;

        /// <summary>Automatically switch to this scene upon going offline (on start / on disconnect / on shutdown).</summary>
        [Header("Scene Management")]
        [Scene]
        [FormerlySerializedAs("m_OfflineScene")]
        [Tooltip("Scene that Mirror will switch to when the client or server is stopped")]
        public string offlineScene = "";

        /// <summary>Automatically switch to this scene upon going online (after connect/startserver).</summary>
        [Scene]
        [FormerlySerializedAs("m_OnlineScene")]
        [Tooltip("Scene that Mirror will switch to when the server is started. Clients will recieve a Scene Message to load the server's current scene when they connect.")]
        public string onlineScene = "";

        // transport layer
        [Header("Network Info")]
        [Tooltip("Transport component attached to this object that server and client will use to connect")]
        [SerializeField]
        protected Transport transport;

        /// <summary>Server's address for clients to connect to.</summary>
        [FormerlySerializedAs("m_NetworkAddress")]
        [Tooltip("Network Address where the client should connect to the server. Server does not use this for anything.")]
        public string networkAddress = "localhost";

        /// <summary>The maximum number of concurrent network connections to support.</summary>
        [FormerlySerializedAs("m_MaxConnections")]
        [Tooltip("Maximum number of concurrent connections.")]
        public int maxConnections = 100;

        [Obsolete("Transport is responsible for timeouts.")]
        public bool disconnectInactiveConnections;

        [Obsolete("Transport is responsible for timeouts. Configure the Transport's timeout setting instead.")]
        public float disconnectInactiveTimeout = 60f;

        [Header("Authentication")]
        [Tooltip("Authentication component attached to this object")]
        public NetworkAuthenticator authenticator;

        /// <summary>The default prefab to be used to create player objects on the server.</summary>
        // Player objects are created in the default handler for AddPlayer() on
        // the server. Implementing OnServerAddPlayer overrides this behaviour.
        [Header("Player Object")]
        [FormerlySerializedAs("m_PlayerPrefab")]
        [Tooltip("Prefab of the player object. Prefab must have a Network Identity component. May be an empty game object or a full avatar.")]
        public GameObject playerPrefab;

        /// <summary>Enable to automatically create player objects on connect and on scene change.</summary>
        [FormerlySerializedAs("m_AutoCreatePlayer")]
        [Tooltip("Should Mirror automatically spawn the player after scene change?")]
        public bool autoCreatePlayer = true;

        /// <summary>Where to spawn players.</summary>
        [FormerlySerializedAs("m_PlayerSpawnMethod")]
        [Tooltip("Round Robin or Random order of Start Position selection")]
        public PlayerSpawnMethod playerSpawnMethod;

        /// <summary>Prefabs that can be spawned over the network need to be registered here.</summary>
        [FormerlySerializedAs("m_SpawnPrefabs"), HideInInspector]
        public List<GameObject> spawnPrefabs = new List<GameObject>();

        /// <summary>List of transforms populated by NetworkStartPositions</summary>
        public static List<Transform> startPositions = new List<Transform>();
        public static int startPositionIndex;

        /// <summary>The one and only NetworkManager</summary>
        public static NetworkManager singleton { get; private set; }

        /// <summary>Number of active player objects across all connections on the server.</summary>
        public int numPlayers => NetworkServer.connections.Count(kv => kv.Value.identity != null);

        /// <summary>True if the server is running or client is connected/connecting.</summary>
        [NonSerialized]
        public bool isNetworkActive;

        // TODO remove this
        static NetworkConnection clientReadyConnection;

        /// <summary>True if the client loaded a new scene when connecting to the server.</summary>
        // This is set before OnClientConnect is called, so it can be checked
        // there to perform different logic if a scene load occurred.
        [NonSerialized]
        public bool clientLoadedScene;

        // helper enum to know if we started the networkmanager as server/client/host.
        // -> this is necessary because when StartHost changes server scene to
        //    online scene, FinishLoadScene is called and the host client isn't
        //    connected yet (no need to connect it before server was fully set up).
        //    in other words, we need this to know which mode we are running in
        //    during FinishLoadScene.
        public NetworkManagerMode mode { get; private set; }

        // virtual so that inheriting classes' OnValidate() can call base.OnValidate() too
        public virtual void OnValidate()
        {
            // add transport if there is none yet. makes upgrading easier.
            if (transport == null)
            {
                // was a transport added yet? if not, add one
                transport = GetComponent<Transport>();
                if (transport == null)
                {
                    transport = gameObject.AddComponent<KcpTransport>();
                    Debug.Log("NetworkManager: added default Transport because there was none yet.");
                }
#if UNITY_EDITOR
                // For some insane reason, this line fails when building unless wrapped in this define. Stupid but true.
                // error CS0234: The type or namespace name 'Undo' does not exist in the namespace 'UnityEditor' (are you missing an assembly reference?)
                UnityEditor.Undo.RecordObject(gameObject, "Added default Transport");
#endif
            }

            // always >= 0
            maxConnections = Mathf.Max(maxConnections, 0);

            if (playerPrefab != null && playerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError("NetworkManager - Player Prefab must have a NetworkIdentity.");
                playerPrefab = null;
            }

            // This avoids the mysterious "Replacing existing prefab with assetId ... Old prefab 'Player', New prefab 'Player'" warning.
            if (playerPrefab != null && spawnPrefabs.Contains(playerPrefab))
            {
                Debug.LogWarning("NetworkManager - Player Prefab should not be added to Registered Spawnable Prefabs list...removed it.");
                spawnPrefabs.Remove(playerPrefab);
            }
        }

        // virtual so that inheriting classes' Awake() can call base.Awake() too
        public virtual void Awake()
        {
            // Don't allow collision-destroyed second instance to continue.
            if (!InitializeSingleton()) return;

            Debug.Log("Mirror | mirror-networking.com | discord.gg/N9QVxbM");

            // Set the networkSceneName to prevent a scene reload
            // if client connection to server fails.
            networkSceneName = offlineScene;

            // setup OnSceneLoaded callback
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // virtual so that inheriting classes' Start() can call base.Start() too
        public virtual void Start()
        {
            // headless mode? then start the server
            // can't do this in Awake because Awake is for initialization.
            // some transports might not be ready until Start.
            //
            // (tick rate is applied in StartServer!)
#if UNITY_SERVER
            if (autoStartServerBuild)
            {
                StartServer();
            }
#endif
        }

        // virtual so that inheriting classes' LateUpdate() can call base.LateUpdate() too
        public virtual void LateUpdate()
        {
            UpdateScene();
        }

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
            // Debug.Log("NetworkManager SetupServer");
            InitializeSingleton();

            if (runInBackground)
                Application.runInBackground = true;

            if (authenticator != null)
            {
                authenticator.OnStartServer();
                authenticator.OnServerAuthenticated.AddListener(OnServerAuthenticated);
            }

            ConfigureServerFrameRate();

            // batching
            NetworkServer.batching = serverBatching;

            // Copy auto-disconnect settings to NetworkServer
#pragma warning disable 618
            NetworkServer.disconnectInactiveTimeout = disconnectInactiveTimeout;
            NetworkServer.disconnectInactiveConnections = disconnectInactiveConnections;
#pragma warning restore 618

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

        /// <summary>Starts the server, listening for incoming connections.</summary>
        public void StartServer()
        {
            if (NetworkServer.active)
            {
                Debug.LogWarning("Server already started.");
                return;
            }

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

        /// <summary>Starts the client, connects it to the server with networkAddress.</summary>
        public void StartClient()
        {
            if (NetworkClient.active)
            {
                Debug.LogWarning("Client already started.");
                return;
            }

            mode = NetworkManagerMode.ClientOnly;

            InitializeSingleton();

            if (runInBackground)
                Application.runInBackground = true;

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated);
            }

            isNetworkActive = true;

            // In case this is a headless client...
            ConfigureServerFrameRate();

            RegisterClientMessages();

            if (string.IsNullOrEmpty(networkAddress))
            {
                Debug.LogError("Must set the Network Address field in the manager");
                return;
            }
            // Debug.Log("NetworkManager StartClient address:" + networkAddress);

            NetworkClient.Connect(networkAddress);

            OnStartClient();
        }

        /// <summary>Starts the client, connects it to the server via Uri</summary>
        public void StartClient(Uri uri)
        {
            if (NetworkClient.active)
            {
                Debug.LogWarning("Client already started.");
                return;
            }

            mode = NetworkManagerMode.ClientOnly;

            InitializeSingleton();

            if (runInBackground)
                Application.runInBackground = true;

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated);
            }

            isNetworkActive = true;

            RegisterClientMessages();

            // Debug.Log("NetworkManager StartClient address:" + uri);
            networkAddress = uri.Host;

            NetworkClient.Connect(uri);

            OnStartClient();
        }

        /// <summary>Starts a network "host" - a server and client in the same application.</summary>
        public void StartHost()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("Server or Client already started.");
                return;
            }

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
            //Debug.Log("StartHostClient called");
            StartHostClient();
        }

        void StartHostClient()
        {
            //Debug.Log("NetworkManager ConnectLocalClient");

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

        /// <summary>This stops both the client and the server that the manager is using.</summary>
        public void StopHost()
        {
            OnStopHost();

            // calling OnTransportDisconnected was needed to fix
            // https://github.com/vis2k/Mirror/issues/1515
            // so that the host client receives a DisconnectMessage
            // TODO reevaluate if this is still needed after all the disconnect
            //      fixes, and try to put this into LocalConnection.Disconnect!
            NetworkServer.OnTransportDisconnected(NetworkConnection.LocalConnectionId);

            StopClient();
            StopServer();
        }

        /// <summary>Stops the server from listening and simulating the game.</summary>
        public void StopServer()
        {
            if (!NetworkServer.active)
                return;

            if (authenticator != null)
            {
                authenticator.OnServerAuthenticated.RemoveListener(OnServerAuthenticated);
                authenticator.OnStopServer();
            }

            // Get Network Manager out of DDOL before going to offline scene
            // to avoid collision and let a fresh Network Manager be created.
            // IMPORTANT: .gameObject can be null if StopClient is called from
            //            OnApplicationQuit or from tests!
#pragma warning disable 618
            if (gameObject != null && !PersistNetworkManagerToOfflineScene &&
                gameObject.scene.name == "DontDestroyOnLoad"
                && !string.IsNullOrEmpty(offlineScene)
                && SceneManager.GetActiveScene().path != offlineScene)
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
#pragma warning restore 618

            OnStopServer();

            //Debug.Log("NetworkManager StopServer");
            isNetworkActive = false;
            NetworkServer.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;

            if (!string.IsNullOrEmpty(offlineScene))
            {
                ServerChangeScene(offlineScene);
            }

            startPositionIndex = 0;

            networkSceneName = "";
        }

        /// <summary>Stops and disconnects the client.</summary>
        public void StopClient()
        {
            if (authenticator != null)
            {
                authenticator.OnClientAuthenticated.RemoveListener(OnClientAuthenticated);
                authenticator.OnStopClient();
            }

            // Get Network Manager out of DDOL before going to offline scene
            // to avoid collision and let a fresh Network Manager be created.
            // IMPORTANT: .gameObject can be null if StopClient is called from
            //            OnApplicationQuit or from tests!
#pragma warning disable 618
            if (gameObject != null && !PersistNetworkManagerToOfflineScene &&
                gameObject.scene.name == "DontDestroyOnLoad"
                && !string.IsNullOrEmpty(offlineScene)
                && SceneManager.GetActiveScene().path != offlineScene)
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
#pragma warning restore 618

            OnStopClient();

            //Debug.Log("NetworkManager StopClient");
            isNetworkActive = false;

            // shutdown client
            NetworkClient.Disconnect();
            NetworkClient.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;

            // If this is the host player, StopServer will already be changing scenes.
            // Check loadingSceneAsync to ensure we don't double-invoke the scene change.
            // Check if NetworkServer.active because we can get here via Disconnect before server has started to change scenes.
            if (!string.IsNullOrEmpty(offlineScene) && !IsSceneActive(offlineScene) && loadingSceneAsync == null && !NetworkServer.active)
            {
                ClientChangeScene(offlineScene, SceneOperation.Normal);
            }

            networkSceneName = "";
        }

        // called when quitting the application by closing the window / pressing
        // stop in the editor. virtual so that inheriting classes'
        // OnApplicationQuit() can call base.OnApplicationQuit() too
        public virtual void OnApplicationQuit()
        {
            // stop client first
            // (we want to send the quit packet to the server instead of waiting
            //  for a timeout)
            if (NetworkClient.isConnected)
            {
                StopClient();
                //Debug.Log("OnApplicationQuit: stopped client");
            }

            // stop server after stopping client (for proper host mode stopping)
            if (NetworkServer.active)
            {
                StopServer();
                //Debug.Log("OnApplicationQuit: stopped server");
            }
        }

        /// <summary>Set the frame rate for a headless server. Override to disable or modify.</summary>
        public virtual void ConfigureServerFrameRate()
        {
            // only set framerate for server build
#if UNITY_SERVER
            Application.targetFrameRate = serverTickRate;
            // Debug.Log("Server Tick Rate set to: " + Application.targetFrameRate + " Hz.");
#endif
        }

        bool InitializeSingleton()
        {
            if (singleton != null && singleton == this)
                return true;

            if (dontDestroyOnLoad)
            {
                if (singleton != null)
                {
                    Debug.LogWarning("Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time. The duplicate NetworkManager will be destroyed.");
                    Destroy(gameObject);

                    // Return false to not allow collision-destroyed second instance to continue.
                    return false;
                }
                Debug.Log("NetworkManager created singleton (DontDestroyOnLoad)");
                singleton = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.Log("NetworkManager created singleton (ForScene)");
                singleton = this;
            }

            // set active transport AFTER setting singleton.
            // so only if we didn't destroy ourselves.
            Transport.activeTransport = transport;
            return true;
        }

        void RegisterServerMessages()
        {
            NetworkServer.OnConnectedEvent = OnServerConnectInternal;
            NetworkServer.OnDisconnectedEvent = OnServerDisconnect;
            NetworkServer.RegisterHandler<AddPlayerMessage>(OnServerAddPlayerInternal);

            // Network Server initially registers its own handler for this, so we replace it here.
            NetworkServer.ReplaceHandler<ReadyMessage>(OnServerReadyMessageInternal);
        }

        void RegisterClientMessages()
        {
            NetworkClient.OnConnectedEvent = OnClientConnectInternal;
            NetworkClient.OnDisconnectedEvent = OnClientDisconnectInternal;
            NetworkClient.RegisterHandler<NotReadyMessage>(OnClientNotReadyMessageInternal);
            NetworkClient.RegisterHandler<SceneMessage>(OnClientSceneInternal, false);

            if (playerPrefab != null)
                NetworkClient.RegisterPrefab(playerPrefab);

            foreach (GameObject prefab in spawnPrefabs.Where(t => t != null))
                NetworkClient.RegisterPrefab(prefab);
        }

        // This is the only way to clear the singleton, so another instance can be created.
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

        // virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        public virtual void OnDestroy()
        {
            //Debug.Log("NetworkManager destroyed");
        }

        /// <summary>The name of the current network scene.</summary>
        // set by NetworkManager when changing the scene.
        // new clients will automatically load this scene.
        // Loading a scene manually won't set it.
        public static string networkSceneName { get; protected set; } = "";

        public static AsyncOperation loadingSceneAsync;

        /// <summary>Change the server scene and all client's scenes across the network.</summary>
        // Called automatically if onlineScene or offlineScene are set, but it
        // can be called from user code to switch scenes again while the game is
        // in progress. This automatically sets clients to be not-ready during
        // the change and ready again to participate in the new scene.
        public virtual void ServerChangeScene(string newSceneName)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                Debug.LogError("ServerChangeScene empty scene name");
                return;
            }

            // Debug.Log("ServerChangeScene " + newSceneName);
            NetworkServer.SetAllClientsNotReady();
            networkSceneName = newSceneName;

            // Let server prepare for scene change
            OnServerChangeScene(newSceneName);

            // Suspend the server's transport while changing scenes
            // It will be re-enabled in FinishLoadScene.
            Transport.activeTransport.enabled = false;

            loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);

            // ServerChangeScene can be called when stopping the server
            // when this happens the server is not active so does not need to tell clients about the change
            if (NetworkServer.active)
            {
                // notify all clients about the new scene
                NetworkServer.SendToAll(new SceneMessage { sceneName = newSceneName });
            }

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

            // Debug.Log("ClientChangeScene newSceneName:" + newSceneName + " networkSceneName:" + networkSceneName);

            // vis2k: pause message handling while loading scene. otherwise we will process messages and then lose all
            // the state as soon as the load is finishing, causing all kinds of bugs because of missing state.
            // (client may be null after StopClient etc.)
            // Debug.Log("ClientChangeScene: pausing handlers while scene is loading to avoid data loss after scene was loaded.");
            Transport.activeTransport.enabled = false;

            // Cache sceneOperation so we know what was requested by the
            // Scene message in OnClientChangeScene and OnClientSceneChanged
            clientSceneOperation = sceneOperation;

            // Let client prepare for scene change
            OnClientChangeScene(newSceneName, sceneOperation, customHandling);

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
        //   * NetworkClient.PrepareToSpawnSceneObjects enables them again on the
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
                    // Debug.Log("Respawned Server objects after additive scene load: " + scene.name);
                }
                if (NetworkClient.active)
                {
                    NetworkClient.PrepareToSpawnSceneObjects();
                    // Debug.Log("Rebuild Client spawnableObjects after additive scene load: " + scene.name);
                }
            }
        }

        void UpdateScene()
        {
            if (loadingSceneAsync != null && loadingSceneAsync.isDone)
            {
                // Debug.Log("ClientChangeScene done readyCon:" + clientReadyConnection);

                // try-finally to guarantee loadingSceneAsync being cleared.
                // fixes https://github.com/vis2k/Mirror/issues/2517 where if
                // FinishLoadScene throws an exception, loadingSceneAsync would
                // never be cleared and this code would run every Update.
                try
                {
                    FinishLoadScene();
                }
                finally
                {
                    loadingSceneAsync.allowSceneActivation = true;
                    loadingSceneAsync = null;
                }
            }
        }

        protected void FinishLoadScene()
        {
            // NOTE: this cannot use NetworkClient.allClients[0] - that client may be for a completely different purpose.

            // process queued messages that we received while loading the scene
            Debug.Log("FinishLoadScene: resuming handlers after scene was loading.");
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
            Debug.Log("Finished loading scene in host mode.");

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

                // DO NOT call OnClientSceneChanged here.
                // the scene change happened because StartHost loaded the
                // server's online scene. it has nothing to do with the client.
                // this was not meant as a client scene load, so don't call it.
                //
                // otherwise AddPlayer would be called twice:
                // -> once for client OnConnected
                // -> once in OnClientSceneChanged
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
                    // let client know that we changed scene
                    OnClientSceneChanged(NetworkClient.connection);
                }
            }
        }

        // finish load scene part for server-only. . makes code easier and is
        // necessary for FinishStartServer later.
        void FinishLoadSceneServerOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            Debug.Log("Finished loading scene in server-only mode.");

            NetworkServer.SpawnObjects();
            OnServerSceneChanged(networkSceneName);
        }

        // finish load scene part for client-only. makes code easier and is
        // necessary for FinishStartClient later.
        void FinishLoadSceneClientOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            Debug.Log("Finished loading scene in client-only mode.");

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

        /// <summary>
        /// Registers the transform of a game object as a player spawn location.
        /// <para>This is done automatically by NetworkStartPosition components, but can be done manually from user script code.</para>
        /// </summary>
        /// <param name="start">Transform to register.</param>
        public static void RegisterStartPosition(Transform start)
        {
            // Debug.Log("RegisterStartPosition: (" + start.gameObject.name + ") " + start.position);
            startPositions.Add(start);

            // reorder the list so that round-robin spawning uses the start positions
            // in hierarchy order.  This assumes all objects with NetworkStartPosition
            // component are siblings, either in the scene root or together as children
            // under a single parent in the scene.
            startPositions = startPositions.OrderBy(transform => transform.GetSiblingIndex()).ToList();
        }

        /// <summary>Unregister a Transform from start positions.</summary>
        // TODO why is this static?
        public static void UnRegisterStartPosition(Transform start)
        {
            // Debug.Log("UnRegisterStartPosition: (" + start.gameObject.name + ") " + start.position);
            startPositions.Remove(start);
        }

        /// <summary>Get the next NetworkStartPosition based on the selected PlayerSpawnMethod.</summary>
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

        void OnServerConnectInternal(NetworkConnection conn)
        {
            //Debug.Log("NetworkManager.OnServerConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnServerAuthenticate(conn);
            }
            else
            {
                // authenticate immediately
                OnServerAuthenticated(conn);
            }
        }

        // called after successful authentication
        // TODO do the NetworkServer.OnAuthenticated thing from x branch
        void OnServerAuthenticated(NetworkConnection conn)
        {
            //Debug.Log("NetworkManager.OnServerAuthenticated");

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

        void OnServerReadyMessageInternal(NetworkConnection conn, ReadyMessage msg)
        {
            //Debug.Log("NetworkManager.OnServerReadyMessageInternal");
            OnServerReady(conn);
        }

        void OnServerAddPlayerInternal(NetworkConnection conn, AddPlayerMessage msg)
        {
            //Debug.Log("NetworkManager.OnServerAddPlayer");

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

        void OnClientConnectInternal()
        {
            //Debug.Log("NetworkManager.OnClientConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnClientAuthenticate();
            }
            else
            {
                // authenticate immediately
                OnClientAuthenticated(NetworkClient.connection);
            }
        }

        // called after successful authentication
        void OnClientAuthenticated(NetworkConnection conn)
        {
            //Debug.Log("NetworkManager.OnClientAuthenticated");

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

        // TODO call OnClientDisconnect directly, don't pass the connection
        void OnClientDisconnectInternal()
        {
            //Debug.Log("NetworkManager.OnClientDisconnectInternal");
            OnClientDisconnect(NetworkClient.connection);
        }

        void OnClientNotReadyMessageInternal(NotReadyMessage msg)
        {
            //Debug.Log("NetworkManager.OnClientNotReadyMessageInternal");
            NetworkClient.ready = false;
            OnClientNotReady(NetworkClient.connection);
            // NOTE: clientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        void OnClientSceneInternal(SceneMessage msg)
        {
            //Debug.Log("NetworkManager.OnClientSceneInternal");
            if (NetworkClient.isConnected && !NetworkServer.active)
            {
                ClientChangeScene(msg.sceneName, msg.sceneOperation, msg.customHandling);
            }
        }

        /// <summary>Called on the server when a new client connects.</summary>
        public virtual void OnServerConnect(NetworkConnection conn) {}

        /// <summary>Called on the server when a client disconnects.</summary>
        // Called by NetworkServer.OnTransportDisconnect!
        public virtual void OnServerDisconnect(NetworkConnection conn)
        {
            // by default, this function destroys the connection's player.
            // can be overwritten for cases like delayed logouts in MMOs to
            // avoid players escaping from PvP situations by logging out.
            NetworkServer.DestroyPlayerForConnection(conn);
            //Debug.Log("OnServerDisconnect: Client disconnected.");
        }

        /// <summary>Called on the server when a client is ready (= loaded the scene)</summary>
        public virtual void OnServerReady(NetworkConnection conn)
        {
            if (conn.identity == null)
            {
                // this is now allowed (was not for a while)
                //Debug.Log("Ready with no player object");
            }
            NetworkServer.SetClientReady(conn);
        }

        /// <summary>Called on server when a client requests to add the player. Adds playerPrefab by default. Can be overwritten.</summary>
        // The default implementation for this function creates a new player object from the playerPrefab.
        public virtual void OnServerAddPlayer(NetworkConnection conn)
        {
            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            // instantiating a "Player" prefab gives it the name "Player(clone)"
            // => appending the connectionId is WAY more useful for debugging!
            player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
            NetworkServer.AddPlayerForConnection(conn, player);
        }

        [Obsolete("OnServerError was removed because it hasn't been used in a long time.")]
        public virtual void OnServerError(NetworkConnection conn, int errorCode) {}

        /// <summary>Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed</summary>
        public virtual void OnServerChangeScene(string newSceneName) {}

        /// <summary>Called on server after a scene load with ServerChangeScene() is completed.</summary>
        public virtual void OnServerSceneChanged(string sceneName) {}

        /// <summary>Called on the client when connected to a server. By default it sets client as ready and adds a player.</summary>
        // TODO client only ever uses NetworkClient.connection. this parameter is redundant.
        public virtual void OnClientConnect(NetworkConnection conn)
        {
            // OnClientConnect by default calls AddPlayer but it should not do
            // that when we have online/offline scenes. so we need the
            // clientLoadedScene flag to prevent it.
            if (!clientLoadedScene)
            {
                // Ready/AddPlayer is usually triggered by a scene load
                // completing. if no scene was loaded, then Ready/AddPlayer it
                // here instead.
                if (!NetworkClient.ready) NetworkClient.Ready();
                if (autoCreatePlayer)
                {
                    NetworkClient.AddPlayer();
                }
            }
        }

        /// <summary>Called on clients when disconnected from a server.</summary>
        // TODO client only ever uses NetworkClient.connection. this parameter is redundant.
        public virtual void OnClientDisconnect(NetworkConnection conn)
        {
            StopClient();
        }

        [Obsolete("OnClientError was removed because it hasn't been used in a long time.")]
        public virtual void OnClientError(NetworkConnection conn, int errorCode) {}

        /// <summary>Called on clients when a servers tells the client it is no longer ready, e.g. when switching scenes.</summary>
        // TODO client only ever uses NetworkClient.connection. this parameter is redundant.
        public virtual void OnClientNotReady(NetworkConnection conn) {}

        /// <summary>Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed</summary>
        // customHandling: indicates if scene loading will be handled through overrides
        public virtual void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) {}

        /// <summary>Called on clients when a scene has completed loaded, when the scene load was initiated by the server.</summary>
        // Scene changes can cause player objects to be destroyed. The default
        // implementation of OnClientSceneChanged in the NetworkManager is to
        // add a player object for the connection if no player object exists.
        // TODO client only ever uses NetworkClient.connection. this parameter is redundant.
        public virtual void OnClientSceneChanged(NetworkConnection conn)
        {
            // always become ready.
            if (!NetworkClient.ready) NetworkClient.Ready();

            // Only call AddPlayer for normal scene changes, not additive load/unload
            if (clientSceneOperation == SceneOperation.Normal && autoCreatePlayer && NetworkClient.localPlayer == null)
            {
                // add player if existing one is null
                NetworkClient.AddPlayer();
            }
        }

        // Since there are multiple versions of StartServer, StartClient and
        // StartHost, to reliably customize their functionality, users would
        // need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

        /// <summary>This is invoked when a host is started.</summary>
        public virtual void OnStartHost() {}

        /// <summary>This is invoked when a server is started - including when a host is started.</summary>
        public virtual void OnStartServer() {}

        /// <summary>This is invoked when the client is started.</summary>
        public virtual void OnStartClient() {}

        /// <summary>This is called when a server is stopped - including when a host is stopped.</summary>
        public virtual void OnStopServer() {}

        /// <summary>This is called when a client is stopped.</summary>
        public virtual void OnStopClient() {}

        /// <summary>This is called when a host is stopped.</summary>
        public virtual void OnStopHost() {}
    }
}
