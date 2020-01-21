using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Discovery
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkDiscoveryHUD")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkDiscovery.html")]
    [RequireComponent(typeof(NetworkDiscovery))]
    public class NetworkDiscoveryHUD : MonoBehaviour
    {
        readonly Dictionary<long, ServerInfo> discoveredServers = new Dictionary<long, ServerInfo>();
        Vector2 scrollViewPos = Vector2.zero;

        public NetworkDiscovery networkDiscovery;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (networkDiscovery == null)
                networkDiscovery = GetComponent<NetworkDiscovery>();
        }
#endif

        void OnEnable()
        {
            NetworkDiscovery.OnServerFound += OnDiscoveredServer;
        }

        void OnDisable()
        {
            NetworkDiscovery.OnServerFound -= OnDiscoveredServer;
        }

        void OnGUI()
        {
            if (NetworkManager.singleton == null)
                return;

            if (NetworkServer.active || NetworkClient.active)
                return;

            // In my own game I ripped this out, this is just as an example (wanted to avoid adding a NetworkManager to the sample)
            if (!NetworkClient.isConnected && !NetworkServer.active && !NetworkClient.active)
                DrawGUI();
        }

        private void DrawGUI()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Find Servers"))
            {
                discoveredServers.Clear();
                networkDiscovery.StartDiscovery();
            }

            // LAN Host
            if (GUILayout.Button("Start Host"))
            {
                discoveredServers.Clear();
                NetworkManager.singleton.StartHost();
                networkDiscovery.AdvertiseServer();
            }

            // Dedicated server
            if (GUILayout.Button("Start Server"))
            {
                discoveredServers.Clear();
                NetworkManager.singleton.StartServer();

                networkDiscovery.AdvertiseServer();
            }

            GUILayout.EndHorizontal();

            // show list of found server

            GUILayout.Label($"Discovered Servers [{discoveredServers.Count}]:");

            // servers
            scrollViewPos = GUILayout.BeginScrollView(scrollViewPos);

            foreach (ServerInfo info in discoveredServers.Values)
                if (GUILayout.Button(info.EndPoint.Address.ToString()))
                    Connect(info);

            GUILayout.EndScrollView();
        }

        void Connect(ServerInfo info)
        {
            if (NetworkManager.singleton == null || Transport.activeTransport == null)
                return;

            if (!(Transport.activeTransport is TelepathyTransport))
            {
                Debug.LogErrorFormat("Only {0} is supported", typeof(TelepathyTransport));
                return;
            }

            // assign address and port
            NetworkManager.singleton.networkAddress = info.EndPoint.Address.ToString();

            ((TelepathyTransport)Transport.activeTransport).port = (ushort)info.port;

            NetworkManager.singleton.StartClient();
        }

        void OnDiscoveredServer(ServerInfo info)
        {
            // Note that you can check the versioning to decide if you can connect to the server or not using this method
            discoveredServers[info.serverId] = info;
        }
    }
}
