//#if MIRROR <- commented out because MIRROR isn't defined on first import yet
using System;
using System.Linq;
using System.Net;
using UnityEngine;
using Mirror;
using Unity.Collections;

namespace kcp2k
{
    [HelpURL("https://mirror-networking.gitbook.io/docs/transports/kcp-transport")]
    [DisallowMultipleComponent]
    public class KcpTransport : Transport
    {
        // scheme used by this transport
        public const string Scheme = "kcp";

        // common
        [Header("Transport Configuration")]
        public ushort Port = 7777;
        [Tooltip("DualMode listens to IPv6 and IPv4 simultaneously. Disable if the platform only supports IPv4.")]
        public bool DualMode = true;
        [Tooltip("NoDelay is recommended to reduce latency. This also scales better without buffers getting full.")]
        public bool NoDelay = true;
        [Tooltip("KCP internal update interval. 100ms is KCP default, but a lower interval is recommended to minimize latency and to scale to more networked entities.")]
        public uint Interval = 10;
        [Tooltip("KCP timeout in milliseconds. Note that KCP sends a ping automatically.")]
        public int Timeout = 10000;

        [Header("Advanced")]
        [Tooltip("KCP fastresend parameter. Faster resend for the cost of higher bandwidth. 0 in normal mode, 2 in turbo mode.")]
        public int FastResend = 2;
        [Tooltip("KCP congestion window. Enabled in normal mode, disabled in turbo mode. Disable this for high scale games if connections get choked regularly.")]
        public bool CongestionWindow = false; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.
        [Tooltip("KCP window size can be modified to support higher loads.")]
        public uint SendWindowSize = 4096; //Kcp.WND_SND; 32 by default. Mirror sends a lot, so we need a lot more.
        [Tooltip("KCP window size can be modified to support higher loads. This also increases max message size.")]
        public uint ReceiveWindowSize = 4096; //Kcp.WND_RCV; 128 by default. Mirror sends a lot, so we need a lot more.
        [Tooltip("KCP will try to retransmit lost messages up to MaxRetransmit (aka dead_link) before disconnecting.")]
        public uint MaxRetransmit = Kcp.DEADLINK * 2; // default prematurely disconnects a lot of people (#3022). use 2x.
        [Tooltip("Enable to use where-allocation NonAlloc KcpServer/Client/Connection versions. Highly recommended on all Unity platforms.")]
        public bool NonAlloc = true;
        [Tooltip("Enable to automatically set client & server send/recv buffers to OS limit. Avoids issues with too small buffers under heavy load, potentially dropping connections. Increase the OS limit if this is still too small.")]
        public bool MaximizeSendReceiveBuffersToOSLimit = true;

        [Header("Calculated Max (based on Receive Window Size)")]
        [Tooltip("KCP reliable max message size shown for convenience. Can be changed via ReceiveWindowSize.")]
        [ReadOnly] public int ReliableMaxMessageSize = 0; // readonly, displayed from OnValidate
        [Tooltip("KCP unreliable channel max message size for convenience. Not changeable.")]
        [ReadOnly] public int UnreliableMaxMessageSize = 0; // readonly, displayed from OnValidate

        // server & client (where-allocation NonAlloc versions)
        KcpServer server;
        KcpClient client;

        // debugging
        [Header("Debug")]
        public bool debugLog;
        // show statistics in OnGUI
        public bool statisticsGUI;
        // log statistics for headless servers that can't show them in GUI
        public bool statisticsLog;

        // translate Kcp <-> Mirror channels
        static int FromKcpChannel(KcpChannel channel) =>
            channel == KcpChannel.Reliable ? Channels.Reliable : Channels.Unreliable;

        static KcpChannel ToKcpChannel(int channel) =>
            channel == Channels.Reliable ? KcpChannel.Reliable : KcpChannel.Unreliable;

