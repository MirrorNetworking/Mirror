using System.Collections.Generic;
using System.Net;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using System;

namespace Mirror
{
    public enum PlayerSpawnMethod
    {
        Random,
        RoundRobin
    }

    [AddComponentMenu("Network/NetworkManager")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkManager")]
    public class NetworkManager : MonoBehaviour
    {

        // configuration
        [FormerlySerializedAs("m_DontDestroyOnLoad")] public bool dontDestroyOnLoad = true;
        [FormerlySerializedAs("m_RunInBackground")] public bool runInBackground = true;
        public bool startOnHeadless = true;
        [FormerlySerializedAs("m_ShowDebugMessages")] public bool showDebugMessages;

        [Scene]
        [FormerlySerializedAs("m_OfflineScene")] public string offlineScene = "";

        [Scene]
        [FormerlySerializedAs("m_OnlineScene")] public string onlineScene = "";

        [Header("Network Info")]
        // transport layer
        public Transport transport;
        [FormerlySerializedAs("m_NetworkAddress")] public string networkAddress = "localhost";
        [FormerlySerializedAs("m_MaxConnections")] public int maxConnections = 4;

        [Header("Spawn Info")]
        [FormerlySerializedAs("m_PlayerPrefab")] public GameObject playerPrefab;
        [FormerlySerializedAs("m_AutoCreatePlayer")] public bool autoCreatePlayer = true;
        [FormerlySerializedAs("m_PlayerSpawnMethod")] public PlayerSpawnMethod playerSpawnMethod;

        [FormerlySerializedAs("m_SpawnPrefabs"),HideInInspector]
        public List<GameObject> spawnPrefabs = new List<GameObject>();

        public static List<Transform> startPositions = new List<Transform>();

        [NonSerialized]
        public bool clientLoadedScene;

        // only really valid on the server
        public int numPlayers => NetworkServer.connections.Count(kv => kv.Value.playerController != null);

        // runtime data
        public static string networkSceneName = ""; // this is used to make sure that all scene changes are initialized by UNET. loading a scene manually wont set networkSceneName, so UNET would still load it again on start.
        [NonSerialized]
        public bool isNetworkActive;
        public NetworkClient client;
        static int s_StartPositionIndex;

        public static NetworkManager singleton;

        static AsyncOperation s_LoadingSceneAsync;
        static NetworkConnection s_ClientReadyConnection;

        // this is used to persist network address between scenes.
        static string s_Address;

        // virtual so that inheriting classes' Awake() can call base.Awake() too
        public virtual void Awake()
        {
            Debug.Log("Thank you for using Mirror! https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/");

            // Set the networkSceneName to prevent a scene reload
            // if client connection to server fails.
            networkSceneName = offlineScene;

            InitializeSingleton();

            // headless mode? then start the server
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null && startOnHeadless)
            {
                Application.targetFrameRate = 60;
                StartServer();
            }
        }

        void InitializeSingleton()
        {
            if (singleton != null && singleton == this)
            {
                return;
            }

            // do this early
            LogFilter.Debug = showDebugMessages;

            if (dontDestroyOnLoad)
            {
                if (singleton != null)
                {
                    Debug.LogError("Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time. The duplicate NetworkManager will not be used.");
                    Destroy(gameObject);
                    return;
                }
                if (LogFilter.Debug) { Debug.Log("NetworkManager created singleton (DontDestroyOnLoad)"); }
                singleton = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else
            {
                if (LogFilter.Debug) { Debug.Log("NetworkManager created singleton (ForScene)"); }
                singleton = this;
            }

            // persistent network address between scene changes
            if (networkAddress != "")
            {
                s_Address = networkAddress;
            }
            else if (s_Address != "")
            {
                networkAddress = s_Address;
            }
        }

        // NetworkIdentity.UNetStaticUpdate is called from UnityEngine while LLAPI network is active.
        // if we want TCP then we need to call it manually. probably best from NetworkManager, although this means
        // that we can't use NetworkServer/NetworkClient without a NetworkManager invoking Update anymore.
        //
        // virtual so that inheriting classes' LateUpdate() can call base.LateUpdate() too
        public virtual void LateUpdate()
        {
            // call it while the NetworkManager exists.
            // -> we don't only call while Client/Server.Connected, because then we would stop if disconnected and the
            //    NetworkClient wouldn't receive the last Disconnect event, result in all kinds of issues
            NetworkIdentity.UNetStaticUpdate();
        }

        // When pressing Stop in the Editor, Unity keeps threads alive until we
        // press Start again (which might be a Unity bug).
        // Either way, we should disconnect client & server in OnApplicationQuit
        // so they don't keep running until we press Play again.
        // (this is not a problem in builds)
        //
        // virtual so that inheriting classes' OnApplicationQuit() can call base.OnApplicationQuit() too
        public virtual void OnApplicationQuit()
        {
            transport.Shutdown();
        }

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
                    transport = gameObject.AddComponent<TelepathyTransport>();
                    Debug.Log("NetworkManager: added default Transport because there was none yet.");
                }
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
            }

