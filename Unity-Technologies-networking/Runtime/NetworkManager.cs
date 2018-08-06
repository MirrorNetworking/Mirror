#if ENABLE_UNET
using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.Networking.Types;
using UnityEngine.SceneManagement;

namespace UnityEngine.Networking
{
    public enum PlayerSpawnMethod
    {
        Random,
        RoundRobin
    };

    [AddComponentMenu("Network/NetworkManager")]
    public class NetworkManager : MonoBehaviour
    {
        // configuration
        [SerializeField] int m_NetworkPort = 7777;
        [SerializeField] bool m_ServerBindToIP;
        [SerializeField] string m_ServerBindAddress = "";
        [SerializeField] string m_NetworkAddress = "localhost";
        [SerializeField] bool m_DontDestroyOnLoad = true;
        [SerializeField] bool m_RunInBackground = true;
        [Obsolete("Not used anymore, kept to preserve original HLAPI serialization")]
        [SerializeField] bool m_ScriptCRCCheck = true;
        [Obsolete("Not used anymore, kept to preserve original HLAPI serialization")]
        [SerializeField] float m_MaxDelay = 0.01f;
        [SerializeField] LogFilter.FilterLevel m_LogLevel = LogFilter.FilterLevel.Info;
        [SerializeField] GameObject m_PlayerPrefab;
        [SerializeField] bool m_AutoCreatePlayer = true;
        [SerializeField] PlayerSpawnMethod m_PlayerSpawnMethod;
        [SerializeField] string m_OfflineScene = "";
        [SerializeField] string m_OnlineScene = "";
        [SerializeField] List<GameObject> m_SpawnPrefabs = new List<GameObject>();

        [SerializeField] bool m_CustomConfig;
        [SerializeField] int m_MaxConnections = 4;
        [SerializeField] ConnectionConfig m_ConnectionConfig;
        [SerializeField] GlobalConfig m_GlobalConfig;
        [SerializeField] List<QosType> m_Channels = new List<QosType>();

        [SerializeField] bool m_UseWebSockets;

        #pragma warning disable 0169
        [Obsolete("Not used anymore, kept to preserve original HLAPI serialization")]
        [SerializeField] int m_MaxBufferedPackets; 
        [Obsolete("Not used anymore, kept to preserve original HLAPI serialization")]
        [SerializeField] bool m_AllowFragmentation;
        #pragma warning restore 0169
        
        private EndPoint m_EndPoint;
        bool m_ClientLoadedScene;

        // properties
        public int networkPort               { get { return m_NetworkPort; } set { m_NetworkPort = value; } }
        public bool serverBindToIP           { get { return m_ServerBindToIP; } set { m_ServerBindToIP = value; }}
        public string serverBindAddress      { get { return m_ServerBindAddress; } set { m_ServerBindAddress = value; }}
        public string networkAddress         { get { return m_NetworkAddress; }  set { m_NetworkAddress = value; } }
        public bool dontDestroyOnLoad        { get { return m_DontDestroyOnLoad; }  set { m_DontDestroyOnLoad = value; } }
        public bool runInBackground          { get { return m_RunInBackground; }  set { m_RunInBackground = value; } }
        [Obsolete("Not used anymore, kept to preserve original HLAPI serialization")]
        public bool scriptCRCCheck           { get { return m_ScriptCRCCheck; } set { m_ScriptCRCCheck = value;  }}

        [Obsolete("Not used anymore, kept to preserve original HLAPI serialization")]
        public float maxDelay                { get { return m_MaxDelay; }  set { m_MaxDelay = value; } }
        public LogFilter.FilterLevel logLevel { get { return m_LogLevel; }  set { m_LogLevel = value; LogFilter.currentLogLevel = value; } }
        public GameObject playerPrefab       { get { return m_PlayerPrefab; }  set { m_PlayerPrefab = value; } }
        public bool autoCreatePlayer         { get { return m_AutoCreatePlayer; } set { m_AutoCreatePlayer = value; } }
        public PlayerSpawnMethod playerSpawnMethod { get { return m_PlayerSpawnMethod; } set { m_PlayerSpawnMethod = value; } }
        public string offlineScene           { get { return m_OfflineScene; }  set { m_OfflineScene = value; } }
        public string onlineScene            { get { return m_OnlineScene; }  set { m_OnlineScene = value; } }
        public List<GameObject> spawnPrefabs { get { return m_SpawnPrefabs; }}