        TransportError ToTransportError(ErrorCode error)
        {
            switch(error)
            {
                case ErrorCode.DnsResolve: return TransportError.DnsResolve;
                case ErrorCode.Timeout: return TransportError.Timeout;
                case ErrorCode.Congestion: return TransportError.Congestion;
                case ErrorCode.InvalidReceive: return TransportError.InvalidReceive;
                case ErrorCode.InvalidSend: return TransportError.InvalidSend;
                case ErrorCode.ConnectionClosed: return TransportError.ConnectionClosed;
                case ErrorCode.Unexpected: return TransportError.Unexpected;
                default: throw new InvalidCastException($"KCP: missing error translation for {error}");
            }
        }

        void Awake()
        {
            // logging
            //   Log.Info should use Debug.Log if enabled, or nothing otherwise
            //   (don't want to spam the console on headless servers)
            if (debugLog)
                Log.Info = Debug.Log;
            else
                Log.Info = _ => {};
            Log.Warning = Debug.LogWarning;
            Log.Error = Debug.LogError;

#if ENABLE_IL2CPP
            // NonAlloc doesn't work with IL2CPP builds
            NonAlloc = false;
#endif

            // client
            client = NonAlloc
                ? new KcpClientNonAlloc(
                      () => OnClientConnected.Invoke(),
                      (message, channel) => OnClientDataReceived.Invoke(message, FromKcpChannel(channel)),
                      () => OnClientDisconnected.Invoke(),
                      (error, reason) => OnClientError.Invoke(ToTransportError(error), reason))
                : new KcpClient(
                      () => OnClientConnected.Invoke(),
                      (message, channel) => OnClientDataReceived.Invoke(message, FromKcpChannel(channel)),
                      () => OnClientDisconnected.Invoke(),
                      (error, reason) => OnClientError.Invoke(ToTransportError(error), reason));

            // server
            server = NonAlloc
                ? new KcpServerNonAlloc(
                      (connectionId) => OnServerConnected.Invoke(connectionId),
                      (connectionId, message, channel) => OnServerDataReceived.Invoke(connectionId, message, FromKcpChannel(channel)),
                      (connectionId) => OnServerDisconnected.Invoke(connectionId),
                      (connectionId, error, reason) => OnServerError.Invoke(connectionId, ToTransportError(error), reason),
                      DualMode,
                      NoDelay,
                      Interval,
                      FastResend,
                      CongestionWindow,
                      SendWindowSize,
                      ReceiveWindowSize,
                      Timeout,
                      MaxRetransmit,
                      MaximizeSendReceiveBuffersToOSLimit)
                : new KcpServer(
                      (connectionId) => OnServerConnected.Invoke(connectionId),
                      (connectionId, message, channel) => OnServerDataReceived.Invoke(connectionId, message, FromKcpChannel(channel)),
                      (connectionId) => OnServerDisconnected.Invoke(connectionId),
                      (connectionId, error, reason) => OnServerError.Invoke(connectionId, ToTransportError(error), reason),
                      DualMode,
                      NoDelay,
                      Interval,
                      FastResend,
                      CongestionWindow,
                      SendWindowSize,
                      ReceiveWindowSize,
                      Timeout,
                      MaxRetransmit,
                      MaximizeSendReceiveBuffersToOSLimit);

            if (statisticsLog)
                InvokeRepeating(nameof(OnLogStatistics), 1, 1);

            Debug.Log("KcpTransport initialized!");
        }

        void OnValidate()
        {
            // show max message sizes in inspector for convenience
            ReliableMaxMessageSize = KcpConnection.ReliableMaxMessageSize(ReceiveWindowSize);
            UnreliableMaxMessageSize = KcpConnection.UnreliableMaxMessageSize;
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // client
        public override bool ClientConnected() => client.connected;
        public override void ClientConnect(string address)
        {
            client.Connect(address, Port, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout, MaxRetransmit, MaximizeSendReceiveBuffersToOSLimit);
        }
        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != Scheme)
                throw new ArgumentException($"Invalid url {uri}, use {Scheme}://host:port instead", nameof(uri));