            maxConnections = Mathf.Max(maxConnections, 0); // always >= 0

            if (playerPrefab != null && playerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError("NetworkManager - playerPrefab must have a NetworkIdentity.");
                playerPrefab = null;
            }
        }

        internal void RegisterServerMessages()
        {
            NetworkServer.RegisterHandler(MsgType.Connect, OnServerConnectInternal);
            NetworkServer.RegisterHandler(MsgType.Disconnect, OnServerDisconnectInternal);
            NetworkServer.RegisterHandler(MsgType.Ready, OnServerReadyMessageInternal);
            NetworkServer.RegisterHandler(MsgType.AddPlayer, OnServerAddPlayerMessageInternal);
            NetworkServer.RegisterHandler(MsgType.RemovePlayer, OnServerRemovePlayerMessageInternal);
            NetworkServer.RegisterHandler(MsgType.Error, OnServerErrorInternal);
        }

        /// <summary>
        /// This starts a new server.
        /// </summary>
        /// <returns>True if the server was started.</returns>
        public bool StartServer()
        {
            InitializeSingleton();

            if (runInBackground)
                Application.runInBackground = true;

            if (!NetworkServer.Listen(maxConnections))
            {
                Debug.LogError("StartServer listen failed.");
                return false;
            }

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

            if (LogFilter.Debug) { Debug.Log("NetworkManager StartServer"); }
            isNetworkActive = true;

            // Only change scene if the requested online scene is not blank, and is not already loaded
            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (!string.IsNullOrEmpty(onlineScene) && onlineScene != loadedSceneName && onlineScene != offlineScene)
            {
                ServerChangeScene(onlineScene);
            }
            else
            {
                NetworkServer.SpawnObjects();
            }
            return true;
        }

        internal void RegisterClientMessages(NetworkClient client)
        {
            client.RegisterHandler(MsgType.Connect, OnClientConnectInternal);
            client.RegisterHandler(MsgType.Disconnect, OnClientDisconnectInternal);
            client.RegisterHandler(MsgType.NotReady, OnClientNotReadyMessageInternal);
            client.RegisterHandler(MsgType.Error, OnClientErrorInternal);
            client.RegisterHandler(MsgType.Scene, OnClientSceneInternal);

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
        /// This starts a new network client.
        /// It uses the networkAddress and networkPort properties as the address to connect to.
        /// </summary>
        /// <returns>The client object created.</returns>
        public NetworkClient StartClient()
        {
            InitializeSingleton();

            if (runInBackground)
                Application.runInBackground = true;

            isNetworkActive = true;

            client = new NetworkClient();

            RegisterClientMessages(client);

            if (string.IsNullOrEmpty(networkAddress))
            {
                Debug.LogError("Must set the Network Address field in the manager");
                return null;
            }
            if (LogFilter.Debug) { Debug.Log("NetworkManager StartClient address:" + networkAddress); }

            client.Connect(networkAddress);

            OnStartClient(client);
            s_Address = networkAddress;
            return client;
        }

        /// <summary>
        /// This starts a network "host" - a server and client in the same application.
        /// </summary>
        /// <returns>The client object created - this is a "local client".</returns>
        public virtual NetworkClient StartHost()
        {
            OnStartHost();
            if (StartServer())
            {
                NetworkClient localClient = ConnectLocalClient();
                OnStartClient(localClient);
                return localClient;
            }
            return null;
        }

        NetworkClient ConnectLocalClient()
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager StartHost"); }
            networkAddress = "localhost";
            client = ClientScene.ConnectLocalServer();
            RegisterClientMessages(client);
            return client;
        }

        /// <summary>
        /// This stops both the client and the server that the manager is using.
        /// </summary>
        public void StopHost()
        {
            OnStopHost();

            StopServer();
            StopClient();
        }

        /// <summary>
        /// Stops the server that the manager is using.
        /// </summary>
        public void StopServer()
        {
            if (!NetworkServer.active)
                return;

            OnStopServer();

            if (LogFilter.Debug) { Debug.Log("NetworkManager StopServer"); }
            isNetworkActive = false;
            NetworkServer.Shutdown();
            if (!string.IsNullOrEmpty(offlineScene))
            {
                ServerChangeScene(offlineScene);
            }
            CleanupNetworkIdentities();
        }

        /// <summary>
        /// Stops the client that the manager is using.
        /// </summary>
        public void StopClient()
        {
            OnStopClient();

            if (LogFilter.Debug) { Debug.Log("NetworkManager StopClient"); }
            isNetworkActive = false;
            if (client != null)
            {
                // only shutdown this client, not ALL clients.
                client.Disconnect();
                client.Shutdown();
                client = null;
            }

            ClientScene.DestroyAllClientObjects();
            if (!string.IsNullOrEmpty(offlineScene))
            {
                ClientChangeScene(offlineScene, false);
            }
            CleanupNetworkIdentities();
        }

        /// <summary>
        /// This causes the server to switch scenes and automatically sync the scenes.
        /// </summary>
        /// <param name="newSceneName">The name of the scene to change to.</param>
        public virtual void ServerChangeScene(string newSceneName)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                Debug.LogError("ServerChangeScene empty scene name");
                return;
            }

            if (LogFilter.Debug) { Debug.Log("ServerChangeScene " + newSceneName); }
            NetworkServer.SetAllClientsNotReady();
            networkSceneName = newSceneName;

            s_LoadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);

            StringMessage msg = new StringMessage(networkSceneName);
            NetworkServer.SendToAll((short)MsgType.Scene, msg);

            s_StartPositionIndex = 0;
            startPositions.Clear();
        }

        void CleanupNetworkIdentities()
        {
            foreach (NetworkIdentity identity in Resources.FindObjectsOfTypeAll<NetworkIdentity>())
            {
                identity.MarkForReset();
            }
        }

        internal void ClientChangeScene(string newSceneName, bool forceReload)
        {
            if (string.IsNullOrEmpty(newSceneName))
            {
                Debug.LogError("ClientChangeScene empty scene name");
                return;
            }

            if (LogFilter.Debug) { Debug.Log("ClientChangeScene newSceneName:" + newSceneName + " networkSceneName:" + networkSceneName); }

            if (newSceneName == networkSceneName)
            {
                if (!forceReload)
                {
                    FinishLoadScene();
                    return;
                }
            }

            // vis2k: pause message handling while loading scene. otherwise we will process messages and then lose all
            // the state as soon as the load is finishing, causing all kinds of bugs because of missing state.
            // (client may be null after StopClient etc.)
            if (client != null)
            {
                if (LogFilter.Debug) { Debug.Log("ClientChangeScene: pausing handlers while scene is loading to avoid data loss after scene was loaded."); }
                NetworkManager.singleton.transport.enabled = false;
            }

            // Let client prepare for scene change
            OnClientChangeScene(newSceneName);

            s_LoadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);
            networkSceneName = newSceneName;
        }

        void FinishLoadScene()
        {
            // NOTE: this cannot use NetworkClient.allClients[0] - that client may be for a completely different purpose.

            if (client != null)
            {
                // process queued messages that we received while loading the scene
                if (LogFilter.Debug) { Debug.Log("FinishLoadScene: resuming handlers after scene was loading."); }
                NetworkManager.singleton.transport.enabled = true;

                if (s_ClientReadyConnection != null)
                {
                    clientLoadedScene = true;
                    OnClientConnect(s_ClientReadyConnection);
                    s_ClientReadyConnection = null;
                }
            }
            else
            {
                if (LogFilter.Debug) { Debug.Log("FinishLoadScene client is null"); }
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
            if (singleton != null && s_LoadingSceneAsync != null && s_LoadingSceneAsync.isDone)
            {
                if (LogFilter.Debug) { Debug.Log("ClientChangeScene done readyCon:" + s_ClientReadyConnection); }
                singleton.FinishLoadScene();
                s_LoadingSceneAsync.allowSceneActivation = true;
                s_LoadingSceneAsync = null;
            }
        }

        // virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        public virtual void OnDestroy()
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager destroyed"); }
        }

        /// <summary>
        /// Register the transform of a game object as a player spawn location.
        /// </summary>
        /// <param name="start">Transform to register.</param>
        public static void RegisterStartPosition(Transform start)
        {
            if (LogFilter.Debug) { Debug.Log("RegisterStartPosition: (" + start.gameObject.name + ") " + start.position); }
            startPositions.Add(start);
        }

        /// <summary>
        /// Unregisters the transform of a game object as a player spawn location.
        /// </summary>
        /// <param name="start">Transform to unregister.</param>
        public static void UnRegisterStartPosition(Transform start)
        {
            if (LogFilter.Debug) { Debug.Log("UnRegisterStartPosition: (" + start.gameObject.name + ") " + start.position); }
            startPositions.Remove(start);
        }

        /// <summary>
        /// This checks if the NetworkManager has a client and that it is connected to the server.
        /// </summary>
        /// <returns>True if the NetworkManagers client is connected to a server.</returns>
        public bool IsClientConnected()
        {
            return client != null && client.isConnected;
        }

        /// <summary>
        /// Shuts down the NetworkManager completely and destroys the singleton.
        /// </summary>
        // this is the only way to clear the singleton, so another instance can be created.
        public static void Shutdown()
        {
            if (singleton == null)
                return;

            startPositions.Clear();
            s_StartPositionIndex = 0;
            s_ClientReadyConnection = null;

            singleton.StopHost();
            singleton = null;
        }

        // ----------------------------- Server Internal Message Handlers  --------------------------------

        internal void OnServerConnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnServerConnectInternal"); }
            if (networkSceneName != "" && networkSceneName != offlineScene)
            {
                StringMessage msg = new StringMessage(networkSceneName);
                netMsg.conn.Send((short)MsgType.Scene, msg);
            }

            OnServerConnect(netMsg.conn);
        }

        internal void OnServerDisconnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnServerDisconnectInternal"); }
            OnServerDisconnect(netMsg.conn);
        }

        internal void OnServerReadyMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnServerReadyMessageInternal"); }
            OnServerReady(netMsg.conn);
        }

        internal void OnServerAddPlayerMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnServerAddPlayerMessageInternal"); }

            AddPlayerMessage msg = netMsg.ReadMessage<AddPlayerMessage>();

            if (msg.value != null && msg.value.Length > 0)
            {
                // convert payload to extra message and call OnServerAddPlayer
                // (usually for character selection information)
                NetworkMessage extraMessage = new NetworkMessage
                {
                    reader = new NetworkReader(msg.value),
                    conn = netMsg.conn
                };
                OnServerAddPlayer(netMsg.conn, extraMessage);
            }
            else
            {
                OnServerAddPlayer(netMsg.conn);
            }
        }

        internal void OnServerRemovePlayerMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnServerRemovePlayerMessageInternal"); }

            if (netMsg.conn.playerController != null)
            {
                OnServerRemovePlayer(netMsg.conn, netMsg.conn.playerController);
                netMsg.conn.RemovePlayerController();
            }
        }

        internal void OnServerErrorInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnServerErrorInternal"); }

            NetworkError errorMessage = (NetworkError)netMsg;
            OnServerError(netMsg.conn, errorMessage.exception);
        }

        // ----------------------------- Client Internal Message Handlers  --------------------------------

        internal void OnClientConnectInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnClientConnectInternal"); }

            string loadedSceneName = SceneManager.GetSceneAt(0).name;
            if (string.IsNullOrEmpty(onlineScene) || onlineScene == offlineScene || loadedSceneName == onlineScene)
            {
                clientLoadedScene = false;
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
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnClientDisconnectInternal"); }

            OnClientDisconnect(netMsg.conn);
        }

        internal void OnClientNotReadyMessageInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnClientNotReadyMessageInternal"); }

            ClientScene.SetNotReady();
            OnClientNotReady(netMsg.conn);

            // NOTE: s_ClientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        internal void OnClientErrorInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnClientErrorInternal"); }

            NetworkError networkError = (NetworkError)netMsg;
            OnClientError(netMsg.conn, networkError.exception);
        }

        internal void OnClientSceneInternal(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkManager:OnClientSceneInternal"); }

            string newSceneName = netMsg.reader.ReadString();

            if (IsClientConnected() && !NetworkServer.active)
            {
                ClientChangeScene(newSceneName, true);
            }
        }

        // ----------------------------- Server System Callbacks --------------------------------

        /// <summary>
        /// Called on the server when a new client connects.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerConnect(NetworkConnection conn)
        {
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerDisconnect(NetworkConnection conn)
        {
            NetworkServer.DestroyPlayerForConnection(conn);
            if (LogFilter.Debug) { Debug.Log("OnServerDisconnect: Client disconnected."); }
        }

        /// <summary>
        /// Called on the server when a client is ready.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerReady(NetworkConnection conn)
        {
            if (conn.playerController == null)
            {
                // this is now allowed (was not for a while)
                if (LogFilter.Debug) { Debug.Log("Ready with no player object"); }
            }
            NetworkServer.SetClientReady(conn);
        }

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        /// <param name="extraMessageReader">An extra message object passed for the new player.</param>
        public virtual void OnServerAddPlayer(NetworkConnection conn, NetworkMessage extraMessage)
        {
            OnServerAddPlayerInternal(conn);
        }

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void OnServerAddPlayer(NetworkConnection conn)
        {
            OnServerAddPlayerInternal(conn);
        }

        void OnServerAddPlayerInternal(NetworkConnection conn)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("The PlayerPrefab is empty on the NetworkManager. Please setup a PlayerPrefab object.");
                return;
            }

            if (playerPrefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError("The PlayerPrefab does not have a NetworkIdentity. Please add a NetworkIdentity to the player prefab.");
                return;
            }

            if (conn.playerController != null)
            {
                Debug.LogError("There is already a player for this connections.");
                return;
            }

            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        /// <summary>
        /// This finds a spawn position based on NetworkStartPosition objects in the Scene.
        /// </summary>
        /// <returns>Returns the transform to spawn a player at, or null.</returns>
        public Transform GetStartPosition()
        {
            // first remove any dead transforms
            startPositions.RemoveAll(t => t == null);

            if (playerSpawnMethod == PlayerSpawnMethod.Random && startPositions.Count > 0)
            {
                // try to spawn at a random start location
                int index = UnityEngine.Random.Range(0, startPositions.Count);
                return startPositions[index];
            }
            if (playerSpawnMethod == PlayerSpawnMethod.RoundRobin && startPositions.Count > 0)
            {
                if (s_StartPositionIndex >= startPositions.Count)
                {
                    s_StartPositionIndex = 0;
                }

                Transform startPos = startPositions[s_StartPositionIndex];
                s_StartPositionIndex += 1;
                return startPos;
            }
            return null;
        }

        /// <summary>
        /// Called on the server when a client removes a player.
        /// </summary>
        /// <param name="conn">The connection to remove the player from.</param>
        /// <param name="player">The player controller to remove.</param>
        public virtual void OnServerRemovePlayer(NetworkConnection conn, NetworkIdentity player)
        {
            if (player.gameObject != null)
            {
                NetworkServer.Destroy(player.gameObject);
            }
        }

        /// <summary>
        /// Called on the server when a network error occurs for a client connection.
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        /// <param name="exception">The error.</param>
        public virtual void OnServerError(NetworkConnection conn, Exception exception)
        {
            // default implementation just logs the exception
            Debug.LogException(exception, this);
        }

        /// <summary>
        /// Called on the server when a scene is completely loaded, when the scene load was initiated by the server with ServerChangeScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        public virtual void OnServerSceneChanged(string sceneName)
        {
        }

        // ----------------------------- Client System Callbacks --------------------------------

        /// <summary>
        /// Called on the client when connected to a server.
        /// </summary>
        /// <param name="conn">Connection to the server.</param>
        public virtual void OnClientConnect(NetworkConnection conn)
        {
            if (!clientLoadedScene)
            {
                // Ready/AddPlayer is usually triggered by a scene load completing. if no scene was loaded, then Ready/AddPlayer it here instead.
                ClientScene.Ready(conn);
                if (autoCreatePlayer)
                {
                    ClientScene.AddPlayer();
                }
            }
        }

        /// <summary>
        /// Called on clients when disconnected from a server.
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
        /// <param name="exception">The error.</param>
        public virtual void OnClientError(NetworkConnection conn, Exception exception)
        {
            // default implementation just logs the exception
            Debug.LogException(exception, this);
        }

        /// <summary>
        /// Called on clients when a servers tells the client it is no longer ready.
        /// This is commonly used when switching Scenes.
        /// </summary>
        /// <param name="conn">	Connection to a server.</param>
        public virtual void OnClientNotReady(NetworkConnection conn)
        {
        }

        // Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        // This allows client to do work / cleanup / prep before the scene changes.
        public virtual void OnClientChangeScene(string newSceneName) { }

        /// <summary>
        /// Called on clients when a scene has completley loaded, when the scene load was initiated by the server.
        /// </summary>
        /// <param name="conn">	The network connection that the scene change message arrived on.</param>
        public virtual void OnClientSceneChanged(NetworkConnection conn)
        {
            // always become ready.
            ClientScene.Ready(conn);

            // vis2k: replaced all this weird code with something more simple
            if (autoCreatePlayer)
            {
                // add player if existing one is null
                if (ClientScene.localPlayer == null)
                {
                    ClientScene.AddPlayer();
                }
            }
        }

        //------------------------------ Start & Stop callbacks -----------------------------------

        // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
        // their functionality, users would need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

        /// <summary>Called when the host is started.</summary>
        public virtual void OnStartHost() {}
        /// <summary>Called when the server is started - including when a host is started.</summary>
        public virtual void OnStartServer() {}
        /// <summary>Called when the client is started.</summary>
        public virtual void OnStartClient(NetworkClient client) {}
        /// <summary>Called when the server is stopped - including when a host is stopped.</summary>
        public virtual void OnStopServer() {}
        /// <summary>Called when the client is stopped.</summary>
        public virtual void OnStopClient() {}
        /// <summary>Called when the host is stopped.</summary>
        public virtual void OnStopHost() {}
    }
}
