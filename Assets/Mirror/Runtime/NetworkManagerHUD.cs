// vis2k: GUILayout instead of spacey += ...; removed Update hotkeys to avoid
// confusion if someone accidentally presses one.

using System;
using System.Collections;
using System.Net;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// An extension for the NetworkManager that displays a default HUD for controlling the network state of the game.
    /// <para>This component also shows useful internal state for the networking system in the inspector window of the editor. It allows users to view connections, networked objects, message handlers, and packet statistics. This information can be helpful when debugging networked games.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkManagerHUD")]
    [RequireComponent(typeof(NetworkManager))]
    [HelpURL("https://mirror-networking.com/docs/Articles/Components/NetworkManagerHUD.html")]
    public class NetworkManagerHUD : MonoBehaviour
    {
        NetworkManager manager;

        /// <summary>
        /// Whether to show the default control HUD at runtime.
        /// </summary>
        public bool showGUI = true;

        /// <summary>
        /// The horizontal offset in pixels to draw the HUD runtime GUI at.
        /// </summary>
        public int offsetX;

        /// <summary>
        /// The vertical offset in pixels to draw the HUD runtime GUI at.
        /// </summary>
        public int offsetY;

        /// <summary>
        /// Outputs the current devices IP to gui input, useful if your are host/server.
        /// Only use the WAN check when necessary, as it uses an external service.
        /// For further connection information, visit: https://mirror-networking.com/docs/Articles/FAQ.html#how-to-connect
        /// </summary>
        [Tooltip("localhost for games on same device, LAN for different devices same network, WAN for external connections. See summary for more information.")]
        [SerializeField]
        private IPType _IPType = IPType.localhost;
        private string inputField;
        [System.Serializable]
        private enum IPType : int { localhost = 0, LAN = 1, WAN = 2 }
        private string wanIP = "";

        private string[] _ipType;
        private int _ipTypeSelectedIndex = 0;
        private bool _ShowIpTypeDropdown;
        private Vector2 scrollViewVector = Vector2.zero;

        void Awake()
        {
            manager = GetComponent<NetworkManager>();

            SetIPInformation();

            _ipType = Enum.GetNames(typeof(IPType));
        }

        void OnGUI()
        {
            if (!showGUI)
                return;

            GUILayout.BeginArea(new Rect(10 + offsetX, 40 + offsetY, 215, 9999));
            if (!NetworkClient.isConnected && !NetworkServer.active)
            {
                StartButtons();
            }
            else
            {
                StatusLabels();
            }

            // client ready
            if (NetworkClient.isConnected && !ClientScene.ready)
            {
                if (GUILayout.Button("Client Ready"))
                {
                    ClientScene.Ready(NetworkClient.connection);

                    if (ClientScene.localPlayer == null)
                    {
                        ClientScene.AddPlayer(NetworkClient.connection);
                    }
                }
            }

            StopButtons();

            GUILayout.EndArea();
        }

        void StartButtons()
        {
            if (!NetworkClient.active)
            {
            
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label($"IP Type : {_ipType[_ipTypeSelectedIndex]}");
                    {
                        if (GUILayout.Button("▼"))
                        {
                            _ShowIpTypeDropdown = !_ShowIpTypeDropdown;
                        }
                    }
                }
                GUILayout.EndHorizontal();

                if (_ShowIpTypeDropdown)
                {
                    using (GUILayout.ScrollViewScope scope = new GUILayout.ScrollViewScope(scrollViewVector))
                    {
                        scrollViewVector = scope.scrollPosition;
                        for (int index = 0; index < _ipType.Length; index++)
                        {
                            if (!GUILayout.Button(_ipType[index]))
                            {
                                continue;
                            }
                            else
                            {
                                if (index == 0) { _IPType = IPType.localhost; }
                                else if (index == 1) { _IPType = IPType.LAN; }
                                else if (index == 2) { _IPType = IPType.WAN; }
                                SetIPInformation();
                            }

                            _ShowIpTypeDropdown = false;
                            _ipTypeSelectedIndex = index;
                        }
                    }
                }

                // Server + Client
                if (Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    if (GUILayout.Button("Host (Server + Client)"))
                    {
                        manager.StartHost();
                    }
                }

                // Client + IP
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Client"))
                {
                    manager.networkAddress = inputField;
                    manager.StartClient();
                }
                
                inputField = GUILayout.TextField(inputField);
                
                GUILayout.EndHorizontal();

                // Server Only
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    // cant be a server in webgl build
                    GUILayout.Box("(  WebGL cannot be server  )");
                }
                else
                {
                    if (GUILayout.Button("Server Only")) manager.StartServer();
                }
            }
            else
            {
                // Connecting
                GUILayout.Label("Connecting to " + inputField + "..");
                if (GUILayout.Button("Cancel Connection Attempt"))
                {
                    manager.StopClient();
                }
            }
        }

        void StatusLabels()
        {
            // server / client status message
            if (NetworkServer.active)
            {
                GUILayout.Label("Server: active. Transport: " + Transport.activeTransport);
            }
            if (NetworkServer.active)
            {
                GUILayout.Label("Server address: " + inputField);
            }
            else if (NetworkClient.isConnected)
            {
                GUILayout.Label("Connected to: " + manager.networkAddress);
            }
        }

        void StopButtons()
        {
            // stop host if host mode
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                if (GUILayout.Button("Stop Host"))
                {
                    manager.StopHost();
                }
            }
            // stop client if client-only
            else if (NetworkClient.isConnected)
            {
                if (GUILayout.Button("Stop Client"))
                {
                    manager.StopClient();
                }
            }
            // stop server if server-only
            else if (NetworkServer.active)
            {
                if (GUILayout.Button("Stop Server"))
                {
                    manager.StopServer();
                }
            }
        }

        void SetIPInformation()
        {
            if (_IPType == IPType.localhost)
            {
                inputField = "localhost";
                manager.networkAddress = inputField;
            }
            else if (_IPType == IPType.LAN)
            {
                GetLanAddress();
            }
            else if (_IPType == IPType.WAN)
            {
                StartCoroutine(GetWanAddress());
            }
        }

        private void GetLanAddress()
        {
            string hostName = Dns.GetHostName();
            IPAddress iPAddress = Dns.GetHostEntry(hostName).AddressList[0];

            if (hostName != "" && iPAddress != null && iPAddress.ToString() != "")
            {
                inputField = Dns.GetHostEntry(hostName).AddressList[0].ToString();
                manager.networkAddress = inputField;
            }
        }

        IEnumerator GetWanAddress()
        {
            if (wanIP != "") { inputField = wanIP; yield break; }
            
            using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequest.Get("https://api.ipify.org/"))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.isNetworkError || webRequest.isHttpError || webRequest.downloadHandler.text == "")
                {
                    Debug.Log("Public (WAN) IP Error: " + webRequest.error);
                }
                else
                {
                    wanIP = webRequest.downloadHandler.text;
                    inputField = wanIP;
                    manager.networkAddress = wanIP;
                }
            }
        }
    }
}