            int serverPort = uri.IsDefaultPort ? Port : uri.Port;
            client.Connect(uri.Host, (ushort)serverPort, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout, MaxRetransmit, MaximizeSendReceiveBuffersToOSLimit);
        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            client.Send(segment, ToKcpChannel(channelId));

            // call event. might be null if no statistics are listening etc.
            OnClientDataSent?.Invoke(segment, channelId);
        }
        public override void ClientDisconnect() => client.Disconnect();
        // process incoming in early update
        public override void ClientEarlyUpdate()
        {
            // only process messages while transport is enabled.
            // scene change messsages disable it to stop processing.
            // (see also: https://github.com/vis2k/Mirror/pull/379)
            if (enabled) client.TickIncoming();
        }
        // process outgoing in late update
        public override void ClientLateUpdate() => client.TickOutgoing();

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
        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            server.Send(connectionId, segment, ToKcpChannel(channelId));

            // call event. might be null if no statistics are listening etc.
            OnServerDataSent?.Invoke(connectionId, segment, channelId);
        }
        public override void ServerDisconnect(int connectionId) =>  server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId)
        {
            IPEndPoint endPoint = server.GetClientEndPoint(connectionId);
            return endPoint != null ? endPoint.Address.ToString() : "";
        }
        public override void ServerStop() => server.Stop();
        public override void ServerEarlyUpdate()
        {
            // only process messages while transport is enabled.
            // scene change messsages disable it to stop processing.
            // (see also: https://github.com/vis2k/Mirror/pull/379)
            if (enabled) server.TickIncoming();
        }
        // process outgoing in late update
        public override void ServerLateUpdate() => server.TickOutgoing();

        // common
        public override void Shutdown() {}

        // max message size
        public override int GetMaxPacketSize(int channelId = Channels.Reliable)
        {
            // switch to kcp channel.
            // unreliable or reliable.
            // default to reliable just to be sure.
            switch (channelId)
            {
                case Channels.Unreliable:
                    return KcpConnection.UnreliableMaxMessageSize;
                default:
                    return KcpConnection.ReliableMaxMessageSize(ReceiveWindowSize);
            }
        }

        // kcp reliable channel max packet size is MTU * WND_RCV
        // this allows 144kb messages. but due to head of line blocking, all
        // other messages would have to wait until the maxed size one is
        // delivered. batching 144kb messages each time would be EXTREMELY slow
        // and fill the send queue nearly immediately when using it over the
        // network.
        // => instead we always use MTU sized batches.
        // => people can still send maxed size if needed.
        public override int GetBatchThreshold(int channelId) =>
            KcpConnection.UnreliableMaxMessageSize;

        // server statistics
        // LONG to avoid int overflows with connections.Sum.
        // see also: https://github.com/vis2k/Mirror/pull/2777
        public long GetAverageMaxSendRate() =>
            server.connections.Count > 0
                ? server.connections.Values.Sum(conn => (long)conn.MaxSendRate) / server.connections.Count
                : 0;
        public long GetAverageMaxReceiveRate() =>
            server.connections.Count > 0
                ? server.connections.Values.Sum(conn => (long)conn.MaxReceiveRate) / server.connections.Count
                : 0;
        long GetTotalSendQueue() =>
            server.connections.Values.Sum(conn => conn.SendQueueCount);
        long GetTotalReceiveQueue() =>
            server.connections.Values.Sum(conn => conn.ReceiveQueueCount);
        long GetTotalSendBuffer() =>
            server.connections.Values.Sum(conn => conn.SendBufferCount);
        long GetTotalReceiveBuffer() =>
            server.connections.Values.Sum(conn => conn.ReceiveBufferCount);

        // PrettyBytes function from DOTSNET
        // pretty prints bytes as KB/MB/GB/etc.
        // long to support > 2GB
        // divides by floats to return "2.5MB" etc.
        public static string PrettyBytes(long bytes)
        {
            // bytes
            if (bytes < 1024)
                return $"{bytes} B";
            // kilobytes
            else if (bytes < 1024L * 1024L)
                return $"{(bytes / 1024f):F2} KB";
            // megabytes
            else if (bytes < 1024 * 1024L * 1024L)
                return $"{(bytes / (1024f * 1024f)):F2} MB";
            // gigabytes
            return $"{(bytes / (1024f * 1024f * 1024f)):F2} GB";
        }