        public List<Transform> startPositions { get { return s_StartPositions; }}

        public bool customConfig             { get { return m_CustomConfig; } set { m_CustomConfig = value; } }
        public ConnectionConfig connectionConfig { get { if (m_ConnectionConfig == null) { m_ConnectionConfig = new ConnectionConfig(); } return m_ConnectionConfig; } }
        public GlobalConfig globalConfig     { get { if (m_GlobalConfig == null) { m_GlobalConfig = new GlobalConfig(); } return m_GlobalConfig; } }
        public int maxConnections            { get { return m_MaxConnections; } set { m_MaxConnections = value; } }
        public List<QosType> channels        { get { return m_Channels; } }

        public EndPoint secureTunnelEndpoint { get { return m_EndPoint; } set { m_EndPoint = value; } }

        public bool useWebSockets            { get { return m_UseWebSockets; } set { m_UseWebSockets = value; } }

        public bool clientLoadedScene        { get { return m_ClientLoadedScene; } set { m_ClientLoadedScene = value; } }

        // only really valid on the server
        public int numPlayers
        {
            get
            {
                int amount = 0;
                foreach (NetworkConnection conn in NetworkServer.connections)
                {
                    if (conn != null)
                    {
                        amount += conn.playerControllers.Count(pc => pc.IsValid);
                    }
                }
                return amount;
            }
        }

        // runtime data
        static public string networkSceneName = ""; // this is used to make sure that all scene changes are initialized by UNET. loading a scene manually wont set networkSceneName, so UNET would still load it again on start.
        public bool isNetworkActive;
        public NetworkClient client;
        static List<Transform> s_StartPositions = new List<Transform>();
        static int s_StartPositionIndex;

        public static NetworkManager singleton;

        static AsyncOperation s_LoadingSceneAsync;
        static NetworkConnection s_ClientReadyConnection;

        // this is used to persist network address between scenes.
        static string s_Address;

#if UNITY_EDITOR
        static bool s_DomainReload;
        static NetworkManager s_PendingSingleton;

        internal static void OnDomainReload()
        {
            s_DomainReload = true;
        }

        public NetworkManager()
        {
            s_PendingSingleton = this;
        }
#endif

        void Awake()
        {
            Debug.Log("Thank you for using HLAPI Community Edition! https://forum.unity.com/threads/unet-hlapi-community-edition.425437/");
            InitializeSingleton();
        }

        void InitializeSingleton()
        {
            if (singleton != null && singleton == this)
            {
                return;
            }

            // do this early
            if (logLevel != LogFilter.FilterLevel.SetInScripting)
            {
                LogFilter.currentLogLevel = logLevel;
            }

            if (m_DontDestroyOnLoad)
            {
                if (singleton != null)
                {
                    if (LogFilter.logDev) { Debug.Log("Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time. The duplicate NetworkManager will not be used."); }
                    Destroy(gameObject);
                    return;
                }
                if (LogFilter.logDev) { Debug.Log("NetworkManager created singleton (DontDestroyOnLoad)"); }
                singleton = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else
            {
                if (LogFilter.logDev) { Debug.Log("NetworkManager created singleton (ForScene)"); }
                singleton = this;
            }

            // persistent network address between scene changes
            if (m_NetworkAddress != "")
            {
                s_Address = m_NetworkAddress;
            }
            else if (s_Address != "")
            {
                m_NetworkAddress = s_Address;
            }
        }

        void OnValidate()
        {
            m_MaxConnections = Mathf.Clamp(m_MaxConnections, 1, 32000); // [1, 32000]

            if (m_PlayerPrefab != null && m_PlayerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkManager - playerPrefab must have a NetworkIdentity."); }
                m_PlayerPrefab = null;
            }

            if (m_ConnectionConfig != null && m_ConnectionConfig.MinUpdateTimeout <= 0)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkManager MinUpdateTimeout cannot be zero or less. The value will be reset to 1 millisecond"); }
                m_ConnectionConfig.MinUpdateTimeout = 1;
            }

            if (m_GlobalConfig != null)
            {
                if (m_GlobalConfig.ThreadAwakeTimeout <= 0)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkManager ThreadAwakeTimeout cannot be zero or less. The value will be reset to 1 millisecond"); }
                    m_GlobalConfig.ThreadAwakeTimeout = 1;
                }
            }

