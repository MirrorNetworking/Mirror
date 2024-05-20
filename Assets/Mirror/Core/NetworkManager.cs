using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    public enum PlayerSpawnMethod { Random, RoundRobin }
    public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly, Host }
    public enum HeadlessStartOptions { DoNothing, AutoStartServer, AutoStartClient }

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/Network Manager")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-manager")]
    public class NetworkManager : MonoBehaviour
    {
        /// <summary>Enable to keep NetworkManager alive when changing scenes.</summary>
        // This should be set if your game has a single NetworkManager that exists for the lifetime of the process. If there is a NetworkManager in each scene, then this should not be set.</para>
        [Header("Configuration")]
        [FormerlySerializedAs("m_DontDestroyOnLoad")]
        [Tooltip("Should the Network Manager object be persisted through scene changes?")]
        public bool dontDestroyOnLoad = true;

        /// <summary>Multiplayer games should always run in the background so the network doesn't time out.</summary>
        [FormerlySerializedAs("m_RunInBackground")]
        [Tooltip("Multiplayer games should always run in the background so the network doesn't time out.")]
        public bool runInBackground = true;

        /// <summary>Should the server auto-start when 'Server Build' is checked in build settings</summary>
        [Header("Auto-Start Options")]

        [Tooltip("Choose whether Server or Client should auto-start in headless builds")]
        public HeadlessStartOptions headlessStartMode = HeadlessStartOptions.DoNothing;

        [Tooltip("Headless Start Mode in Editor\nwhen enabled, headless start mode will be used in editor as well.")]
        public bool editorAutoStart;

        /// <summary>Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.</summary>
        [Tooltip("Server / Client send rate per second.\nUse 60-100Hz for fast paced games like Counter-Strike to minimize latency.\nUse around 30Hz for games like WoW to minimize computations.\nUse around 1-10Hz for slow paced games like EVE.")]
        [FormerlySerializedAs("serverTickRate")]
        public int sendRate = 60;

        // Deprecated 2023-11-25
        // Using SerializeField and HideInInspector to self-correct for being
        // replaced by headlessStartMode. This can be removed in the future.
        // See OnValidate() for how we handle this.
        [Obsolete("Deprecated - Use headlessStartMode instead.")]
        [FormerlySerializedAs("autoStartServerBuild"), SerializeField, HideInInspector]
        public bool autoStartServerBuild = true;

        // Deprecated 2023-11-25
        // Using SerializeField and HideInInspector to self-correct for being
        // replaced by headlessStartMode. This can be removed in the future.
        // See OnValidate() for how we handle this.
        [Obsolete("Deprecated - Use headlessStartMode instead.")]
        [FormerlySerializedAs("autoConnectClientBuild"), SerializeField, HideInInspector]
        public bool autoConnectClientBuild;

        // client send rate follows server send rate to avoid errors for now
        /// <summary>Client Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.</summary>
        // [Tooltip("Client broadcasts 'sendRate' times per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        // public int clientSendRate = 30; // 33 ms

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
        public Transport transport;

        /// <summary>Server's address for clients to connect to.</summary>
        [FormerlySerializedAs("m_NetworkAddress")]
        [Tooltip("Network Address where the client should connect to the server. Server does not use this for anything.")]
        public string networkAddress = "localhost";

        /// <summary>The maximum number of concurrent network connections to support.</summary>
        [FormerlySerializedAs("m_MaxConnections")]
        [Tooltip("Maximum number of concurrent connections.")]
        public int maxConnections = 100;

        // Mirror global disconnect inactive option, independent of Transport.
        // not all Transports do this properly, and it's easiest to configure this just once.
        // this is very useful for some projects, keep it.
        [Tooltip("When enabled, the server automatically disconnects inactive connections after the configured timeout.")]
        public bool disconnectInactiveConnections;

        [Tooltip("Timeout in seconds for server to automatically disconnect inactive connections if 'disconnectInactiveConnections' is enabled.")]
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

        [Header("Security")]
        [Tooltip("For security, it is recommended to disconnect a player if a networked action triggers an exception\nThis could prevent components being accessed in an undefined state, which may be an attack vector for exploits.\nHowever, some games may want to allow exceptions in order to not interrupt the player's experience.")]
        public bool exceptionsDisconnect = true; // security by default

        [Header("Snapshot Interpolation")]
        public SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings();

        [Header("Connection Quality")]
        [Tooltip("Method to use for connection quality evaluation.\nSimple: based on rtt and jitter.\nPragmatic: based on snapshot interpolation adjustment.")]
        public ConnectionQualityMethod evaluationMethod;

        [Tooltip("Interval in seconds to evaluate connection quality.\nSet to 0 to disable connection quality evaluation.")]
        [Range(0, 60)]
        [FormerlySerializedAs("connectionQualityInterval")]
        public float evaluationInterval = 3;

        [Header("Interpolation UI - Requires Editor / Dev Build")]
        public bool timeInterpolationGui = false;

        /// <summary>The one and only NetworkManager</summary>
        public static NetworkManager singleton { get; internal set; }

        /// <summary>Number of active player objects across all connections on the server.</summary>
        public int numPlayers => NetworkServer.connections.Count(kv => kv.Value.identity != null);

        /// <summary>True if the server is running or client is connected/connecting.</summary>
        public bool isNetworkActive => NetworkServer.active || NetworkClient.active;

        // TODO remove this
        // internal for tests
        internal static NetworkConnection clientReadyConnection;

        /// <summary>True if the client loaded a new scene when connecting to the server.</summary>
        // This is set before OnClientConnect is called, so it can be checked
        // there to perform different logic if a scene load occurred.
        protected bool clientLoadedScene;

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
#pragma warning disable 618
            // autoStartServerBuild and autoConnectClientBuild are now obsolete, but to avoid
            // a breaking change we'll set headlessStartMode to what the user had set before.
            //
            // headlessStartMode defaults to DoNothing, so if the user had neither of these
            // set, then it will remain as DoNothing, and if they set headlessStartMode to
            // any selection in the inspector it won't get changed back.
            if (autoStartServerBuild)
                headlessStartMode = HeadlessStartOptions.AutoStartServer;
            else if (autoConnectClientBuild)
                headlessStartMode = HeadlessStartOptions.AutoStartClient;

            // Setting both to false here prevents this code from fighting with user
            // selection in the inspector, and they're both SerialisedField's.
            autoStartServerBuild = false;
            autoConnectClientBuild = false;
#pragma warning restore 618

            // always >= 0
            maxConnections = Mathf.Max(maxConnections, 0);

            if (playerPrefab != null && !playerPrefab.TryGetComponent(out NetworkIdentity _))
            {
                Debug.LogError("NetworkManager - Player Prefab must have a NetworkIdentity.");
                playerPrefab = null;
            }

            // This avoids the mysterious "Replacing existing prefab with assetId ... Old prefab 'Player', New prefab 'Player'" warning.
            if (playerPrefab != null && spawnPrefabs.Contains(playerPrefab))
            {
                Debug.LogWarning("NetworkManager - Player Prefab doesn't need to be in Spawnable Prefabs list too. Removing it.");
                spawnPrefabs.Remove(playerPrefab);
            }
        }

        // virtual so that inheriting classes' Reset() can call base.Reset() too
        // Reset only gets called when the component is added or the user resets the component
        // Thats why we validate these things that only need to be validated on adding the NetworkManager here
        // If we would do it in OnValidate() then it would run this everytime a value changes
        public virtual void Reset()
        {
            // make sure someone doesn't accidentally add another NetworkManager
            // need transform.root because when adding to a child, the parent's
            // Reset isn't called.
            foreach (NetworkManager manager in transform.root.GetComponentsInChildren<NetworkManager>())
            {
                if (manager != this)
                {
                    Debug.LogError($"{name} detected another component of type {typeof(NetworkManager)} in its hierarchy on {manager.name}. There can only be one, please remove one of them.");
                    // return early so that transport component isn't auto-added
                    // to the duplicate NetworkManager.
                    return;
                }
            }
        }

        // virtual so that inheriting classes' Awake() can call base.Awake() too
        public virtual void Awake()
        {
            // Don't allow collision-destroyed second instance to continue.
            if (!InitializeSingleton()) return;

            // Apply configuration in Awake once already
            ApplyConfiguration();

            // Set the networkSceneName to prevent a scene reload
            // if client connection to server fails.
            networkSceneName = offlineScene;

            // setup OnSceneLoaded callback
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // virtual so that inheriting classes' Start() can call base.Start() too
        public virtual void Start()
        {
            // Auto-start headless server or client.
            //
            // We can't do this in Awake because Awake is for initialization
            // and some transports might not be ready until Start.
            //
            // Auto-starting in Editor is useful for debugging, so that can
            // be enabled with editorAutoStart.
            if (Utils.IsHeadless())
            {
                if (!Application.isEditor || editorAutoStart)
                    switch (headlessStartMode)
                    {
                        case HeadlessStartOptions.AutoStartServer:
                            StartServer();
                            break;
                        case HeadlessStartOptions.AutoStartClient:
                            StartClient();
                            break;
                    }
            }
        }

        // make sure to call base.Update() when overwriting
        public virtual void Update()
        {
            ApplyConfiguration();
        }

        // virtual so that inheriting classes' LateUpdate() can call base.LateUpdate() too
        public virtual void LateUpdate()
        {
            UpdateScene();
        }

        ////////////////////////////////////////////////////////////////////////

        // keep the online scene change check in a separate function.
        // only change scene if the requested online scene is not blank, and is not already loaded.
        bool IsServerOnlineSceneChangeNeeded() =>
            !string.IsNullOrWhiteSpace(onlineScene) &&
            !Utils.IsSceneActive(onlineScene) &&
            onlineScene != offlineScene;

        // NetworkManager exposes some NetworkServer/Client configuration.
        // we apply it every Update() in order to avoid two sources of truth.
        // fixes issues where NetworkServer.sendRate was never set because
        // NetworkManager.StartServer was never called, etc.
        // => all exposed settings should be applied at all times if NM exists.
        void ApplyConfiguration()
        {
            NetworkServer.tickRate = sendRate;
            NetworkClient.snapshotSettings = snapshotSettings;
            NetworkClient.connectionQualityInterval = evaluationInterval;
            NetworkClient.connectionQualityMethod = evaluationMethod;
        }

        // full server setup code, without spawning objects yet
        void SetupServer()
        {
            // Debug.Log("NetworkManager SetupServer");
            InitializeSingleton();

            // apply settings before initializing anything
            NetworkServer.disconnectInactiveConnections = disconnectInactiveConnections;
            NetworkServer.disconnectInactiveTimeout = disconnectInactiveTimeout;
            NetworkServer.exceptionsDisconnect = exceptionsDisconnect;

            if (runInBackground)
                Application.runInBackground = true;

            if (authenticator != null)
            {
                authenticator.OnStartServer();
                authenticator.OnServerAuthenticated.AddListener(OnServerAuthenticated);
            }

            ConfigureHeadlessFrameRate();

            // start listening to network connections
            NetworkServer.Listen(maxConnections);

            // this must be after Listen(), since that registers the default message handlers
            RegisterServerMessages();

            // do not call OnStartServer here yet.
            // this is up to the caller. different for server-only vs. host mode.
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

            // call OnStartServer AFTER Listen, so that NetworkServer.active is
            // true and we can call NetworkServer.Spawn in OnStartServer
            // overrides.
            // (useful for loading & spawning stuff from database etc.)
            //
            // note: there is no risk of someone connecting after Listen() and
            //       before OnStartServer() because this all runs in one thread
            //       and we don't start processing connects until Update.
            OnStartServer();

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

        void SetupClient()
        {
            InitializeSingleton();

#pragma warning disable 618
            // Remove when OnConnectionQualityChanged is removed.
            NetworkClient.onConnectionQualityChanged += OnConnectionQualityChanged;
#pragma warning restore 618

            // apply settings before initializing anything
            NetworkClient.exceptionsDisconnect = exceptionsDisconnect;
            // NetworkClient.sendRate = clientSendRate;

            if (runInBackground)
                Application.runInBackground = true;

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated);
            }

        }

        /// <summary>Starts the client, connects it to the server with networkAddress.</summary>
        public void StartClient()
        {
            // Do checks and short circuits before setting anything up.
            // If / when we retry, we won't have conflict issues.
            if (NetworkClient.active)
            {
                Debug.LogWarning("Client already started.");
                return;
            }

            if (string.IsNullOrWhiteSpace(networkAddress))
            {
                Debug.LogError("Must set the Network Address field in the manager");
                return;
            }

            mode = NetworkManagerMode.ClientOnly;

            SetupClient();

            // In case this is a headless client...
            ConfigureHeadlessFrameRate();

            RegisterClientMessages();

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

            SetupClient();

            RegisterClientMessages();

            // Debug.Log($"NetworkManager StartClient address:{uri}");
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

            // invoke user callbacks AFTER ConnectHost has set .activeHost.
            // this way initialization can properly handle host mode.
            //
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3302
            // where [SyncVar] hooks wouldn't be called for objects spawned in
            // NetworkManager.OnStartServer, because .activeHost was still false.
            //
            // TODO is there a risk of someone connecting between Listen() and FinishStartHost()?
            OnStartServer();

            // call OnStartHost AFTER SetupServer. this way we can use
            // NetworkServer.Spawn etc. in there too. just like OnStartServer
            // is called after the server is actually properly started.
            OnStartHost();

            // server scene was loaded. now spawn all the objects
            NetworkServer.SpawnObjects();

            // connect client and call OnStartClient AFTER server scene was
            // loaded and all objects were spawned.
            // DO NOT do this earlier. it would cause race conditions where a
            // client will do things before the server is even fully started.
            //Debug.Log("StartHostClient called");
            SetupClient();
            RegisterClientMessages();

            // InvokeOnConnected needs to be called AFTER RegisterClientMessages
            // (https://github.com/vis2k/Mirror/pull/1249/)
            HostMode.InvokeOnConnected();

            OnStartClient();
        }

        /// <summary>This stops both the client and the server that the manager is using.</summary>
        public void StopHost()
        {
            OnStopHost();
            StopClient();
            StopServer();
        }

        /// <summary>Stops the server from listening and simulating the game.</summary>
        public void StopServer()
        {
            // return if already stopped to avoid recursion deadlock
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
            if (gameObject != null
                && gameObject.scene.name == "DontDestroyOnLoad"
                && !string.IsNullOrWhiteSpace(offlineScene)
                && SceneManager.GetActiveScene().path != offlineScene)
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());

            OnStopServer();

            //Debug.Log("NetworkManager StopServer");
            NetworkServer.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;

            if (!string.IsNullOrWhiteSpace(offlineScene))
            {
                ServerChangeScene(offlineScene);
            }

            startPositionIndex = 0;

            networkSceneName = "";
        }

        /// <summary>Stops and disconnects the client.</summary>
        public void StopClient()
        {
            if (mode == NetworkManagerMode.Offline)
                return;

            // For Host client, call OnServerDisconnect before NetworkClient.Disconnect
            // because we need NetworkServer.localConnection to not be null
            // NetworkClient.Disconnect will set it null.
            if (mode == NetworkManagerMode.Host)
                OnServerDisconnect(NetworkServer.localConnection);

            // ask client -> transport to disconnect.
            // handle voluntary and involuntary disconnects in OnClientDisconnect.
            //
            //   StopClient
            //     NetworkClient.Disconnect
            //       Transport.Disconnect
            //         ...
            //       Transport.OnClientDisconnect
            //     NetworkClient.OnTransportDisconnect
            //   NetworkManager.OnClientDisconnect
            NetworkClient.Disconnect();

#pragma warning disable 618
            // Remove when OnConnectionQualityChanged is removed.
            NetworkClient.onConnectionQualityChanged -= OnConnectionQualityChanged;
#pragma warning restore 618

            // UNET invoked OnDisconnected cleanup immediately.
            // let's keep it for now, in case any projects depend on it.
            // TODO simply remove this in the future.
            OnClientDisconnectInternal();
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

            // Call ResetStatics to reset statics and singleton
            ResetStatics();
        }

        /// <summary>Set the frame rate for a headless builds. Override to disable or modify.</summary>
        // useful for dedicated servers.
        // useful for headless benchmark clients.
        public virtual void ConfigureHeadlessFrameRate()
        {
            if (Utils.IsHeadless())
            {
                Application.targetFrameRate = sendRate;
                // Debug.Log($"Server Tick Rate set to {Application.targetFrameRate} Hz.");
            }
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
                //Debug.Log("NetworkManager created singleton (DontDestroyOnLoad)");
                singleton = this;
                if (Application.isPlaying)
                {
                    // Force the object to scene root, in case user made it a child of something
                    // in the scene since DDOL is only allowed for scene root objects
                    transform.SetParent(null);
                    DontDestroyOnLoad(gameObject);
                }
            }
            else
            {
                //Debug.Log("NetworkManager created singleton (ForScene)");
                singleton = this;
            }

            // set active transport AFTER setting singleton.
            // so only if we didn't destroy ourselves.

            // This tries to avoid missing transport errors and more clearly tells user what to fix.
            if (transport == null)
                if (TryGetComponent(out Transport newTransport))
                {
                    Debug.LogWarning($"No Transport assigned to Network Manager - Using {newTransport} found on same object.");
                    transport = newTransport;
                }
                else
                {
                    Debug.LogError("No Transport on Network Manager...add a transport and assign it.");
                    return false;
                }

            Transport.active = transport;
            return true;
        }

        void RegisterServerMessages()
        {
            NetworkServer.OnConnectedEvent = OnServerConnectInternal;
            NetworkServer.OnDisconnectedEvent = OnServerDisconnect;
            NetworkServer.OnErrorEvent = OnServerError;
            NetworkServer.OnTransportExceptionEvent = OnServerTransportException;
            NetworkServer.RegisterHandler<AddPlayerMessage>(OnServerAddPlayerInternal);

            // Network Server initially registers its own handler for this, so we replace it here.
            NetworkServer.ReplaceHandler<ReadyMessage>(OnServerReadyMessageInternal);
        }

        void RegisterClientMessages()
        {
            NetworkClient.OnConnectedEvent = OnClientConnectInternal;
            NetworkClient.OnDisconnectedEvent = OnClientDisconnectInternal;
            NetworkClient.OnErrorEvent = OnClientError;
            NetworkClient.OnTransportExceptionEvent = OnClientTransportException;

            // Don't require authentication because server may send NotReadyMessage from ServerChangeScene
            NetworkClient.RegisterHandler<NotReadyMessage>(OnClientNotReadyMessageInternal, false);
            NetworkClient.RegisterHandler<SceneMessage>(OnClientSceneInternal, false);

            if (playerPrefab != null)
                NetworkClient.RegisterPrefab(playerPrefab);

            foreach (GameObject prefab in spawnPrefabs.Where(t => t != null))
                NetworkClient.RegisterPrefab(prefab);
        }

        // This is the only way to clear the singleton, so another instance can be created.
        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ResetStatics()
        {
            // call StopHost if we have a singleton
            if (singleton)
                singleton.StopHost();

            // reset all statics
            startPositions.Clear();
            startPositionIndex = 0;
            clientReadyConnection = null;
            loadingSceneAsync = null;
            networkSceneName = string.Empty;

            // and finally (in case it isn't null already)...
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
            if (string.IsNullOrWhiteSpace(newSceneName))
            {
                Debug.LogError("ServerChangeScene empty scene name");
                return;
            }

            if (NetworkServer.isLoadingScene && newSceneName == networkSceneName)
            {
                Debug.LogError($"Scene change is already in progress for {newSceneName}");
                return;
            }

            // Throw error if called from client
            // Allow changing scene while stopping the server
            if (!NetworkServer.active && newSceneName != offlineScene)
            {
                Debug.LogError("ServerChangeScene can only be called on an active server.");
                return;
            }

            // Debug.Log($"ServerChangeScene {newSceneName}");
            NetworkServer.SetAllClientsNotReady();
            networkSceneName = newSceneName;

            // Let server prepare for scene change
            OnServerChangeScene(newSceneName);

            // set server flag to stop processing messages while changing scenes
            // it will be re-enabled in FinishLoadScene.
            NetworkServer.isLoadingScene = true;

            loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);

            // ServerChangeScene can be called when stopping the server
            // when this happens the server is not active so does not need to tell clients about the change
            if (NetworkServer.active)
            {
                // notify all clients about the new scene
                NetworkServer.SendToAll(new SceneMessage
                {
                    sceneName = newSceneName
                });
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
            if (string.IsNullOrWhiteSpace(newSceneName))
            {
                Debug.LogError("ClientChangeScene empty scene name");
                return;
            }

            //Debug.Log($"ClientChangeScene newSceneName: {newSceneName} networkSceneName{networkSceneName}");

            // Let client prepare for scene change
            OnClientChangeScene(newSceneName, sceneOperation, customHandling);

            // After calling OnClientChangeScene, exit if server since server is already doing
            // the actual scene change, and we don't need to do it for the host client
            if (NetworkServer.active)
                return;

            // set client flag to stop processing messages while loading scenes.
            // otherwise we would process messages and then lose all the state
            // as soon as the load is finishing, causing all kinds of bugs
            // because of missing state.
            // (client may be null after StopClient etc.)
            // Debug.Log("ClientChangeScene: pausing handlers while scene is loading to avoid data loss after scene was loaded.");
            NetworkClient.isLoadingScene = true;

            // Cache sceneOperation so we know what was requested by the
            // Scene message in OnClientChangeScene and OnClientSceneChanged
            clientSceneOperation = sceneOperation;

            // scene handling will happen in overrides of OnClientChangeScene and/or OnClientSceneChanged
            // Do not call FinishLoadScene here. Custom handler will assign loadingSceneAsync and we need
            // to wait for that to finish. UpdateScene already checks for that to be not null and isDone.
            if (customHandling)
                return;

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

                        // Reset the flag that we disabled before entering this switch
                        NetworkClient.isLoadingScene = false;
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

                        // Reset the flag that we disabled before entering this switch
                        NetworkClient.isLoadingScene = false;
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
                    // Debug.Log($"Respawned Server objects after additive scene load: {scene.name}");
                }
                if (NetworkClient.active)
                {
                    NetworkClient.PrepareToSpawnSceneObjects();
                    // Debug.Log($"Rebuild Client spawnableObjects after additive scene load: {scene.name}");
                }
            }
        }

        void UpdateScene()
        {
            if (loadingSceneAsync != null && loadingSceneAsync.isDone)
            {
                //Debug.Log($"ClientChangeScene done readyConn {clientReadyConnection}");

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
            //Debug.Log("FinishLoadScene: resuming handlers after scene was loading.");
            NetworkServer.isLoadingScene = false;
            NetworkClient.isLoadingScene = false;

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
            //Debug.Log("Finished loading scene in host mode.");

            if (clientReadyConnection != null)
            {
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
                    OnClientSceneChanged();
            }
        }

        // finish load scene part for server-only. . makes code easier and is
        // necessary for FinishStartServer later.
        void FinishLoadSceneServerOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            //Debug.Log("Finished loading scene in server-only mode.");

            NetworkServer.SpawnObjects();
            OnServerSceneChanged(networkSceneName);
        }

        // finish load scene part for client-only. makes code easier and is
        // necessary for FinishStartClient later.
        void FinishLoadSceneClientOnly()
        {
            // debug message is very important. if we ever break anything then
            // it's very obvious to notice.
            //Debug.Log("Finished loading scene in client-only mode.");

            if (clientReadyConnection != null)
            {
                clientLoadedScene = true;
                clientReadyConnection = null;
            }

            if (NetworkClient.isConnected)
                OnClientSceneChanged();
        }

        /// <summary>
        /// Registers the transform of a game object as a player spawn location.
        /// <para>This is done automatically by NetworkStartPosition components, but can be done manually from user script code.</para>
        /// </summary>
        /// <param name="start">Transform to register.</param>
        // Static because it's called from NetworkStartPosition::Awake
        // and singleton may not exist yet
        public static void RegisterStartPosition(Transform start)
        {
            // Debug.Log($"RegisterStartPosition: {start.gameObject.name} {start.position}");
            startPositions.Add(start);

            // reorder the list so that round-robin spawning uses the start positions
            // in hierarchy order.  This assumes all objects with NetworkStartPosition
            // component are siblings, either in the scene root or together as children
            // under a single parent in the scene.
            startPositions = startPositions.OrderBy(transform => transform.GetSiblingIndex()).ToList();
        }

        /// <summary>Unregister a Transform from start positions.</summary>
        // Static because it's called from NetworkStartPosition::OnDestroy
        // and singleton may not exist yet
        public static void UnRegisterStartPosition(Transform start)
        {
            //Debug.Log($"UnRegisterStartPosition: {start.name} {start.position}");
            startPositions.Remove(start);
        }

        /// <summary>Get the next NetworkStartPosition based on the selected PlayerSpawnMethod.</summary>
        public virtual Transform GetStartPosition()
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

        void OnServerConnectInternal(NetworkConnectionToClient conn)
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
        void OnServerAuthenticated(NetworkConnectionToClient conn)
        {
            //Debug.Log("NetworkManager.OnServerAuthenticated");

            // set connection to authenticated
            conn.isAuthenticated = true;

            // proceed with the login handshake by calling OnServerConnect
            if (networkSceneName != "" && networkSceneName != offlineScene)
            {
                SceneMessage msg = new SceneMessage()
                {
                    sceneName = networkSceneName
                };
                conn.Send(msg);
            }

            OnServerConnect(conn);
        }

        void OnServerReadyMessageInternal(NetworkConnectionToClient conn, ReadyMessage msg)
        {
            //Debug.Log("NetworkManager.OnServerReadyMessageInternal");
            OnServerReady(conn);
        }

        void OnServerAddPlayerInternal(NetworkConnectionToClient conn, AddPlayerMessage msg)
        {
            //Debug.Log("NetworkManager.OnServerAddPlayer");

            if (autoCreatePlayer && playerPrefab == null)
            {
                Debug.LogError("The PlayerPrefab is empty on the NetworkManager. Please setup a PlayerPrefab object.");
                return;
            }

            if (autoCreatePlayer && !playerPrefab.TryGetComponent(out NetworkIdentity _))
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
                OnClientAuthenticated();
            }
        }

        // called after successful authentication
        void OnClientAuthenticated()
        {
            //Debug.Log("NetworkManager.OnClientAuthenticated");

            // set connection to authenticated
            NetworkClient.connection.isAuthenticated = true;

            // Set flag to wait for scene change?
            if (string.IsNullOrWhiteSpace(onlineScene) || onlineScene == offlineScene || Utils.IsSceneActive(onlineScene))
            {
                clientLoadedScene = false;
            }
            else
            {
                // Scene message expected from server.
                clientLoadedScene = true;
                clientReadyConnection = NetworkClient.connection;
            }

            // Call virtual method regardless of whether a scene change is expected or not.
            OnClientConnect();
        }

        // Transport callback, invoked after client fully disconnected.
        // the call order should always be:
        //   Disconnect() -> ask Transport -> Transport.OnDisconnected -> Cleanup
        void OnClientDisconnectInternal()
        {
            //Debug.Log("NetworkManager.OnClientDisconnectInternal");

            // Only let this run once. StopClient in Host mode changes to ServerOnly
            if (mode == NetworkManagerMode.ServerOnly || mode == NetworkManagerMode.Offline)
                return;

            // user callback
            OnClientDisconnect();

            if (authenticator != null)
            {
                authenticator.OnClientAuthenticated.RemoveListener(OnClientAuthenticated);
                authenticator.OnStopClient();
            }

            // set mode BEFORE changing scene so FinishStartScene doesn't re-initialize anything.
            // set mode BEFORE NetworkClient.Disconnect so StopClient only runs once.
            // set mode BEFORE OnStopClient so StopClient only runs once.
            // If we got here from StopClient in Host mode, change to ServerOnly.
            // - If StopHost was called, StopServer will put us in Offline mode.
            if (mode == NetworkManagerMode.Host)
                mode = NetworkManagerMode.ServerOnly;
            else
                mode = NetworkManagerMode.Offline;

            //Debug.Log("NetworkManager StopClient");
            OnStopClient();

            // shutdown client
            NetworkClient.Shutdown();

            // Exit here if we're now in ServerOnly mode (StopClient called in Host mode).
            if (mode == NetworkManagerMode.ServerOnly) return;

            // Get Network Manager out of DDOL before going to offline scene
            // to avoid collision and let a fresh Network Manager be created.
            // IMPORTANT: .gameObject can be null if StopClient is called from
            //            OnApplicationQuit or from tests!
            if (gameObject != null
                && gameObject.scene.name == "DontDestroyOnLoad"
                && !string.IsNullOrWhiteSpace(offlineScene)
                && SceneManager.GetActiveScene().path != offlineScene)
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());

            // If StopHost called in Host mode, StopServer will change scenes after this.
            // Check loadingSceneAsync to ensure we don't double-invoke the scene change.
            // Check if NetworkServer.active because we can get here via Disconnect before server has started to change scenes.
            if (!string.IsNullOrWhiteSpace(offlineScene) && !Utils.IsSceneActive(offlineScene) && loadingSceneAsync == null && !NetworkServer.active)
            {
                ClientChangeScene(offlineScene, SceneOperation.Normal);
            }

            networkSceneName = "";
        }

        void OnClientNotReadyMessageInternal(NotReadyMessage msg)
        {
            //Debug.Log("NetworkManager.OnClientNotReadyMessageInternal");
            NetworkClient.ready = false;
            OnClientNotReady();

            // NOTE: clientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        void OnClientSceneInternal(SceneMessage msg)
        {
            //Debug.Log("NetworkManager.OnClientSceneInternal");

            // This needs to run for host client too. NetworkServer.active is checked there
            if (NetworkClient.isConnected)
            {
                ClientChangeScene(msg.sceneName, msg.sceneOperation, msg.customHandling);
            }
        }

        /// <summary>Called on the server when a new client connects.</summary>
        public virtual void OnServerConnect(NetworkConnectionToClient conn) { }

        /// <summary>Called on the server when a client disconnects.</summary>
        // Called by NetworkServer.OnTransportDisconnect!
        public virtual void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // by default, this function destroys the connection's player.
            // can be overwritten for cases like delayed logouts in MMOs to
            // avoid players escaping from PvP situations by logging out.
            NetworkServer.DestroyPlayerForConnection(conn);
            //Debug.Log("OnServerDisconnect: Client disconnected.");
        }

        /// <summary>Called on the server when a client is ready (= loaded the scene)</summary>
        public virtual void OnServerReady(NetworkConnectionToClient conn)
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
        public virtual void OnServerAddPlayer(NetworkConnectionToClient conn)
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

        /// <summary>Called on server when transport raises an exception. NetworkConnection may be null.</summary>
        public virtual void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason) { }

        /// <summary>Called on server when transport raises an exception. NetworkConnection may be null.</summary>
        public virtual void OnServerTransportException(NetworkConnectionToClient conn, Exception exception) { }

        /// <summary>Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed</summary>
        public virtual void OnServerChangeScene(string newSceneName) { }

        /// <summary>Called on server after a scene load with ServerChangeScene() is completed.</summary>
        public virtual void OnServerSceneChanged(string sceneName) { }

        /// <summary>Called on the client when connected to a server. By default it sets client as ready and adds a player.</summary>
        public virtual void OnClientConnect()
        {
            // OnClientConnect by default calls AddPlayer but it should not do
            // that when we have online/offline scenes. so we need the
            // clientLoadedScene flag to prevent it.
            if (!clientLoadedScene)
            {
                // Ready/AddPlayer is usually triggered by a scene load completing.
                // if no scene was loaded, then Ready/AddPlayer it here instead.
                if (!NetworkClient.ready)
                    NetworkClient.Ready();

                if (autoCreatePlayer)
                    NetworkClient.AddPlayer();
            }
        }

        /// <summary>Called on clients when disconnected from a server.</summary>
        public virtual void OnClientDisconnect() { }

        // Deprecated 2023-12-05
        /// <summary>Deprecated: NetworkClient handles this now.</summary>
        [Obsolete("NetworkClient handles this now.")]
        public virtual void CalculateConnectionQuality()
        {
            // Moved to NetworkClient
        }

        // Deprecated 2023-12-05
        /// <summary>Deprecated: NetworkClient handles this now.</summary>
        [Obsolete("This will be removed. Subscribe to NetworkClient.onConnectionQualityChanged in your own code")]
        public virtual void OnConnectionQualityChanged(ConnectionQuality previous, ConnectionQuality current)
        {
            // logging the change is very useful to track down user's lag reports.
            // we want to include as much detail as possible for debugging.
            //Debug.Log($"[Mirror] Connection Quality changed from {previous} to {current}:\n  rtt={(NetworkTime.rtt * 1000):F1}ms\n  rttVar={(NetworkTime.rttVariance * 1000):F1}ms\n  bufferTime={(NetworkClient.bufferTime * 1000):F1}ms");
        }

        /// <summary>Called on client when transport raises an exception.</summary>
        public virtual void OnClientError(TransportError error, string reason) { }

        /// <summary>Called on client when transport raises an exception.</summary>
        public virtual void OnClientTransportException(Exception exception) { }

        /// <summary>Called on clients when a servers tells the client it is no longer ready, e.g. when switching scenes.</summary>
        public virtual void OnClientNotReady() { }

        /// <summary>Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed</summary>
        // customHandling: indicates if scene loading will be handled through overrides
        public virtual void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { }

        /// <summary>Called on clients when a scene has completed loaded, when the scene load was initiated by the server.</summary>
        // Scene changes can cause player objects to be destroyed. The default
        // implementation of OnClientSceneChanged in the NetworkManager is to
        // add a player object for the connection if no player object exists.
        public virtual void OnClientSceneChanged()
        {
            // always become ready.
            if (NetworkClient.connection.isAuthenticated && !NetworkClient.ready) NetworkClient.Ready();

            // Only call AddPlayer for normal scene changes, not additive load/unload
            if (NetworkClient.connection.isAuthenticated && clientSceneOperation == SceneOperation.Normal && autoCreatePlayer && NetworkClient.localPlayer == null)
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
        public virtual void OnStartHost() { }

        /// <summary>This is invoked when a server is started - including when a host is started.</summary>
        public virtual void OnStartServer() { }

        /// <summary>This is invoked when the client is started.</summary>
        public virtual void OnStartClient() { }

        /// <summary>This is called when a server is stopped - including when a host is stopped.</summary>
        public virtual void OnStopServer() { }

        /// <summary>This is called when a client is stopped.</summary>
        public virtual void OnStopClient() { }

        /// <summary>This is called when a host is stopped.</summary>
        public virtual void OnStopHost() { }

#if DEBUG
        // keep OnGUI even in builds. useful to debug snap interp.
        void OnGUI()
        {
            if (!timeInterpolationGui) return;
            NetworkClient.OnGUI();
        }
#endif
    }
}