// OnGUI allocates even if it does nothing. avoid in release.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnGUI()
        {
            if (!statisticsGUI) return;

            GUILayout.BeginArea(new Rect(5, 110, 300, 300));

            if (ServerActive())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("SERVER");
                GUILayout.Label($"  connections: {server.connections.Count}");
                GUILayout.Label($"  MaxSendRate (avg): {PrettyBytes(GetAverageMaxSendRate())}/s");
                GUILayout.Label($"  MaxRecvRate (avg): {PrettyBytes(GetAverageMaxReceiveRate())}/s");
                GUILayout.Label($"  SendQueue: {GetTotalSendQueue()}");
                GUILayout.Label($"  ReceiveQueue: {GetTotalReceiveQueue()}");
                GUILayout.Label($"  SendBuffer: {GetTotalSendBuffer()}");
                GUILayout.Label($"  ReceiveBuffer: {GetTotalReceiveBuffer()}");
                GUILayout.EndVertical();
            }

            if (ClientConnected())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("CLIENT");
                GUILayout.Label($"  MaxSendRate: {PrettyBytes(client.connection.MaxSendRate)}/s");
                GUILayout.Label($"  MaxRecvRate: {PrettyBytes(client.connection.MaxReceiveRate)}/s");
                GUILayout.Label($"  SendQueue: {client.connection.SendQueueCount}");
                GUILayout.Label($"  ReceiveQueue: {client.connection.ReceiveQueueCount}");
                GUILayout.Label($"  SendBuffer: {client.connection.SendBufferCount}");
                GUILayout.Label($"  ReceiveBuffer: {client.connection.ReceiveBufferCount}");
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }
#endif

        void OnLogStatistics()
        {
            if (ServerActive())
            {
                string log = "kcp SERVER @ time: " + NetworkTime.localTime + "\n";
                log += $"  connections: {server.connections.Count}\n";
                log += $"  MaxSendRate (avg): {PrettyBytes(GetAverageMaxSendRate())}/s\n";
                log += $"  MaxRecvRate (avg): {PrettyBytes(GetAverageMaxReceiveRate())}/s\n";
                log += $"  SendQueue: {GetTotalSendQueue()}\n";
                log += $"  ReceiveQueue: {GetTotalReceiveQueue()}\n";
                log += $"  SendBuffer: {GetTotalSendBuffer()}\n";
                log += $"  ReceiveBuffer: {GetTotalReceiveBuffer()}\n\n";
                Debug.Log(log);
            }

            if (ClientConnected())
            {
                string log = "kcp CLIENT @ time: " + NetworkTime.localTime + "\n";
                log += $"  MaxSendRate: {PrettyBytes(client.connection.MaxSendRate)}/s\n";
                log += $"  MaxRecvRate: {PrettyBytes(client.connection.MaxReceiveRate)}/s\n";
                log += $"  SendQueue: {client.connection.SendQueueCount}\n";
                log += $"  ReceiveQueue: {client.connection.ReceiveQueueCount}\n";
                log += $"  SendBuffer: {client.connection.SendBufferCount}\n";
                log += $"  ReceiveBuffer: {client.connection.ReceiveBufferCount}\n\n";
                Debug.Log(log);
            }
        }

        public override string ToString() => "KCP";
    }
}
//#endif MIRROR <- commented out because MIRROR isn't defined on first import yet
