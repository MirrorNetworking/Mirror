using Assets.Scripts.NetworkMessages;
using Assets.Scripts.Utility.Serialisation;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkDiscoveryHUD")]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Transports/NetworkDiscovery.html")]
    [RequireComponent(typeof(NetworkDiscovery))]
    public class NetworkDiscoveryHUD : MonoBehaviour
    {
        Dictionary<string, DiscoveryInfo> discoveredServers = new Dictionary<string, DiscoveryInfo>();
        string[] headerNames = new string[] { "IP", "Host" };
        Vector2 scrollViewPos = Vector2.zero;

        GUIStyle centeredLabelStyle;

        public int offsetX = 5;
        public int offsetY = 150;
        public int width = 500, height = 400;

        void OnEnable()
        {
            NetworkDiscovery.onReceivedServerResponse += OnDiscoveredServer;
        }

        void OnDisable()
        {
            NetworkDiscovery.onReceivedServerResponse -= OnDiscoveredServer;
        }

        void OnGUI()
        {
            if (NetworkManager.singleton == null) return;

            if (NetworkServer.active || NetworkClient.active) return;

            if (!NetworkDiscovery.SupportedOnThisPlatform)  return;

            if (centeredLabelStyle == null)
            {
                centeredLabelStyle = new GUIStyle(GUI.skin.label);
                centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            }

            int elemWidth = width / headerNames.Length - 5;

            GUILayout.BeginArea(new Rect(offsetX, offsetY, width, height));

            // In my own game I ripped this out, this is just as an example (wanted to avoid adding a NetworkManager to the sample)
            if (!NetworkClient.isConnected && !NetworkServer.active)
            {
                if (!NetworkClient.active)
                {
                    // LAN Host
                    if (GUILayout.Button("Passive Host", GUILayout.Height(25), GUILayout.ExpandWidth(false)))
                    {
                        discoveredServers.Clear();
                        NetworkManager.singleton.StartHost();

                        // Wire in broadcaster pipeline here
                        GameBroadcastPacket gameBroadcastPacket = new GameBroadcastPacket();

                        gameBroadcastPacket.serverAddress = NetworkManager.singleton.networkAddress;
                        gameBroadcastPacket.port = ((TelepathyTransport)Transport.activeTransport).port;
                        gameBroadcastPacket.hostName = "MyDistinctDummyPlayerName";
                        gameBroadcastPacket.serverGUID = NetworkDiscovery.instance.serverId;

                        byte[] broadcastData = ByteStreamer.StreamToBytes(gameBroadcastPacket);
                        NetworkDiscovery.instance.ServerPassiveBroadcastGame(broadcastData);
                    }
                }
            }

            if (GUILayout.Button("Active Discovery", GUILayout.Height(25), GUILayout.ExpandWidth(false)))
            {
                discoveredServers.Clear();
                NetworkDiscovery.instance.ClientRunActiveDiscovery();
            }

            GUILayout.Label(string.Format("Servers [{0}]:", discoveredServers.Count));

            // header
            GUILayout.BeginHorizontal();

            foreach (string str in headerNames)
                GUILayout.Label(str, GUILayout.Width(elemWidth));

            GUILayout.EndHorizontal();

            // servers
            scrollViewPos = GUILayout.BeginScrollView(scrollViewPos);

            foreach (var info in discoveredServers.Values)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(info.EndPoint.Address.ToString(), GUILayout.Width(elemWidth)))
                    Connect(info);

                for (int i = 0; i < headerNames.Length; i++)
                {
                    if (i == 0)
                        GUILayout.Label(info.unpackedData.serverAddress, centeredLabelStyle, GUILayout.Width(elemWidth));
                    else
                        GUILayout.Label(info.unpackedData.hostName, centeredLabelStyle, GUILayout.Width(elemWidth));
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        void Connect(DiscoveryInfo info)
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

            ((TelepathyTransport)Transport.activeTransport).port = (ushort)info.unpackedData.port;

            NetworkManager.singleton.StartClient();
        }

        void OnDiscoveredServer(DiscoveryInfo info)
        {
            // Note that you can check the versioning to decide if you can connect to the server or not using this method
            discoveredServers[info.unpackedData.serverGUID] = info;
        }
    }
}
