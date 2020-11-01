//#if MIRROR <- commented out because MIRROR isn't defined on first import yet
using System;
using System.Linq;
using System.Net;
using Mirror;
using UnityEngine;

namespace kcp2k
{
    public class KcpTransport : Transport
    {
        // scheme used by this transport
        public const string Scheme = "kcp";

        // common
        [Header("Transport Configuration")]
        public ushort Port = 7777;
        [Tooltip("NoDelay is recommended to reduce latency. This also scales better without buffers getting full.")]
        public bool NoDelay = true;
        [Tooltip("KCP internal update interval. 100ms is KCP default, but a lower interval is recommended to minimize latency and to scale to more networked entities.")]
        public uint Interval = 10;
        [Header("Advanced")]
        [Tooltip("KCP fastresend parameter. Faster resend for the cost of higher bandwidth.")]
        public int FastResend = 0;
        [Tooltip("KCP congestion window can be disabled. This is necessary to Mirror 10k Benchmark. Disable this for high scale games if connections get chocked regularly.")]
        public bool CongestionWindow = true; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.
        [Tooltip("KCP window size can be modified to support higher loads. For example, Mirror Benchmark requires 128 for 4k monsters, 256 for 10k monsters (if CongestionWindow is disabled.)")]
        public uint SendWindowSize = 128; //Kcp.WND_SND; 32 by default. 128 is better for 4k Benchmark etc.
        [Tooltip("KCP window size can be modified to support higher loads. For example, Mirror Benchmark requires 128 for 4k monsters, 256 for 10k monsters (if CongestionWindow is disabled.)")]
        public uint ReceiveWindowSize = Kcp.WND_RCV;

        // server & client
        KcpServer server;
        KcpClient client;

        // debugging
        [Header("Debug")]
        public bool debugGUI;

        void Awake()
        {
            // TODO simplify after converting Mirror Transport events to Action
            client = new KcpClient(
                () => OnClientConnected.Invoke(),
                (message) => OnClientDataReceived.Invoke(message, Channels.DefaultReliable),
                () => OnClientDisconnected.Invoke()
            );
            // TODO simplify after converting Mirror Transport events to Action
            server = new KcpServer(
                (connectionId) => OnServerConnected.Invoke(connectionId),
                (connectionId, message) => OnServerDataReceived.Invoke(connectionId, message, Channels.DefaultReliable),
                (connectionId) => OnServerDisconnected.Invoke(connectionId),
                NoDelay,
                Interval,
                FastResend,
                CongestionWindow,
                SendWindowSize,
                ReceiveWindowSize
            );
            Debug.Log("KcpTransport initialized!");
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // client
        public override bool ClientConnected() => client.connected;
        public override void ClientConnect(string address)
        {
            client.Connect(address, Port, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize);
        }
        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            client.Send(segment);
        }
        public override void ClientDisconnect() => client.Disconnect();

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled)
                return;

            server.Tick();
            client.Tick();
        }

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = Port;
            return builder.Uri;
        }
        public override bool ServerActive() => server.IsActive();
        public override void ServerStart() => server.Start(Port);
        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            server.Send(connectionId, segment);
        }
        public override bool ServerDisconnect(int connectionId)
        {
            server.Disconnect(connectionId);
            return true;
        }
        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);
        public override void ServerStop() => server.Stop();

        // common
        public override void Shutdown() {}

        // MTU
        public override int GetMaxPacketSize(int channelId = Channels.DefaultReliable) => Kcp.MTU_DEF;

        public override string ToString()
        {
            return "KCP";
        }

        int GetTotalSendQueue() =>
            server.connections.Values.Sum(conn => conn.SendQueueCount);
        int GetTotalReceiveQueue() =>
            server.connections.Values.Sum(conn => conn.ReceiveQueueCount);
        int GetTotalSendBuffer() =>
            server.connections.Values.Sum(conn => conn.SendBufferCount);
        int GetTotalReceiveBuffer() =>
            server.connections.Values.Sum(conn => conn.ReceiveBufferCount);

        void OnGUI()
        {
            if (!debugGUI) return;

            GUILayout.BeginArea(new Rect(5, 100, 300, 300));

            if (ServerActive())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("SERVER");
                GUILayout.Label("  connections: " + server.connections.Count);
                GUILayout.Label("  SendQueue: " + GetTotalSendQueue());
                GUILayout.Label("  ReceiveQueue: " + GetTotalReceiveQueue());
                GUILayout.Label("  SendBuffer: " + GetTotalSendBuffer());
                GUILayout.Label("  ReceiveBuffer: " + GetTotalReceiveBuffer());
                GUILayout.EndVertical();
            }

            if (ClientConnected())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("CLIENT");
                GUILayout.Label("  SendQueue: " + client.connection.SendQueueCount);
                GUILayout.Label("  ReceiveQueue: " + client.connection.ReceiveQueueCount);
                GUILayout.Label("  SendBuffer: " + client.connection.SendBufferCount);
                GUILayout.Label("  ReceiveBuffer: " + client.connection.ReceiveBufferCount);
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }
    }
}
//#endif MIRROR <- commented out because MIRROR isn't defined on first import yet
