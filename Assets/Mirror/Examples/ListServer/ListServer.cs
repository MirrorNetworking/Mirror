// add this component to the NetworkManager
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.ListServer
{
    public class ServerStatus
    {
        public string ip;
        //public ushort port; // <- not all transports use a port. assume default port. feel free to also send a port if needed.
        public string title;
        public ushort players;
        public ushort capacity;

        public int lastLatency = -1;
#if !UNITY_WEBGL // Ping isn't known in WebGL builds
        public Ping ping;
#endif
        public ServerStatus(string ip, /*ushort port,*/ string title, ushort players, ushort capacity)
        {
            this.ip = ip;
            //this.port = port;
            this.title = title;
            this.players = players;
            this.capacity = capacity;
#if !UNITY_WEBGL // Ping isn't known in WebGL builds
            ping = new Ping(ip);
#endif
        }
    }

    [RequireComponent(typeof(NetworkManager))]
    public class ListServer : MonoBehaviour
    {
        [Header("List Server Connection")]
        public NetworkManager manager;
        public string listServerIp = "127.0.0.1";
        public ushort gameServerToListenPort = 8887;
        public ushort clientToListenPort = 8888;
        public string gameServerTitle = "Deathmatch";
        readonly Tcp.Client gameServerToListenConnection = new Tcp.Client();
        readonly Tcp.Client clientToListenConnection = new Tcp.Client();

        [Header("UI")]
        public GameObject mainPanel;
        public Transform content;
        public Text statusText;
        public UIServerStatusSlot slotPrefab;
        public Button serverAndPlayButton;
        public Button serverOnlyButton;
        public GameObject connectingPanel;
        public Text connectingText;
        public Button connectingCancelButton;
        int connectingDots = 0;

        // all the servers, stored as dict with unique ip key so we can
        // update them more easily
        // (use "ip:port" if port is needed)
        readonly Dictionary<string, ServerStatus> list = new Dictionary<string, ServerStatus>();

        void Start()
        {
            // examples
            //list["127.0.0.1"] = new ServerStatus("127.0.0.1", "Deathmatch", 3, 10);
            //list["192.168.0.1"] = new ServerStatus("192.168.0.1", "Free for all", 7, 10);
            //list["172.217.22.3"] = new ServerStatus("172.217.22.3", "5vs5", 10, 10);
            //list["172.217.16.142"] = new ServerStatus("172.217.16.142", "Hide & Seek Mod", 0, 10);


            clientToListenConnection.ReceivedData += ParseMessage;
            // Update once a second. no need to try to reconnect or read data
            // in each Update call
            // -> calling it more than 1/second would also cause significantly
            //    more broadcasts in the list server.
            InvokeRepeating(nameof(Tick), 0, 1);
        }

        bool IsConnecting() => manager.client.active && !manager.client.ready;
        bool FullyConnected() => manager.client.active && manager.client.ready;

        // should we use the client to listen connection?
        bool UseClientToListen()
        {
            return !NetworkManager.isHeadless && !manager.server.active && !FullyConnected();
        }

        // should we use the game server to listen connection?
        bool UseGameServerToListen()
        {
            return manager.server.active;
        }

        void Tick()
        {
            TickGameServer();
            TickClient();
        }

        // send server status to list server
        void SendStatus()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());

            // create message
            writer.Write((ushort)manager.server.connections.Count);
            writer.Write((ushort)manager.server.MaxConnections);
            byte[] titleBytes = Encoding.UTF8.GetBytes(gameServerTitle);
            writer.Write((ushort)titleBytes.Length);
            writer.Write(titleBytes);
            writer.Flush();

            // list server only allows up to 128 bytes per message
            if (writer.BaseStream.Position <= 128)
            {
                byte[] data = ((MemoryStream)writer.BaseStream).ToArray();
                // send it
                _ = gameServerToListenConnection.SendAsync(new System.ArraySegment<byte>(data));
            }
            else Debug.LogError("[List Server] List Server will reject messages longer than 128 bytes. Please use a shorter title.");
        }

        void TickGameServer()
        {
            // send server data to listen
            if (UseGameServerToListen())
            {
                // connected yet?
                if (gameServerToListenConnection.Connected)
                {
                    SendStatus();
                }
                // otherwise try to connect
                // (we may have just started the game)
                else if (!gameServerToListenConnection.Connecting)
                {
                    Debug.Log("[List Server] GameServer connecting......");
                    _ = gameServerToListenConnection.ConnectAsync(listServerIp, gameServerToListenPort);
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
            // note: we don't use ReadString here because the list server
            //       doesn't know C#'s '7-bit-length + utf8' encoding for strings
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            byte ipBytesLength = reader.ReadByte();
            byte[] ipBytes = reader.ReadBytes(ipBytesLength);
            string ip = new IPAddress(ipBytes).ToString();
            //ushort port = reader.ReadUInt16(); <- not all Transports use a port. assume default.
            ushort players = reader.ReadUInt16();
            ushort capacity = reader.ReadUInt16();
            ushort titleLength = reader.ReadUInt16();
            string title = Encoding.UTF8.GetString(reader.ReadBytes(titleLength));
            //Debug.Log("PARSED: ip=" + ip + /*" port=" + port +*/ " players=" + players + " capacity= " + capacity + " title=" + title);

            // build key
            string key = ip/* + ":" + port*/;

            // find existing or create new one
            if (list.TryGetValue(key, out ServerStatus server))
            {
                // refresh
                server.title = title;
                server.players = players;
                server.capacity = capacity;
            }
            else
            {
                // create
                server = new ServerStatus(ip, /*port,*/ title, players, capacity);
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
#if !UNITY_WEBGL // Ping isn't known in WebGL builds
                    // ping again if previous ping finished
                    foreach (ServerStatus server in list.Values)
                    {
                        if (server.ping.isDone)
                        {
                            server.lastLatency = server.ping.time;
                            server.ping = new Ping(server.ip);
                        }
                    }
#endif
                }
                // otherwise try to connect
                // (we may have just joined the menu/disconnect from game server)
                else if (!clientToListenConnection.Connecting)
                {
                    Debug.Log("[List Server] Client connecting...");
                    _ = clientToListenConnection.ConnectAsync(listServerIp, clientToListenPort);
                }
            }
            // shouldn't use client, but still using it? (e.g. after joining)
            else if (clientToListenConnection.Connected)
            {
                clientToListenConnection.Disconnect();
                list.Clear();
            }

            // refresh UI afterwards
            OnUI();
        }

        // instantiate/remove enough prefabs to match amount
        public static void BalancePrefabs(GameObject prefab, int amount, Transform parent)
        {
            // instantiate until amount
            for (int i = parent.childCount; i < amount; ++i)
            {
                Instantiate(prefab, parent, false);
            }

            // delete everything that's too much
            // (backwards loop because Destroy changes childCount)
            for (int i = parent.childCount - 1; i >= amount; --i)
                Destroy(parent.GetChild(i).gameObject);
        }

        void OnUI()
        {
            // only show while client not connected and server not started
            if (!manager.isNetworkActive || IsConnecting())
            {
                mainPanel.SetActive(true);

                // status text
                if (clientToListenConnection.Connecting)
                {
                    //statusText.color = Color.yellow;
                    statusText.text = "Connecting...";
                }
                else if (clientToListenConnection.Connected)
                {
                    //statusText.color = Color.green;
                    statusText.text = "Connected!";
                }
                else
                {
                    //statusText.color = Color.gray;
                    statusText.text = "Disconnected";
                }

                // instantiate/destroy enough slots
                BalancePrefabs(slotPrefab.gameObject, list.Count, content);

                // refresh all members
                for (int i = 0; i < list.Values.Count; ++i)
                {
                    UIServerStatusSlot slot = content.GetChild(i).GetComponent<UIServerStatusSlot>();
                    ServerStatus server = list.Values.ToList()[i];
                    slot.titleText.text = server.title;
                    slot.playersText.text = server.players + "/" + server.capacity;
                    slot.latencyText.text = server.lastLatency != -1 ? server.lastLatency.ToString() : "...";
                    slot.addressText.text = server.ip;
                    slot.joinButton.interactable = !IsConnecting();
                    slot.joinButton.gameObject.SetActive(server.players < server.capacity);
                    slot.joinButton.onClick.RemoveAllListeners();
                    slot.joinButton.onClick.AddListener(() =>
                    {
                        manager.StartClient(server.ip);
                    });
                }

                // server buttons
                serverAndPlayButton.interactable = !IsConnecting();
                serverAndPlayButton.onClick.RemoveAllListeners();
                serverAndPlayButton.onClick.AddListener(() =>
                {
                    manager.StartHost();
                });

                serverOnlyButton.interactable = !IsConnecting();
                serverOnlyButton.onClick.RemoveAllListeners();
                serverOnlyButton.onClick.AddListener(() =>
                {
                    manager.StartServer();
                });
            }
            else mainPanel.SetActive(false);

            // show connecting panel while connecting
            if (IsConnecting())
            {
                connectingPanel.SetActive(true);

                // . => .. => ... => ....
                connectingDots = ((connectingDots + 1) % 4);
                connectingText.text = "Connecting" + new string('.', connectingDots);

                // cancel button
                connectingCancelButton.onClick.RemoveAllListeners();
                connectingCancelButton.onClick.AddListener(manager.StopClient);
            }
            else connectingPanel.SetActive(false);
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
