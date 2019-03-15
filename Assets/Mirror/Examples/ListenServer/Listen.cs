// add this component to the NetworkManager
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Listen
{
    public class ServerInfo
    {
        public string ip;
        public ushort port;
        public string title;
        public ushort players;
        public ushort capacity;
        public Ping ping;

        public ServerInfo(string ip, ushort port, string title, ushort players, ushort capacity)
        {
            this.ip = ip;
            this.port = port;
            this.title = title;
            this.players = players;
            this.capacity = capacity;
            ping = new Ping(ip);
        }
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

        [Header("GUI")]
        public Rect window = new Rect(10, 120, 560, 400);
        public int titleWidth = 220;
        public int playersWidth = 60;
        public int ipWidth = 80;
        public int portWidth = 50;
        public int pingWidth = 50;
        public int joinWidth = 50;
        Vector2 scrollPosition;

        List<ServerInfo> list = new List<ServerInfo>();

        void Start()
        {
            // add example entry
            // (can't do it in list constructor because Ping can't be created there yet)
            list.Add(new ServerInfo("127.0.0.1", 1337, "deathmatch", 1, 2));

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
            // shouldn't use game server, but still using it?
            else if (gameServerToListenConnection.Connected)
            {
                gameServerToListenConnection.Disconnect();
            }

            // receive client data from listen
            if (UseClientToListen())
            {
                // connected yet?
                if (clientToListenConnection.Connected)
                {
                }
                // otherwise try to connect
                // (we may have just joined the menu/disconnect from game server)
                else if (!clientToListenConnection.Connecting)
                {
                    Debug.Log("Establishing client to listen connection...");
                    clientToListenConnection.Connect(listenServerIp, clientToListenPort);
                }
            }
            // shouldn't use client, but still using it? (e.g. after joining)
            else if (clientToListenConnection.Connected)
            {
                clientToListenConnection.Disconnect();
            }
        }

        void OnGUI()
        {
            // show listen data on client
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


                // scroll area
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);

                // server table header
                GUILayout.BeginHorizontal("box");
                GUILayout.Box("<b>Server</b>", GUILayout.Width(titleWidth));
                GUILayout.Box("<b>Players</b>", GUILayout.Width(playersWidth));
                GUILayout.Box("<b>IP</b>", GUILayout.Width(ipWidth));
                GUILayout.Box("<b>Port</b>", GUILayout.Width(portWidth));
                GUILayout.Box("<b>Ping</b>", GUILayout.Width(pingWidth));
                GUILayout.Box("<b>Action</b>", GUILayout.Width(joinWidth));
                GUILayout.EndHorizontal();

                // entries
                foreach (ServerInfo server in list)
                {
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Box(server.title, GUILayout.Width(titleWidth));
                    GUILayout.Box(server.players + "/" + server.capacity, GUILayout.Width(playersWidth));
                    GUILayout.Box(server.ip, GUILayout.Width(ipWidth));
                    GUILayout.Box(server.port.ToString(), GUILayout.Width(portWidth));
                    GUILayout.Box(server.ping.isDone ? server.ping.time + "ms" : "...", GUILayout.Width(pingWidth));
                    GUI.enabled = server.players < server.capacity && !NetworkClient.active;
                    GUILayout.Button("Join", GUILayout.Width(joinWidth));
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();

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