            // vis2k
            // Channels.DefaultReliable = 0 and Unreliable = 1, so we have to
            // force some kind of reliable to channel 0 and unreliable to 1.
            // Otherwise the HLAPI code will assume a wrong channel for a lot of
            // built-in functions.
            //
            // Changing them would result in chaos. Using anything != Fragmented
            // would fail to send bigger messages too.
            if (channels.Count < 1 || !Channels.IsReliableQoS(channels[0]))
                Debug.LogWarning("NetworkManager: First channel needs to be Reliable because in the code Channels.DefaultReliable is 0.");

            if (channels.Count < 2 || !Channels.IsUnreliableQoS(channels[1]))
                Debug.LogWarning("NetworkManager: Second channel needs to be Unreliable because in the code Channels.DefaultReliable is 1.");
        }

        internal void RegisterServerMessages()
        {
            NetworkServer.RegisterHandler((short)MsgType.Connect, OnServerConnectInternal);
            NetworkServer.RegisterHandler((short)MsgType.Disconnect, OnServerDisconnectInternal);
            NetworkServer.RegisterHandler((short)MsgType.Ready, OnServerReadyMessageInternal);
            NetworkServer.RegisterHandler((short)MsgType.AddPlayer, OnServerAddPlayerMessageInternal);
            NetworkServer.RegisterHandler((short)MsgType.RemovePlayer, OnServerRemovePlayerMessageInternal);
            NetworkServer.RegisterHandler((short)MsgType.Error, OnServerErrorInternal);
        }

        public bool StartServer()
        {
            return StartServer(null, -1);
        }

        bool StartServer(ConnectionConfig config, int maxConnections)
        {
            InitializeSingleton();

            OnStartServer();

            if (m_RunInBackground)
                Application.runInBackground = true;

            NetworkServer.useWebSockets = m_UseWebSockets;

            if (m_GlobalConfig != null)
            {
                NetworkTransport.Init(m_GlobalConfig);
            }

            // passing a config overrides setting the connectionConfig property
            if (m_CustomConfig && m_ConnectionConfig != null && config == null)
            {
                m_ConnectionConfig.Channels.Clear();
                for (int channelId = 0; channelId < m_Channels.Count; channelId++)
                {
                    m_ConnectionConfig.AddChannel(m_Channels[channelId]);
                }
                NetworkServer.Configure(m_ConnectionConfig, m_MaxConnections);
            }

            if (config != null)
            {
                NetworkServer.Configure(config, maxConnections);
            }

            if (m_ServerBindToIP && !string.IsNullOrEmpty(m_ServerBindAddress))
            {
                if (!NetworkServer.Listen(m_ServerBindAddress, m_NetworkPort))
                {
                    if (LogFilter.logError) { Debug.LogError("StartServer listen on " + m_ServerBindAddress + " failed."); }
                    return false;
                }
            }
            else
            {
                if (!NetworkServer.Listen(m_NetworkPort))
                {
                    if (LogFilter.logError) { Debug.LogError("StartServer listen failed."); }
                    return false;
                }
            }

            // this must be after Listen(), since that registers the default message handlers
            RegisterServerMessages();

            if (LogFilter.logDebug) { Debug.Log("NetworkManager StartServer port:" + m_NetworkPort); }
            isNetworkActive = true;

            // Only change scene if the requested online scene is not blank, and is not already loaded
            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (!string.IsNullOrEmpty(m_OnlineScene) && m_OnlineScene != loadedSceneName && m_OnlineScene != m_OfflineScene)
            {
                ServerChangeScene(m_OnlineScene);
            }
            else
            {
                NetworkServer.SpawnObjects();
            }
            return true;
        }

