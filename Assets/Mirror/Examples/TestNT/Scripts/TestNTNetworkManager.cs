#if UNITY_SERVER
using System;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Mirror.SimpleWeb;
using kcp2k;

namespace TestNT
{
    [AddComponentMenu("")]
    public class TestNTNetworkManager : NetworkManager
    {
        // Overrides the base singleton so we don't
        // have to cast to this type everywhere.
        public static new TestNTNetworkManager singleton { get; private set; }

        public GameObject playerNinjaPrefab;

        public GameObject botPrefab;
        public GameObject npcPrefab;

        public GameObject botNinjaPrefab;
        public GameObject npcNinjaPrefab;

        /// <summary>
        /// Runs on both Server and Client
        /// Networking is NOT initialized when this fires
        /// </summary>
        public override void Awake()
        {
            base.Awake();
            singleton = this;
        }

        // Called by OnValueChanged of Login UI element NetworkAddressInput
        public void SetHostname(string hostname)
        {
            networkAddress = hostname;
        }

        // Called by OnValueChanged of Login UI element NetworkAddressDropdown
        public void OnSelectServer(int server)
        {
            if (server == 0)
            {
                SetHostname("mirror.clevertech.net");

                if (transport is SimpleWebTransport swt)
                    swt.port = 7777;

                if (transport is KcpTransport kcp)
                    kcp.Port = 7777;
            }
            if (server == 1)
            {
                SetHostname("stresstest.idev.dl.je");

                if (transport is SimpleWebTransport swt)
                    swt.port = 443;

                if (transport is KcpTransport kcp)
                    kcp.Port = 443;
            }
            if (server == 2)
            {
                SetHostname("localhost");

                if (transport is SimpleWebTransport swt)
                    swt.port = 27777;

                if (transport is KcpTransport kcp)
                    kcp.Port = 27777;
            }
        }

        #region Unity Callbacks

#if UNITY_SERVER
        public override void Start()
        {
            if (autoStartServerBuild)
            {
                // set default sendRate, then let CmdLineArgs override
                Application.targetFrameRate = 30;

                if (Transport.active is SimpleWebTransport swt)
                    swt.port = 27777;

                if (Transport.active is kcp2k.KcpTransport kcp)
                    kcp.Port = 27777;

                ProcessCmdLineArgs();

                if (Transport.active is SimpleWebTransport swt2)
                {
                    swt2.sslEnabled = false;
                    swt2.clientUseWss = false;
                }

                StartServer();
            }
            // only start server or client, never both
            else if (autoConnectClientBuild)
            {
                // set default sendRate, then let CmdLineArgs override
                Application.targetFrameRate = 60;

                if (Transport.active is SimpleWebTransport swt)
                {
                    swt.sslEnabled = true;
                    swt.clientUseWss = true;
                }

                ProcessCmdLineArgs();

                ((TestNTNetworkAuthenticator)authenticator).SetPlayername($"Bot[{sendRate}] ", true);

                StartClient();
            }
        }

        void ProcessCmdLineArgs()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("/h:", StringComparison.InvariantCultureIgnoreCase))
                    networkAddress = arg.Remove(0, 3);

                if (arg.StartsWith("/p:", StringComparison.InvariantCultureIgnoreCase))
                    if (ushort.TryParse(arg.Remove(0, 3), out ushort port))
                    {
                        if (transport is SimpleWebTransport swt)
                            swt.port = port;

                        if (transport is KcpTransport kcp)
                            kcp.Port = port;
                    }

                if (arg.Equals("/ssl", StringComparison.InvariantCultureIgnoreCase) && Transport.active is SimpleWebTransport swt2)
                {
                    swt2.clientUseWss = true;
                    swt2.sslEnabled = true;
                }

                if (arg.StartsWith("/ninja:", StringComparison.InvariantCultureIgnoreCase))
                    if (uint.TryParse(arg.Remove(0, 7), out uint multiplier))
                    {
                        ((TestNTNetworkAuthenticator)authenticator).SetNinja(true);
                        ((TestNTNetworkAuthenticator)authenticator).SetMultiplier(multiplier.ToString());
                    }

                if (arg.Equals("/nossl", StringComparison.InvariantCultureIgnoreCase) && Transport.active is SimpleWebTransport swt3)
                {
                    swt3.clientUseWss = false;
                    swt3.sslEnabled = false;
                }

                if (arg.StartsWith("/r:", StringComparison.InvariantCultureIgnoreCase))
                    if (int.TryParse(arg.Remove(0, 3), out sendRate))
                        Application.targetFrameRate = sendRate;

                if (arg.StartsWith("/m:", StringComparison.InvariantCultureIgnoreCase))
                    float.TryParse(arg.Remove(0, 3), out bufferTimeMultiplierForClamping);
            }
        }
#endif

        /// <summary>
        /// Runs on both Server and Client
        /// </summary>
        public override void LateUpdate()
        {
            base.LateUpdate();
        }

        /// <summary>
        /// Runs on both Server and Client
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
        }

#endregion

