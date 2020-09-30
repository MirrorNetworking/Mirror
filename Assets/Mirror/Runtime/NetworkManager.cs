using System;
using System.Collections.Generic;
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
    public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly }

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
        /// <para>If the application is a Server Build, StartServer is automatically invoked.</para>
        /// <para>Server build is true when "Server build" is checked in build menu, or BuildOptions.EnableHeadlessMode flag is in BuildOptions</para>
        /// </summary>
        [Tooltip("Should the server auto-start when 'Server Build' is checked in build settings")]
        [FormerlySerializedAs("startOnHeadless")]
        public bool autoStartServerBuild = true;

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
        /// <para>For each of these prefabs, ClientScene.RegisterPrefab() will be automatically invoked.</para>
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
        [Obsolete("Use #if UNITY_SERVER instead.")]
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

            // UNET/original Mirror used to support runtime scene changes, which
            // never worked. let's simply forbid it.
            SceneManager.sceneLoaded += (scene, sceneMode) =>
            {
                if (isNetworkActive)
                {
                    throw new Exception("Mirror does not support runtime scene changes of any kind. This never realiably worked before.");
                }
            };
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
#if UNITY_SERVER
            if (autoStartServerBuild)
            {
                StartServer();
            }
#endif
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
        }

        #endregion

        #region Start & Stop

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
        public void StartServer()
        {
            mode = NetworkManagerMode.ServerOnly;

            SetupServer();
            NetworkServer.SpawnObjects();
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
            // Debug.Log("NetworkManager StartClient address:" + networkAddress);

            NetworkClient.Connect(networkAddress);

            OnStartClient();
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

            Debug.Log("NetworkManager StopServer");
            isNetworkActive = false;
            NetworkServer.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;

            startPositionIndex = 0;
        }

        /// <summary>
        /// Stops the client that the manager is using.
        /// </summary>
        public void StopClient()
        {
            if (authenticator != null)
                authenticator.OnClientAuthenticated.RemoveListener(OnClientAuthenticated);

            OnStopClient();

            Debug.Log("NetworkManager StopClient");
            isNetworkActive = false;

            // shutdown client
            NetworkClient.Disconnect();
            NetworkClient.Shutdown();

            // set offline mode
            mode = NetworkManagerMode.Offline;
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
            // only set framerate for server build
#if UNITY_SERVER
            Application.targetFrameRate = serverTickRate;
            if (logger.logEnabled) Debug.Log("Server Tick Rate set to: " + Application.targetFrameRate + " Hz.");
#endif
        }

        bool InitializeSingleton()
        {
            if (singleton != null && singleton == this) return true;

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
            NetworkServer.RegisterHandler<ConnectMessage>(OnServerConnectInternal, false);
            NetworkServer.RegisterHandler<DisconnectMessage>(OnServerDisconnectInternal, false);
            NetworkServer.RegisterHandler<AddPlayerMessage>(OnServerAddPlayerInternal);
            NetworkServer.RegisterHandler<ErrorMessage>(OnServerErrorInternal, false);

            // Network Server initially registers its own handler for this, so we replace it here.
            NetworkServer.ReplaceHandler<ReadyMessage>(OnServerReadyMessageInternal);
        }

        void RegisterClientMessages()
        {
            NetworkClient.RegisterHandler<ConnectMessage>(OnClientConnectInternal, false);
            NetworkClient.RegisterHandler<DisconnectMessage>(OnClientDisconnectInternal, false);
            NetworkClient.RegisterHandler<NotReadyMessage>(OnClientNotReadyMessageInternal);
            NetworkClient.RegisterHandler<ErrorMessage>(OnClientErrorInternal, false);

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

            singleton = null;
        }

        /// <summary>
        /// virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        /// </summary>
        public virtual void OnDestroy()
        {
            Debug.Log("NetworkManager destroyed");
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
            // Debug.Log("RegisterStartPosition: (" + start.gameObject.name + ") " + start.position);
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
            // Debug.Log("UnRegisterStartPosition: (" + start.gameObject.name + ") " + start.position);
            startPositions.Remove(start);
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

        #endregion

        #region Server Internal Message Handlers

        void OnServerConnectInternal(NetworkConnection conn, ConnectMessage connectMsg)
        {
            Debug.Log("NetworkManager.OnServerConnectInternal");

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
        void OnServerAuthenticated(NetworkConnection conn)
        {
            Debug.Log("NetworkManager.OnServerAuthenticated");

            // set connection to authenticated
            conn.isAuthenticated = true;

            // proceed with the login handshake by calling OnServerConnect
            OnServerConnect(conn);
        }

        void OnServerDisconnectInternal(NetworkConnection conn, DisconnectMessage msg)
        {
            Debug.Log("NetworkManager.OnServerDisconnectInternal");
            OnServerDisconnect(conn);
        }

        void OnServerReadyMessageInternal(NetworkConnection conn, ReadyMessage msg)
        {
            Debug.Log("NetworkManager.OnServerReadyMessageInternal");
            OnServerReady(conn);
        }

        void OnServerAddPlayerInternal(NetworkConnection conn, AddPlayerMessage msg)
        {
            Debug.Log("NetworkManager.OnServerAddPlayer");

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

        void OnServerErrorInternal(NetworkConnection conn, ErrorMessage msg)
        {
            Debug.Log("NetworkManager.OnServerErrorInternal");
            OnServerError(conn, msg.value);
        }

        #endregion

        #region Client Internal Message Handlers

        void OnClientConnectInternal(NetworkConnection conn, ConnectMessage message)
        {
            Debug.Log("NetworkManager.OnClientConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnClientAuthenticate(conn);
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
            Debug.Log("NetworkManager.OnClientAuthenticated");

            // set connection to authenticated
            conn.isAuthenticated = true;

            // proceed with the login handshake by calling OnClientConnect
            clientLoadedScene = false;
            OnClientConnect(conn);
        }

        void OnClientDisconnectInternal(NetworkConnection conn, DisconnectMessage msg)
        {
            Debug.Log("NetworkManager.OnClientDisconnectInternal");
            OnClientDisconnect(conn);
        }

        void OnClientNotReadyMessageInternal(NetworkConnection conn, NotReadyMessage msg)
        {
            Debug.Log("NetworkManager.OnClientNotReadyMessageInternal");

            ClientScene.ready = false;
            OnClientNotReady(conn);

            // NOTE: clientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        void OnClientErrorInternal(NetworkConnection conn, ErrorMessage msg)
        {
            Debug.Log("NetworkManager:OnClientErrorInternal");
            OnClientError(conn, msg.value);
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
            Debug.Log("OnServerDisconnect: Client disconnected.");
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
                Debug.Log("Ready with no player object");
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
        /// Called on the server when a network error occurs for a client connection.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        /// <param name="errorCode">Error code.</param>
        public virtual void OnServerError(NetworkConnection conn, int errorCode) { }

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

        #endregion

        #region Start & Stop callbacks

        // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
        // their functionality, users would need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

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

        #endregion
    }
}
