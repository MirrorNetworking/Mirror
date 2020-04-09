using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Mirror.Examples.ListServer
{
    public class ServerStatus
    {
        public string ip;
        // not all transports use a port. assume default port. feel free to also send a port if needed.
        public string title;
        public ushort players;
        public ushort capacity;

        public int lastLatency = -1;
#if !UNITY_WEBGL
        // Ping isn't known in WebGL builds
        public Ping ping;
#endif
        public ServerStatus(string ip, /*ushort port,*/ string title, ushort players, ushort capacity)
        {
            this.ip = ip;
            this.title = title;
            this.players = players;
            this.capacity = capacity;
#if !UNITY_WEBGL
            // Ping isn't known in WebGL builds
            ping = new Ping(ip);
#endif
        }
    }

    public class ListServers : MonoBehaviour
    {
        [Header("List Server Connection")]
        public NetworkManager manager;
        public string listServerUrl = "tpc4://127.0.0.1:8888";
        IConnection clientToListenConnection;

        public AsyncTransport transport;

        private bool Connecting;
        private bool Connected;

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
        int connectingDots;

        // all the servers, stored as dict with unique ip key so we can
        // update them more easily
        // (use "ip:port" if port is needed)
        readonly Dictionary<string, ServerStatus> list = new Dictionary<string, ServerStatus>();

        public async Task Start()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            Connecting = true;

            try
            {
                clientToListenConnection = await transport.ConnectAsync(new Uri(listServerUrl));

                Connecting = false;
                Connected = true;

                InvokeRepeating(nameof(Tick), 1f, 1f);

                var buffer = new MemoryStream();

                while (await clientToListenConnection.ReceiveAsync(buffer))
                {
                    ParseMessage(buffer.ToArray());
                    buffer.SetLength(0);
                }
            }
            finally
            {
                Connecting = false;
                Connected = false;

                clientToListenConnection.Disconnect();
                clientToListenConnection = null;
            }
        }

        bool IsConnecting() => manager.client.Active && !manager.client.ready;
        bool FullyConnected() => manager.client.Active && manager.client.ready;

        // should we use the game server to listen connection?
        bool UseGameServerToListen()
        {
            return manager.server.Active;
        }

        void ParseMessage(byte[] bytes)
        {
            // note: we don't use ReadString here because the list server
            //       doesn't know C#'s '7-bit-length + utf8' encoding for strings
            var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            byte ipBytesLength = reader.ReadByte();
            byte[] ipBytes = reader.ReadBytes(ipBytesLength);
            string ip = new IPAddress(ipBytes).ToString();
            ushort players = reader.ReadUInt16();
            ushort capacity = reader.ReadUInt16();
            ushort titleLength = reader.ReadUInt16();
            string title = Encoding.UTF8.GetString(reader.ReadBytes(titleLength));

            // build key
            string key = ip;

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
                server = new ServerStatus(ip, title, players, capacity);
            }

            // save
            list[key] = server;
        }

        void Tick()
        {

#if !UNITY_WEBGL
            // Ping isn't known in WebGL builds

            // receive client data from listen
            // connected yet?
            if (clientToListenConnection != null)
            {
                // ping again if previous ping finished
                foreach (ServerStatus server in list.Values)
                {
                    if (server.ping.isDone)
                    {
                        server.lastLatency = server.ping.time;
                        server.ping = new Ping(server.ip);
                    }
                }
            }
#endif

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
            if (!manager.IsNetworkActive || IsConnecting())
            {
                mainPanel.SetActive(true);

                // status text
                if (Connecting)
                {
                    statusText.text = "Connecting...";
                }
                else if (Connected)
                {
                    statusText.text = "Connected!";
                }
                else
                {
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
            if (clientToListenConnection != null)
                clientToListenConnection.Disconnect();
        }
    }
}