#region Scene Management

        /// <summary>
        /// This causes the server to switch scenes and sets the networkSceneName.
        /// <para>Clients that connect to this server will automatically switch to this scene. This is called automatically if onlineScene or offlineScene are set, but it can be called from user code to switch scenes again while the game is in progress. This automatically sets clients to be not-ready. The clients must call NetworkClient.Ready() again to participate in the new scene.</para>
        /// </summary>
        /// <param name="newSceneName"></param>
        public override void ServerChangeScene(string newSceneName)
        {
            base.ServerChangeScene(newSceneName);
        }

        /// <summary>
        /// Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        public override void OnServerChangeScene(string newSceneName) { }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ServerChangeScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        public override void OnServerSceneChanged(string sceneName) { }

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        /// <param name="customHandling">true to indicate that scene loading will be handled through overrides</param>
        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            FindObjectOfType<NetworkManagerHUD>().enabled = newSceneName == onlineScene;
        }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
        }

#endregion

#region Server System Callbacks

        /// <summary>
        /// Called on the server when a new client connects.
        /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerConnect(NetworkConnectionToClient conn) { }

        /// <summary>
        /// Called on the server when a client is ready.
        /// <para>The default implementation of this function calls NetworkServer.SetClientReady() to continue the network setup process.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            base.OnServerReady(conn);
        }

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            TestNTNetworkAuthenticator.AuthRequestMessage authData = (TestNTNetworkAuthenticator.AuthRequestMessage)conn.authenticationData;

            GameObject player;

            if (authData.isBot)
            {
                if (authData.useNinja)
                    player = Instantiate(botNinjaPrefab);
                else
                    player = Instantiate(botPrefab);
            }
            else
            {
                if (authData.useNinja)
                    player = Instantiate(playerNinjaPrefab);
                else
                    player = Instantiate(playerPrefab);
            }

            if (authData.useNinja)
                player.GetComponent<NTRCustomSendInterval>().sendIntervalMultiplier = authData.multiplier;

            player.transform.LookAt(new Vector3(0f, 1f, 0f));

            PlayerName playerName = player.GetComponent<PlayerName>();
            if (authData.isBot)
                playerName.playerName = $"{authData.authUsername}{conn.connectionId:0000}";
            else
                playerName.playerName = authData.authUsername;

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // remove player name from the HashSet
            if (conn.authenticationData != null)
            {
                TestNTNetworkAuthenticator.AuthRequestMessage authData = (TestNTNetworkAuthenticator.AuthRequestMessage)conn.authenticationData;
                PlayerName.playerNames.Remove(authData.authUsername);
            }

            // remove connection from Dictionary of conn > names
            PlayerName.connNames.Remove(conn);

            base.OnServerDisconnect(conn);
        }

        /// <summary>
        /// Called on server when transport raises an exception.
        /// <para>NetworkConnection may be null.</para>
        /// </summary>
        /// <param name="conn">Connection of the client...may be null</param>
        /// <param name="exception">Exception thrown from the Transport.</param>
        public override void OnServerError(NetworkConnectionToClient conn, TransportError transportError, string message) { }

#endregion

#region Client System Callbacks

        /// <summary>
        /// Called on the client when connected to a server.
        /// <para>The default implementation of this function sets the client as ready and adds a player. Override the function to dictate what happens when the client connects.</para>
        /// </summary>
        public override void OnClientConnect()
        {
            base.OnClientConnect();
        }

        /// <summary>
        /// Called on clients when disconnected from a server.
        /// <para>This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.</para>
        /// </summary>
        public override void OnClientDisconnect()
        {
            Debug.Log("OnClientDisconnect");

            if (SceneManager.GetActiveScene().path != offlineScene) return;

            // If we're in offline scene, we failed to connect...

            LoginUI.instance.networkAddressDropdown.interactable = true;
            LoginUI.instance.usernameInput.interactable = true;
            LoginUI.instance.ninjaToggle.interactable = true;
            LoginUI.instance.multiplierInput.interactable = true;

            LoginUI.instance.ToggleButtons(LoginUI.instance.usernameInput.text);
        }

        /// <summary>
        /// Called on clients when a servers tells the client it is no longer ready.
        /// <para>This is commonly used when switching scenes.</para>
        /// </summary>
        public override void OnClientNotReady() { }

        /// <summary>
        /// Called on client when transport raises an exception.</summary>
        /// </summary>
        /// <param name="exception">Exception thrown from the Transport.</param>
        public override void OnClientError(TransportError transportError, string message)
        {
            Debug.LogError($"OnClientError {transportError} {message}");
        }

#endregion

#region Start & Stop Callbacks

        // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
        // their functionality, users would need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

        /// <summary>
        /// This is invoked when a host is started.
        /// <para>StartHost has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartHost() { }

        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartServer()
        {
            PlayerName.connNames.Clear();
        }

        /// <summary>
        /// This is invoked when the client is started.
        /// </summary>
        public override void OnStartClient()
        {
            NetworkClient.RegisterPrefab(botPrefab);
            NetworkClient.RegisterPrefab(npcPrefab);
            NetworkClient.RegisterPrefab(playerNinjaPrefab);
            NetworkClient.RegisterPrefab(botNinjaPrefab);
            NetworkClient.RegisterPrefab(npcNinjaPrefab);
        }

        /// <summary>
        /// This is called when a host is stopped.
        /// </summary>
        public override void OnStopHost() { }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public override void OnStopServer() { }

        /// <summary>
        /// This is called when a client is stopped.
        /// </summary>
        public override void OnStopClient() { }

#endregion
    }
}