        internal void RegisterClientMessages(NetworkClient client)
        {
            client.RegisterHandler((short)MsgType.Connect, OnClientConnectInternal);
            client.RegisterHandler((short)MsgType.Disconnect, OnClientDisconnectInternal);
            client.RegisterHandler((short)MsgType.NotReady, OnClientNotReadyMessageInternal);
            client.RegisterHandler((short)MsgType.Error, OnClientErrorInternal);
            client.RegisterHandler((short)MsgType.Scene, OnClientSceneInternal);

            if (m_PlayerPrefab != null)
            {
                ClientScene.RegisterPrefab(m_PlayerPrefab);
            }
            for (int i = 0; i < m_SpawnPrefabs.Count; i++)
            {
                var prefab = m_SpawnPrefabs[i];
                if (prefab != null)
                {
                    ClientScene.RegisterPrefab(prefab);
                }
            }
        }

        public void UseExternalClient(NetworkClient externalClient)
        {
            if (m_RunInBackground)
                Application.runInBackground = true;

            if (externalClient != null)
            {
                client = externalClient;
                isNetworkActive = true;
                RegisterClientMessages(client);
                OnStartClient(client);
            }
            else
            {
                OnStopClient();

                // this should stop any game-related systems, but not close the connection
                ClientScene.DestroyAllClientObjects();
                ClientScene.HandleClientDisconnect(client.connection);
                client = null;
                if (!string.IsNullOrEmpty(m_OfflineScene))
                {
                    ClientChangeScene(m_OfflineScene, false);
                }
            }
            s_Address = m_NetworkAddress;
        }

        public NetworkClient StartClient(ConnectionConfig config, int hostPort)
        {
            InitializeSingleton();

            if (m_RunInBackground)
                Application.runInBackground = true;

            isNetworkActive = true;

            if (m_GlobalConfig != null)
            {
                NetworkTransport.Init(m_GlobalConfig);
            }

            client = new NetworkClient();
            client.hostPort = hostPort;

            if (config != null)
            {
                if ((config.UsePlatformSpecificProtocols) && (Application.platform != RuntimePlatform.PS4) && (Application.platform != RuntimePlatform.PSP2))
                    throw new ArgumentOutOfRangeException("Platform specific protocols are not supported on this platform");

                client.Configure(config, 1);
            }
            else
            {
                if (m_CustomConfig && m_ConnectionConfig != null)
                {
                    m_ConnectionConfig.Channels.Clear();
                    for (int i = 0; i < m_Channels.Count; i++)
                    {
                        m_ConnectionConfig.AddChannel(m_Channels[i]);
                    }
                    if ((m_ConnectionConfig.UsePlatformSpecificProtocols) && (Application.platform != RuntimePlatform.PS4) && (Application.platform != RuntimePlatform.PSP2))
                        throw new ArgumentOutOfRangeException("Platform specific protocols are not supported on this platform");
                    client.Configure(m_ConnectionConfig, m_MaxConnections);
                }
            }

            RegisterClientMessages(client);
            if (m_EndPoint != null)
            {
                if (LogFilter.logDebug) { Debug.Log("NetworkManager StartClient using provided SecureTunnel"); }
                client.Connect(m_EndPoint);
            }
            else
            {
                if (string.IsNullOrEmpty(m_NetworkAddress))
                {
                    if (LogFilter.logError) { Debug.LogError("Must set the Network Address field in the manager"); }
                    return null;
                }
                if (LogFilter.logDebug) { Debug.Log("NetworkManager StartClient address:" + m_NetworkAddress + " port:" + m_NetworkPort); }

                client.Connect(m_NetworkAddress, m_NetworkPort);
            }

            OnStartClient(client);
            s_Address = m_NetworkAddress;
            return client;
        }

