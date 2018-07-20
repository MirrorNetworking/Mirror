// vis2k: GUILayout instead of spacey += ...; removed Update hotkeys to avoid
// confusion if someone accidentally presses one.
using System;
using System.ComponentModel;

#if ENABLE_UNET

namespace UnityEngine.Networking
{
    [AddComponentMenu("Network/NetworkManagerHUD")]
    [RequireComponent(typeof(NetworkManager))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class NetworkManagerHUD : MonoBehaviour
    {
        public NetworkManager manager;
        public bool showGUI = true;
        public int offsetX;
        public int offsetY;

        void Awake()
        {
            manager = GetComponent<NetworkManager>();
        }

        void OnGUI()
        {
            if (!showGUI)
                return;

            bool noConnection = (manager.client == null || manager.client.connection == null ||
                                 manager.client.connection.connectionId == -1);

            GUILayout.BeginArea(new Rect(10 + offsetX, 40 + offsetY, 215, 9999));
            if (!manager.IsClientConnected() && !NetworkServer.active)
            {
                if (noConnection)
                {
                    // LAN Host
                    if (Application.platform != RuntimePlatform.WebGLPlayer)
                    {
                        if (GUILayout.Button("LAN Host"))
                        {
                            manager.StartHost();
                        }
                    }

                    // LAN Client + IP
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("LAN Client"))
                    {
                        manager.StartClient();
                    }
                    manager.networkAddress = GUILayout.TextField(manager.networkAddress);
                    GUILayout.EndHorizontal();

                    // LAN Server Only
                    if (Application.platform == RuntimePlatform.WebGLPlayer)
                    {
                        // cant be a server in webgl build
                        GUILayout.Box("(  WebGL cannot be server  )");
                    }
                    else
                    {
                        if (GUILayout.Button("LAN Server Only")) manager.StartServer();
                    }
                }
                else
                {
                    // Connecting
                    GUILayout.Label("Connecting to " + manager.networkAddress + ":" + manager.networkPort + "..");
                    if (GUILayout.Button("Cancel Connection Attempt"))
                    {
                        manager.StopClient();
                    }
                }
            }
            else
            {
                // server / client status message
                if (NetworkServer.active)
                {
                    string serverMsg = "Server: port=" + manager.networkPort;
                    if (manager.useWebSockets)
                    {
                        serverMsg += " (Using WebSockets)";
                    }

                    GUILayout.Label(serverMsg);
                }
                if (manager.IsClientConnected())
                {
                    GUILayout.Label("Client: address=" + manager.networkAddress + " port=" + manager.networkPort);
                }
            }

            // client ready
            if (manager.IsClientConnected() && !ClientScene.ready)
            {
                if (GUILayout.Button("Client Ready"))
                {
                    ClientScene.Ready(manager.client.connection);

                    if (ClientScene.localPlayers.Count == 0)
                    {
                        ClientScene.AddPlayer(0);
                    }
                }
            }

            // stop
            if (NetworkServer.active || manager.IsClientConnected())
            {
                if (GUILayout.Button("Stop"))
                {
                    manager.StopHost();
                }
            }

            GUILayout.EndArea();
        }
    }
}
#endif //ENABLE_UNET
