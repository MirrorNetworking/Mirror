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
        readonly Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();
        Vector2 scrollViewPos = Vector2.zero;

        public NetworkDiscovery networkDiscovery;
        private GUIStyle buttonStyle;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (networkDiscovery == null)
            {
                networkDiscovery = GetComponent<NetworkDiscovery>();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(networkDiscovery.OnServerFound, OnDiscoveredServer);
                UnityEditor.Undo.RecordObjects(new Object[] { this, networkDiscovery }, "Set NetworkDiscovery");
            }
        }
#endif

        void OnGUI()
        {
            if (NetworkManager.singleton == null)
                return;

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle("button");
                buttonStyle.fontSize = 22;
                buttonStyle.fixedWidth = 160;
                buttonStyle.fixedHeight = 80;
            }

            DrawGUI();
        }

        void DrawGUI()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Find Servers", buttonStyle))
            {
                discoveredServers.Clear();
                networkDiscovery.StartDiscovery();
            }

            if (GUILayout.Button("Stop Discovery", buttonStyle))
            {
                networkDiscovery.StopDiscovery();
            }
            // LAN Host
            if (GUILayout.Button("Start Host", buttonStyle))
            {
                discoveredServers.Clear();
                NetworkManager.singleton.StartHost();
                networkDiscovery.AdvertiseServer();
            }

            // Dedicated server
            if (GUILayout.Button("Start Server", buttonStyle))
            {
                discoveredServers.Clear();
                NetworkManager.singleton.StartServer();
                networkDiscovery.AdvertiseServer();
            }

            if (NetworkServer.active && NetworkClient.isConnected)
            {
                if (GUILayout.Button("Stop Host", buttonStyle))
                {
                    NetworkManager.singleton.StopHost();
                }
            }
            // stop client if client-only
            else if (NetworkClient.isConnected)
            {
                if (GUILayout.Button("Stop Client", buttonStyle))
                {
                    NetworkManager.singleton.StopClient();
                }
            }
            // stop server if server-only
            else if (NetworkServer.active)
            {
                if (GUILayout.Button("Stop Server", buttonStyle))
                {
                    NetworkManager.singleton.StopServer();
                }
            }

            GUILayout.EndHorizontal();

            // show list of found server

            GUILayout.Label($"Discovered Servers [{discoveredServers.Count}]:");

            // servers
            scrollViewPos = GUILayout.BeginScrollView(scrollViewPos);

            foreach (ServerResponse info in discoveredServers.Values)
                if (GUILayout.Button(info.EndPoint.Address.ToString(), buttonStyle))
                    Connect(info);

            GUILayout.EndScrollView();
        }

        void Connect(ServerResponse info)
        {
            NetworkManager.singleton.StartClient(info.uri);
        }

        public void OnDiscoveredServer(ServerResponse info)
        {
            // Note that you can check the versioning to decide if you can connect to the server or not using this method
            discoveredServers[info.serverId] = info;
        }
    }
}
