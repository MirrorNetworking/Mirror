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
        public GameObject botNinjaPrefab;

        public GameObject npcPrefab;
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
                {
                    swt.port = 7778;
                    swt.clientUseWss = true;
                }

                if (transport is KcpTransport kcp)
                    kcp.Port = 7778;
            }
            if (server == 1)
            {
                SetHostname("stresstest.idev.dl.je");

                if (transport is SimpleWebTransport swt)
                {
                    swt.port = 443;
                    swt.clientUseWss = true;
                }

                if (transport is KcpTransport kcp)
                    kcp.Port = 443;
            }
            if (server == 2)
            {
                SetHostname("localhost");

                if (transport is SimpleWebTransport swt)
                {
                    swt.port = 27778;
                    swt.clientUseWss = false;
                }

                if (transport is KcpTransport kcp)
                    kcp.Port = 27778;
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
                    swt.port = 27778;

                if (Transport.active is kcp2k.KcpTransport kcp)
                    kcp.Port = 27778;

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
            // Initialize false for bots, arg will set it true if present
            ((TestNTNetworkAuthenticator)authenticator).SetNinja(false);

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
            }
        }
#endif

#endregion

#region Scene Management

        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            FindObjectOfType<NetworkManagerHUD>().enabled = newSceneName == onlineScene;
        }

#endregion

#region Server System Callbacks

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

#endregion

#region Client System Callbacks

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

#endregion

#region Start & Stop Callbacks

        public override void OnStartServer()
        {
            PlayerName.connNames.Clear();
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterPrefab(botPrefab);
            NetworkClient.RegisterPrefab(npcPrefab);
            NetworkClient.RegisterPrefab(playerNinjaPrefab);
            NetworkClient.RegisterPrefab(botNinjaPrefab);
            NetworkClient.RegisterPrefab(npcNinjaPrefab);
        }

#endregion
    }
}
