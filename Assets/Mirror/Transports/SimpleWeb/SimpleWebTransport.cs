using System;
using System.Net;
using System.Security.Authentication;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.SimpleWeb
{
    [DisallowMultipleComponent]
    public class SimpleWebTransport : Transport, PortTransport
    {
        public const string NormalScheme = "ws";
        public const string SecureScheme = "wss";

        [Tooltip("Port to use for server and client")]
        public ushort port = 7778;
        public ushort Port { get => port; set => port=value; }

        [Tooltip("Tells the client to use the default port. This is useful when connecting to reverse proxy rather than directly to websocket server")]
        public bool ClientUseDefaultPort;

        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker might send multiple fake packets with 2GB headers, causing the server to run out of memory after allocating multiple large packets.")]
        public int maxMessageSize = 16 * 1024;

        [Tooltip("Max size for http header send as handshake for websockets")]
        public int handshakeMaxSize = 3000;

        [Tooltip("disables nagle algorithm. lowers CPU% and latency but increases bandwidth")]
        public bool noDelay = true;

        [Tooltip("Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds)")]
        public int sendTimeout = 5000;

        [Tooltip("How long without a message before disconnecting (in milliseconds)")]
        public int receiveTimeout = 20000;

        [Tooltip("Caps the number of messages the server will process per tick. Allows LateUpdate to finish to let the reset of unity continue in case more messages arrive before they are processed")]
        public int serverMaxMessagesPerTick = 10000;

        [Tooltip("Caps the number of messages the client will process per tick. Allows LateUpdate to finish to let the reset of unity continue in case more messages arrive before they are processed")]
        public int clientMaxMessagesPerTick = 1000;

        [Header("Server settings")]

        [Tooltip("Groups messages in queue before calling Stream.Send")]
        public bool batchSend = true;

        [Tooltip("Waits for 1ms before grouping and sending messages.\n" +
            "This gives time for mirror to finish adding message to queue so that less groups need to be made.\n" +
            "If WaitBeforeSend is true then BatchSend Will also be set to true")]
        public bool waitBeforeSend = true;

        [Header("Ssl Settings")]
        [Tooltip("Sets connect scheme to wss. Useful when client needs to connect using wss when TLS is outside of transport.\nNOTE: if sslEnabled is true clientUseWss is also true")]
        public bool clientUseWss;

        [Tooltip("Requires wss connections on server, only to be used with SSL cert.json, never with reverse proxy.\nNOTE: if sslEnabled is true clientUseWss is also true")]
        public bool sslEnabled;

        [Tooltip("Path to json file that contains path to cert and its password\nUse Json file so that cert password is not included in client builds\nSee Assets/Mirror/Transports/.cert.example.Json")]
        public string sslCertJson = "./cert.json";

        [Tooltip("Protocols that SSL certificate is created to support.")]
        public SslProtocols sslProtocols = SslProtocols.Tls12;

        [Header("Debug")]
        [Tooltip("Log functions uses ConditionalAttribute which will effect which log methods are allowed. DEBUG allows warn/error, SIMPLEWEB_LOG_ENABLED allows all")]
        [FormerlySerializedAs("logLevels")]
        [SerializeField] Log.Levels _logLevels = Log.Levels.info;

        /// <summary>
        /// <para>Gets _logLevels field</para>
        /// <para>Sets _logLevels and Log.level fields</para>
        /// </summary>
        public Log.Levels LogLevels
        {
            get => _logLevels;
            set
            {
                _logLevels = value;
                Log.level = _logLevels;
            }
        }

        SimpleWebClient client;
        SimpleWebServer server;

        TcpConfig TcpConfig => new TcpConfig(noDelay, sendTimeout, receiveTimeout);

        void Awake()
        {
            Log.level = _logLevels;
        }

        void OnValidate()
        {
            Log.level = _logLevels;
        }

        public override bool Available() => true;

        public override int GetMaxPacketSize(int channelId = 0) => maxMessageSize;

        public override void Shutdown()
        {
            client?.Disconnect();
            client = null;
            server?.Stop();
            server = null;
        }

        #region Client

        string GetClientScheme() => (sslEnabled || clientUseWss) ? SecureScheme : NormalScheme;

        public override bool ClientConnected()
        {
            // not null and not NotConnected (we want to return true if connecting or disconnecting)
            return client != null && client.ConnectionState != ClientState.NotConnected;
        }

        public override void ClientConnect(string hostname)
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = GetClientScheme(),
                Host = hostname,
            };
            // https://github.com/MirrorNetworking/Mirror/pull/3477
            if (!ClientUseDefaultPort)
                builder.Port = Port;

            ClientConnect(builder.Uri);
        }

        public override void ClientConnect(Uri uri)
        {
            // connecting or connected
            if (ClientConnected())
            {
                Debug.LogError("[SimpleWebTransport] Already Connected");
                return;
            }

            client = SimpleWebClient.Create(maxMessageSize, clientMaxMessagesPerTick, TcpConfig);
            if (client == null)
                return;

            client.onConnect += OnClientConnected.Invoke;

            client.onDisconnect += () =>
            {
                OnClientDisconnected.Invoke();
                // clear client here after disconnect event has been sent
                // there should be no more messages after disconnect
                client = null;
            };

            client.onData += (ArraySegment<byte> data) => OnClientDataReceived.Invoke(data, Channels.Reliable);

            client.onError += (Exception e) =>
            {
                OnClientError.Invoke(TransportError.Unexpected, e.ToString());
                ClientDisconnect();
            };

            client.Connect(uri);
        }

        public override void ClientDisconnect()
        {
            // don't set client null here of messages wont be processed
            client?.Disconnect();
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            if (!ClientConnected())
            {
                Debug.LogError("[SimpleWebTransport] Not Connected");
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SimpleWebTransport] Message greater than max size");
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SimpleWebTransport] Message count was zero");
                return;
            }

            client.Send(segment);

            // call event. might be null if no statistics are listening etc.
            OnClientDataSent?.Invoke(segment, Channels.Reliable);
        }

        // messages should always be processed in early update
        public override void ClientEarlyUpdate()
        {
            client?.ProcessMessageQueue(this);
        }

        #endregion

        #region Server

        string GetServerScheme() => sslEnabled ? SecureScheme : NormalScheme;

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = GetServerScheme(),
                Host = Dns.GetHostName(),
                Port = port
            };
            return builder.Uri;
        }

        public override bool ServerActive()
        {
            return server != null && server.Active;
        }

        public override void ServerStart()
        {
            if (ServerActive())
                Debug.LogError("[SimpleWebTransport] Server Already Started");

            SslConfig config = SslConfigLoader.Load(sslEnabled, sslCertJson, sslProtocols);
            server = new SimpleWebServer(serverMaxMessagesPerTick, TcpConfig, maxMessageSize, handshakeMaxSize, config);

            server.onConnect += OnServerConnected.Invoke;
            server.onDisconnect += OnServerDisconnected.Invoke;
            server.onData += (int connId, ArraySegment<byte> data) => OnServerDataReceived.Invoke(connId, data, Channels.Reliable);
            server.onError += (connId, exception) => OnServerError(connId, TransportError.Unexpected, exception.ToString());

            SendLoopConfig.batchSend = batchSend || waitBeforeSend;
            SendLoopConfig.sleepBeforeSend = waitBeforeSend;

            server.Start(port);
        }

        public override void ServerStop()
        {
            if (!ServerActive())
                Debug.LogError("[SimpleWebTransport] Server Not Active");

            server.Stop();
            server = null;
        }

        public override void ServerDisconnect(int connectionId)
        {
            if (!ServerActive())
                Debug.LogError("[SimpleWebTransport] Server Not Active");

            server.KickClient(connectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (!ServerActive())
            {
                Log.Error("[SimpleWebTransport] Server Not Active", false);
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SimpleWebTransport] Message greater than max size", false);
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SimpleWebTransport] Message count was zero", false);
                return;
            }

            server.SendOne(connectionId, segment);

            // call event. might be null if no statistics are listening etc.
            OnServerDataSent?.Invoke(connectionId, segment, Channels.Reliable);
        }

        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);

        // messages should always be processed in early update
        public override void ServerEarlyUpdate()
        {
            server?.ProcessMessageQueue(this);
        }

        #endregion
    }
}
