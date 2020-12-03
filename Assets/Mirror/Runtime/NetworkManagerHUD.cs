// vis2k: GUILayout instead of spacey += ...; removed Update hotkeys to avoid
// confusion if someone accidentally presses one.
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
        [Tooltip("localhost for games on same device, LAN for different devices same wifi, WAN for external connections. See summary for more information.")]
        [SerializeField]
        private IPType _IPType = IPType.localhost;
        private string inputField; 
        [System.Serializable]
        private enum IPType : int { localhost = 0, LAN = 1, WAN = 2 }

        void Awake()
        {
            manager = GetComponent<NetworkManager>();

            if (_IPType != IPType.localhost) { StartCoroutine(SetIPInformation()); }
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
                    manager.StartClient();
                }
                inputField = manager.networkAddress;
                manager.networkAddress = GUILayout.TextField(inputField);
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
                GUILayout.Label("Connecting to " + manager.networkAddress + "..");
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
            if (NetworkClient.isConnected)
            {
                GUILayout.Label("Client: address=" + manager.networkAddress);
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
        
        System.Collections.IEnumerator SetIPInformation()
        {
            if (_IPType == IPType.LAN)
            {
                using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    System.Net.IPEndPoint endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                   // Debug.Log("Local Area Network (LAN) IP: " + endPoint.Address.ToString());
                    inputField = endPoint.Address.ToString();
                   
                }
            }
            else if (_IPType == IPType.WAN)
            {
                using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequest.Get("https://api.ipify.org/"))
                {
                    yield return webRequest.SendWebRequest();

                    if (webRequest.isNetworkError || webRequest.isHttpError || webRequest.downloadHandler.text == "")
                    { Debug.Log("Public (WAN) IP Error: " + webRequest.error); }
                    //else { Debug.Log("Public (WAN) IP: " + webRequest.downloadHandler.text); }
                    inputField = webRequest.downloadHandler.text.ToString();
                }
            }
            manager.networkAddress = inputField;
        }
    }
}
