// vis2k: GUILayout instead of spacey += ...; removed Update hotkeys to avoid
// confusion if someone accidentally presses one.
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mirror
{
    [AddComponentMenu("Network/NetworkManagerHUD")]
    [RequireComponent(typeof(NetworkManager))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class NetworkManagerHUD : MonoBehaviour
    {
        NetworkManager manager;
        public bool showGUI = true;
        public int offsetX;
        public int offsetY;

        [Tooltip("Start the server automatically when running in headless mode")]
        public bool startOnHeadless = true;

        void Awake()
        {
            manager = GetComponent<NetworkManager>();

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null && startOnHeadless)
            {
                // headless mode. Just start the server
                manager.StartServer();
            }
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

                    if (ClientScene.localPlayer == null)
                    {
                        ClientScene.AddPlayer();
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