        public NetworkClient StartClient()
        {
            return StartClient(null);
        }

        public NetworkClient StartClient(ConnectionConfig config)
        {
            return StartClient(config, 0);
        }

        public virtual NetworkClient StartHost(ConnectionConfig config, int maxConnections)
        {
            OnStartHost();
            if (StartServer(config, maxConnections))
            {
                var client = ConnectLocalClient();
                OnServerConnect(client.connection);
                OnStartClient(client);
                return client;
            }
            return null;
        }

        public virtual NetworkClient StartHost()
        {
            OnStartHost();
            if (StartServer())
            {
                var localClient = ConnectLocalClient();
                OnStartClient(localClient);
                return localClient;
            }
            return null;
        }

        NetworkClient ConnectLocalClient()
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager StartHost port:" + m_NetworkPort); }
            m_NetworkAddress = "localhost";
            client = ClientScene.ConnectLocalServer();
            RegisterClientMessages(client);
            return client;
        }

        public void StopHost()
        {
            OnStopHost();

            StopServer();
            StopClient();
        }

        public void StopServer()
        {
            if (!NetworkServer.active)
                return;

            OnStopServer();

            if (LogFilter.logDebug) { Debug.Log("NetworkManager StopServer"); }
            isNetworkActive = false;
            NetworkServer.Shutdown();
            if (!string.IsNullOrEmpty(m_OfflineScene))
            {
                ServerChangeScene(m_OfflineScene);
            }
            CleanupNetworkIdentities();
        }

        public void StopClient()
        {
            OnStopClient();

            if (LogFilter.logDebug) { Debug.Log("NetworkManager StopClient"); }
            isNetworkActive = false;
            if (client != null)
            {
                // only shutdown this client, not ALL clients.
                client.Disconnect();
                client.Shutdown();
                client = null;
            }

            ClientScene.DestroyAllClientObjects();
            if (!string.IsNullOrEmpty(m_OfflineScene))
            {
                ClientChangeScene(m_OfflineScene, false);
            }
            CleanupNetworkIdentities();
        }

        public virtual void ServerChangeScene(string newSceneName)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                if (LogFilter.logError) { Debug.LogError("ServerChangeScene empty scene name"); }
                return;
            }

            if (LogFilter.logDebug) { Debug.Log("ServerChangeScene " + newSceneName); }
            NetworkServer.SetAllClientsNotReady();
            networkSceneName = newSceneName;

            s_LoadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);

            StringMessage msg = new StringMessage(networkSceneName);
            NetworkServer.SendToAll((short)MsgType.Scene, msg);

            s_StartPositionIndex = 0;
            s_StartPositions.Clear();
        }

        void CleanupNetworkIdentities()
        {
            foreach (NetworkIdentity netId in Resources.FindObjectsOfTypeAll<NetworkIdentity>())
            {
                netId.MarkForReset();
            }
        }

        internal void ClientChangeScene(string newSceneName, bool forceReload)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                if (LogFilter.logError) { Debug.LogError("ClientChangeScene empty scene name"); }
                return;
            }

            if (LogFilter.logDebug) { Debug.Log("ClientChangeScene newSceneName:" + newSceneName + " networkSceneName:" + networkSceneName); }


            if (newSceneName == networkSceneName)
            {
                if (!forceReload)
                {
                    FinishLoadScene();
                    return;
                }
            }

            // vis2k: pause message handling while loading scene. otherwise we will process messages and then lose all
            // the sate as soon as the load is finishing, causing all kinds of bugs because of missing state.
            // (client may be null after StopClient etc.)
            if (client != null)
            {
                if (LogFilter.logDebug) { Debug.Log("ClientChangeScene: pausing handlers while scene is loading to avoid data loss after scene was loaded."); }
                client.connection.PauseHandling();
            }

            s_LoadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);
            networkSceneName = newSceneName;
        }

        void FinishLoadScene()
        {
            // NOTE: this cannot use NetworkClient.allClients[0] - that client may be for a completely different purpose.

            if (client != null)
            {
                // process queued messages that we received while loading the scene
                if (LogFilter.logDebug) { Debug.Log("FinishLoadScene: resuming handlers after scene was loading."); }
                client.connection.ResumeHandling();

                if (s_ClientReadyConnection != null)
                {
                    m_ClientLoadedScene = true;
                    OnClientConnect(s_ClientReadyConnection);
                    s_ClientReadyConnection = null;
                }
            }
            else
            {
                if (LogFilter.logDev) { Debug.Log("FinishLoadScene client is null"); }
            }

            if (NetworkServer.active)
            {
                NetworkServer.SpawnObjects();
                OnServerSceneChanged(networkSceneName);
            }

            if (IsClientConnected() && client != null)
            {
                RegisterClientMessages(client);
                OnClientSceneChanged(client.connection);
            }
        }

        internal static void UpdateScene()
        {
#if UNITY_EDITOR
            // In the editor, reloading scripts in play mode causes a Mono Domain Reload.
            // This gets the transport layer (C++) and HLAPI (C#) out of sync.
            // This check below detects that problem and shuts down the transport layer to bring both systems back in sync.
            if (singleton == null && s_PendingSingleton != null && s_DomainReload)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("NetworkManager detected a script reload in the editor. This has caused the network to be shut down."); }

                s_DomainReload = false;
                s_PendingSingleton.InitializeSingleton();

                // destroy network objects
                var uvs = FindObjectsOfType<NetworkIdentity>();
                foreach (var uv in uvs)
                {
                    Destroy(uv.gameObject);
                }

                singleton.StopHost();

                NetworkTransport.Shutdown();
            }
