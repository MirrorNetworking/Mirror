// add this component to the NetworkManager
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Listen
{
    public struct ServerInfo
    {
        public string ip;
        public ushort port;
        public string title;
        public ushort players;
        public ushort capacity;
    }

    [RequireComponent(typeof(NetworkManager))]
    public class Listen : MonoBehaviour
    {
        [Header("Listen Server Connection")]
        public string listenServerIp = "127.0.0.1";
        public ushort gameServerToListenPort = 8887;
        public ushort clientToListenPort = 8888;

        Telepathy.Client gameServerToListenConnection = new Telepathy.Client();
        Telepathy.Client clientToListenConnection = new Telepathy.Client();

        // game server to listen server connection

        // client to listen server connection
        // (only active while receiving game server lists)

        [Header("GUI")]
        public Rect window = new Rect(5, 150, 400, 400);

        List<ServerInfo> list = new List<ServerInfo>();

        void Start()
        {
            // Update once a second. no need to try to reconnect or read data
            // in each Update call
            InvokeRepeating(nameof(Tick), 0, 1);
        }

        // should we use the client to listen connection?
        bool UseClientToListen()
        {
            return !NetworkManager.IsHeadless() && !NetworkServer.active && !NetworkClient.active;
        }

        // should we use the game server to listen connection?
        bool UseGameServerToListen()
        {
            return NetworkServer.active;
        }

        void Tick()
        {
            // send server data to listen
            if (UseGameServerToListen())
            {
                // TODO
            }

            // receive client data from listen
            if (UseClientToListen())
            {
                // connected yet?
                if (clientToListenConnection.Connected)
                {
                    Debug.Log("Client to listen connection active...");
                }
                // otherwise try to connect
                // (we may have just joined the menu/disconnect from game server)
                else if (!clientToListenConnection.Connecting)
                {
                    Debug.Log("Establishing client to listen connection...");
                    clientToListenConnection.Connect(listenServerIp, clientToListenPort);
                }
            }
        }

        void OnGUI()
        {
            // TODO show listen data on client
            if (UseClientToListen())
            {
                GUILayout.BeginArea(window);
                GUILayout.BeginVertical("box");

                // header
                GUILayout.Label("Gameserver List:");

                if (!clientToListenConnection.Connected)
                    GUILayout.Label("Connecting...");

                if (clientToListenConnection.Connected && list.Count == 0)
                    GUILayout.Label("Scanning...");

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        // disconnect everything when pressing Stop in the Editor
        void OnApplicationQuit()
        {
            if (gameServerToListenConnection.Connected)
                gameServerToListenConnection.Disconnect();
            if (clientToListenConnection.Connected)
                clientToListenConnection.Disconnect();
        }
    }
}
