// add this component to the NetworkManager
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mirror.Examples.Listen
{
    public class ServerInfo
    {
        public string ip;
        //public ushort port; // <- not all transports use a port. assume default port. feel free to also send a port if needed.
        public string title;
        public ushort players;
        public ushort capacity;

        public int lastLatency = -1;
        public Ping ping;

        public ServerInfo(string ip, /*ushort port,*/ string title, ushort players, ushort capacity)
        {
            this.ip = ip;
            //this.port = port;
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
        public string gameServerTitle = "Deathmatch";

        Telepathy.Client gameServerToListenConnection = new Telepathy.Client();
        Telepathy.Client clientToListenConnection = new Telepathy.Client();

        [Header("GUI")]
        public Rect window = new Rect(10, 120, 570, 400);
        public int titleWidth = 220;
        public int playersWidth = 60;
        public int addressWidth = 130;
        public int latencyWidth = 60;
        public int joinWidth = 50;
        Vector2 scrollPosition;

        // all the servers, stored as dict with unique ip key so we can
        // update them more easily
        // (use "ip:port" if port is needed)
        Dictionary<string, ServerInfo> list = new Dictionary<string, ServerInfo>();

        void Start()
        {
            // add example entry
            // (can't do it in list constructor because Ping can't be created there yet)
            //list.Add(new ServerInfo("127.0.0.1", 1337, "Deathmatch", 1, 2));

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
            TickGameServer();
            TickClient();
        }

        void TickGameServer()
        {
            // send server data to listen
            if (UseGameServerToListen())
            {
                // connected yet?
                if (gameServerToListenConnection.Connected)
                {

                }
                // otherwise try to connect
                // (we may have just started the game)
                else if (!gameServerToListenConnection.Connecting)
                {
                    Debug.Log("Establishing game server to listen connection...");
                    gameServerToListenConnection.Connect(listenServerIp, gameServerToListenPort);
                }
            }
            // shouldn't use game server, but still using it?
            else if (gameServerToListenConnection.Connected)
            {
                gameServerToListenConnection.Disconnect();
            }
        }

        void ParseMessage(byte[] bytes)
        {
            // use binary reader because our NetworkReader uses custom string reading with bools
            // => we don't use ReadString here because the listen server doesn't
            //    know C#'s '7-bit-length + utf8' encoding for strings
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            ushort ipLength = reader.ReadUInt16();
            string ip = new string(reader.ReadChars(ipLength));
            //ushort port = reader.ReadUInt16(); <- not all Transports use a port. assume default.
            ushort titleLength = reader.ReadUInt16();
            string title = new string(reader.ReadChars(titleLength));
            ushort players = reader.ReadUInt16();
            ushort capacity = reader.ReadUInt16();
            Debug.Log("PARSED: ip=" + ip + /*" port=" + port +*/ " title=" + title + " players=" + players + " capacity= " + capacity);

            // build key
            string key = ip/* + ":" + port*/;

            // find existing or create new one
            ServerInfo server;
            if (list.TryGetValue(key, out server))
            {
                // refresh
                server.title = title;
                server.players = players;
                server.capacity = capacity;
            }
            else
            {
                // create
                server = new ServerInfo(ip, /*port,*/ title, players, capacity);
            }

            // save
            list[key] = server;
        }

        void TickClient()
        {
            // receive client data from listen
            if (UseClientToListen())
            {
                // connected yet?
                if (clientToListenConnection.Connected)
                {
                    // receive latest game server info
                    while (clientToListenConnection.GetNextMessage(out Telepathy.Message message))
                    {
                        // data message?
                        if (message.eventType == Telepathy.EventType.Data)
                            ParseMessage(message.data);
                    }

                    // ping again if previous ping finished
                    foreach (ServerInfo server in list.Values)
                    {
                        if (server.ping.isDone)
                        {
                            server.lastLatency = server.ping.time;
                            server.ping = new Ping(server.ip);
                        }
                    }
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
                list.Clear();
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
                GUILayout.Box("<b>Latency</b>", GUILayout.Width(latencyWidth));
                GUILayout.Box("<b>Address</b>", GUILayout.Width(addressWidth));
                GUILayout.Box("<b>Action</b>", GUILayout.Width(joinWidth));
                GUILayout.EndHorizontal();

                // entries
                foreach (ServerInfo server in list.Values)
                {
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Box(server.title, GUILayout.Width(titleWidth));
                    GUILayout.Box(server.players + "/" + server.capacity, GUILayout.Width(playersWidth));
                    GUILayout.Box(server.lastLatency != -1 ? server.lastLatency.ToString() : "...", GUILayout.Width(latencyWidth));
                    GUILayout.Box(server.ip);
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