#endif

            if (singleton == null)
                return;

            if (s_LoadingSceneAsync == null)
                return;

            if (!s_LoadingSceneAsync.isDone)
                return;

            if (LogFilter.logDebug) { Debug.Log("ClientChangeScene done readyCon:" + s_ClientReadyConnection); }
            singleton.FinishLoadScene();
            s_LoadingSceneAsync.allowSceneActivation = true;
            s_LoadingSceneAsync = null;
        }

        void OnDestroy()
        {
            if (LogFilter.logDev) { Debug.Log("NetworkManager destroyed"); }
        }

        static public void RegisterStartPosition(Transform start)
        {
            if (LogFilter.logDebug) { Debug.Log("RegisterStartPosition: (" + start.gameObject.name + ") " + start.position); }
            s_StartPositions.Add(start);
        }

        static public void UnRegisterStartPosition(Transform start)
        {
            if (LogFilter.logDebug) { Debug.Log("UnRegisterStartPosition: (" + start.gameObject.name + ") " + start.position); }
            s_StartPositions.Remove(start);
        }

        public bool IsClientConnected()
        {
            return client != null && client.isConnected;
        }

        // this is the only way to clear the singleton, so another instance can be created.
        static public void Shutdown()
        {
            if (singleton == null)
                return;

            s_StartPositions.Clear();
            s_StartPositionIndex = 0;
            s_ClientReadyConnection = null;

            singleton.StopHost();
            singleton = null;
        }

        // ----------------------------- Server Internal Message Handlers  --------------------------------

        internal void OnServerConnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnServerConnectInternal"); }

            if (networkSceneName != "" && networkSceneName != m_OfflineScene)
            {
                StringMessage msg = new StringMessage(networkSceneName);
                netMsg.conn.Send((short)MsgType.Scene, msg);
            }

            OnServerConnect(netMsg.conn);
        }

        internal void OnServerDisconnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnServerDisconnectInternal"); }
            OnServerDisconnect(netMsg.conn);
        }

        internal void OnServerReadyMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnServerReadyMessageInternal"); }
            OnServerReady(netMsg.conn);
        }

        internal void OnServerAddPlayerMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnServerAddPlayerMessageInternal"); }

            AddPlayerMessage msg = new AddPlayerMessage();
            netMsg.ReadMessage(msg);

            if (msg.msgData != null && msg.msgData.Length > 0)
            {
                var reader = new NetworkReader(msg.msgData);
                OnServerAddPlayer(netMsg.conn, msg.playerControllerId, reader);
            }
            else
            {
                OnServerAddPlayer(netMsg.conn, msg.playerControllerId);
            }
        }

        internal void OnServerRemovePlayerMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnServerRemovePlayerMessageInternal"); }

            RemovePlayerMessage msg = new RemovePlayerMessage();
            netMsg.ReadMessage(msg);

            PlayerController player;
            netMsg.conn.GetPlayerController(msg.playerControllerId, out player);
            OnServerRemovePlayer(netMsg.conn, player);
            netMsg.conn.RemovePlayerController(msg.playerControllerId);
        }

        internal void OnServerErrorInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnServerErrorInternal"); }

            ErrorMessage msg = new ErrorMessage();
            netMsg.ReadMessage(msg);
            OnServerError(netMsg.conn, msg.errorCode);
        }

        // ----------------------------- Client Internal Message Handlers  --------------------------------

        internal void OnClientConnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnClientConnectInternal"); }

            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (string.IsNullOrEmpty(m_OnlineScene) || (m_OnlineScene == m_OfflineScene) || (loadedSceneName == m_OnlineScene))
            {
                m_ClientLoadedScene = false;
                OnClientConnect(netMsg.conn);
            }
            else
            {
                // will wait for scene id to come from the server.
                s_ClientReadyConnection = netMsg.conn;
            }
        }

        internal void OnClientDisconnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnClientDisconnectInternal"); }

            if (!string.IsNullOrEmpty(m_OfflineScene))
            {
                ClientChangeScene(m_OfflineScene, false);
            }

            OnClientDisconnect(netMsg.conn);
        }

        internal void OnClientNotReadyMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnClientNotReadyMessageInternal"); }

            ClientScene.SetNotReady();
            OnClientNotReady(netMsg.conn);

            // NOTE: s_ClientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        internal void OnClientErrorInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnClientErrorInternal"); }

            ErrorMessage msg = new ErrorMessage();
            netMsg.ReadMessage(msg);
            OnClientError(netMsg.conn, msg.errorCode);
        }

        internal void OnClientSceneInternal(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkManager:OnClientSceneInternal"); }

            string newSceneName = netMsg.reader.ReadString();

            if (IsClientConnected() && !NetworkServer.active)
            {
                ClientChangeScene(newSceneName, true);
            }
        }

        // ----------------------------- Server System Callbacks --------------------------------

        public virtual void OnServerConnect(NetworkConnection conn)
        {
        }

        public virtual void OnServerDisconnect(NetworkConnection conn)
        {
            NetworkServer.DestroyPlayersForConnection(conn);

            // note: timeouts happen all the time, no need to throw an error there.
            if (conn.lastError == NetworkError.Ok || conn.lastError == NetworkError.Timeout)
            {
                if (LogFilter.logDebug) { Debug.Log("OnServerDisconnect: client disconnected:" + conn); }
            }
            else
            {
                // a client disconnected, let's show a message
                if (LogFilter.logError) { Debug.LogError("OnServerDisconnect: Client disconnected with error: " + conn.lastError); }
            }
        }

        public virtual void OnServerReady(NetworkConnection conn)
        {
            if (conn.playerControllers.Count == 0)
            {
                // this is now allowed (was not for a while)
                if (LogFilter.logDebug) { Debug.Log("Ready with no player object"); }
            }
            NetworkServer.SetClientReady(conn);
        }

        public virtual void OnServerAddPlayer(NetworkConnection conn, short playerControllerId, NetworkReader extraMessageReader)
        {
            OnServerAddPlayerInternal(conn, playerControllerId);
        }

        public virtual void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
        {
            OnServerAddPlayerInternal(conn, playerControllerId);
        }

        void OnServerAddPlayerInternal(NetworkConnection conn, short playerControllerId)
        {
            if (m_PlayerPrefab == null)
            {
                if (LogFilter.logError) { Debug.LogError("The PlayerPrefab is empty on the NetworkManager. Please setup a PlayerPrefab object."); }
                return;
            }

            if (m_PlayerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                if (LogFilter.logError) { Debug.LogError("The PlayerPrefab does not have a NetworkIdentity. Please add a NetworkIdentity to the player prefab."); }
                return;
            }

            if (playerControllerId < conn.playerControllers.Count  && conn.playerControllers[playerControllerId].IsValid && conn.playerControllers[playerControllerId].gameObject != null)
            {
                if (LogFilter.logError) { Debug.LogError("There is already a player at that playerControllerId for this connections."); }
                return;
            }

            GameObject player;
            Transform startPos = GetStartPosition();
            if (startPos != null)
            {
                player = (GameObject)Instantiate(m_PlayerPrefab, startPos.position, startPos.rotation);
            }
            else
            {
                player = (GameObject)Instantiate(m_PlayerPrefab, Vector3.zero, Quaternion.identity);
            }

            NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
        }

        public Transform GetStartPosition()
        {
            // first remove any dead transforms
            s_StartPositions.RemoveAll(t => t == null);

            if (m_PlayerSpawnMethod == PlayerSpawnMethod.Random && s_StartPositions.Count > 0)
            {
                // try to spawn at a random start location
                int index = Random.Range(0, s_StartPositions.Count);
                return s_StartPositions[index];
            }
            if (m_PlayerSpawnMethod == PlayerSpawnMethod.RoundRobin && s_StartPositions.Count > 0)
            {
                if (s_StartPositionIndex >= s_StartPositions.Count)
                {
                    s_StartPositionIndex = 0;
                }

                Transform startPos = s_StartPositions[s_StartPositionIndex];
                s_StartPositionIndex += 1;
                return startPos;
            }
            return null;
        }

        public virtual void OnServerRemovePlayer(NetworkConnection conn, PlayerController player)
        {
            if (player.gameObject != null)
            {
                NetworkServer.Destroy(player.gameObject);
            }
        }

        public virtual void OnServerError(NetworkConnection conn, int errorCode)
        {
        }

        public virtual void OnServerSceneChanged(string sceneName)
        {
        }

        // ----------------------------- Client System Callbacks --------------------------------

        public virtual void OnClientConnect(NetworkConnection conn)
        {
            if (!clientLoadedScene)
            {
                // Ready/AddPlayer is usually triggered by a scene load completing. if no scene was loaded, then Ready/AddPlayer it here instead.
                ClientScene.Ready(conn);
                if (m_AutoCreatePlayer)
                {
                    ClientScene.AddPlayer(0);
                }
            }
        }

        public virtual void OnClientDisconnect(NetworkConnection conn)
        {
            StopClient();
            if (conn.lastError != NetworkError.Ok)
            {
                if (LogFilter.logError) { Debug.LogError("ClientDisconnected due to error: " + conn.lastError); }
            }
        }

        public virtual void OnClientError(NetworkConnection conn, int errorCode)
        {
        }

        public virtual void OnClientNotReady(NetworkConnection conn)
        {
        }

        public virtual void OnClientSceneChanged(NetworkConnection conn)
        {
            // always become ready.
            ClientScene.Ready(conn);

            // vis2k: replaced all this weird code with something more simple
            if (m_AutoCreatePlayer)
            {
                // add player if all existing ones are null (or if list is empty, then .All returns true)
                if (ClientScene.localPlayers.All(pc => pc.gameObject == null))
                {
                    ClientScene.AddPlayer(0);
                }
            }
        }

        //------------------------------ Start & Stop callbacks -----------------------------------

        // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
        // their functionality, users would need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

        public virtual void OnStartHost()
        {
        }

        public virtual void OnStartServer()
        {
        }

        public virtual void OnStartClient(NetworkClient client)
        {
        }

        public virtual void OnStopServer()
        {
        }

        public virtual void OnStopClient()
        {
        }

        public virtual void OnStopHost()
        {
        }
    }
}
#endif //ENABLE_UNET
