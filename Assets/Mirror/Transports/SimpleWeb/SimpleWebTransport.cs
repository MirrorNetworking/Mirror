using System;
using System.Net;
using System.Security.Authentication;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.SimpleWeb
{
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/websockets-transport")]
    public class SimpleWebTransport : Transport, PortTransport
    {
        public const string NormalScheme = "ws";
        public const string SecureScheme = "wss";

        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker might send multiple fake packets with 2GB headers, causing the server to run out of memory after allocating multiple large packets.")]
        public int maxMessageSize = 16 * 1024;

        [FormerlySerializedAs("handshakeMaxSize")]
        [Tooltip("Max size for http header send as handshake for websockets")]
        public int maxHandshakeSize = 3000;

        [FormerlySerializedAs("serverMaxMessagesPerTick")]
        [Tooltip("Caps the number of messages the server will process per tick. Allows LateUpdate to finish to let the reset of unity continue in case more messages arrive before they are processed")]
        public int serverMaxMsgsPerTick = 10000;

        [FormerlySerializedAs("clientMaxMessagesPerTick")]
        [Tooltip("Caps the number of messages the client will process per tick. Allows LateUpdate to finish to let the reset of unity continue in case more messages arrive before they are processed")]
        public int clientMaxMsgsPerTick = 1000;

        [Tooltip("Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds)")]
        public int sendTimeout = 5000;

        [Tooltip("How long without a message before disconnecting (in milliseconds)")]
        public int receiveTimeout = 20000;

        [Tooltip("disables nagle algorithm. lowers CPU% and latency but increases bandwidth")]
        public bool noDelay = true;

        [Header("Obsolete SSL settings")]

        [Tooltip("Requires wss connections on server, only to be used with SSL cert.json, never with reverse proxy.\nNOTE: if sslEnabled is true clientUseWss is forced true, even if not checked.")]
        public bool sslEnabled;

        [Tooltip("Protocols that SSL certificate is created to support.")]
        public SslProtocols sslProtocols = SslProtocols.Tls12;

        [Tooltip("Path to json file that contains path to cert and its password\nUse Json file so that cert password is not included in client builds\nSee Assets/Mirror/Transports/.cert.example.Json")]
        public string sslCertJson = "./cert.json";

        [Header("Server settings")]

        [Tooltip("Port to use for server")]
        public ushort port = 7778;
        public ushort Port
        {
            get
            {
#if UNITY_WEBGL
                if (clientWebsocketSettings.ClientPortOption == WebsocketPortOption.SpecifyPort)
                    return clientWebsocketSettings.CustomClientPort;
                else
                    return port;
#else
                return port;
#endif
            }
            set
            {
#if UNITY_WEBGL
                if (clientWebsocketSettings.ClientPortOption == WebsocketPortOption.SpecifyPort)
                    clientWebsocketSettings.CustomClientPort = value;
                else
                    port = value;
#else
                port = value;
#endif
            }
        }

        [Tooltip("Groups messages in queue before calling Stream.Send")]
        public bool batchSend = true;

        [Tooltip("Waits for 1ms before grouping and sending messages.\n" +
            "This gives time for mirror to finish adding message to queue so that less groups need to be made.\n" +
            "If WaitBeforeSend is true then BatchSend Will also be set to true")]
        public bool waitBeforeSend = true;

        [Header("Client settings")]

        [Tooltip("Sets connect scheme to wss. Useful when client needs to connect using wss when TLS is outside of transport.\nNOTE: if sslEnabled is true clientUseWss is also true")]
        public bool clientUseWss;
        public ClientWebsocketSettings clientWebsocketSettings;

        [Header("Logging")]

        [Tooltip("Choose minimum severity level for logging\nFlood level requires Debug build")]
        [SerializeField] Log.Levels minimumLogLevel = Log.Levels.Warn;

        /// <summary>
        /// <para>Gets _logLevels field</para>
        /// <para>Sets _logLevels and Log.level fields</para>
        /// </summary>
        public Log.Levels LogLevels
        {
            get => minimumLogLevel;
            set
            {
                minimumLogLevel = value;
                Log.minLogLevel = minimumLogLevel;
            }
        }

        SimpleWebClient client;
        SimpleWebServer server;

        TcpConfig TcpConfig => new TcpConfig(noDelay, sendTimeout, receiveTimeout);

        void Awake()
        {
            Log.minLogLevel = minimumLogLevel;
        }

        public override string ToString() => $"SWT [{port}]";

        void OnValidate()
        {
            Log.minLogLevel = minimumLogLevel;
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

        public override bool IsEncrypted => ClientConnected() && (clientUseWss || sslEnabled) || ServerActive() && sslEnabled;

        // Not technically correct, but there's no good way to get the actual cipher, especially in browser
        // When using reverse proxy, connection between proxy and server is not encrypted.
        public override string EncryptionCipher => "TLS";

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

            switch (clientWebsocketSettings.ClientPortOption)
            {
                case WebsocketPortOption.SpecifyPort:
                    builder.Port = clientWebsocketSettings.CustomClientPort;
                    break;
                case WebsocketPortOption.MatchWebpageProtocol:
                    // not including a port in the builder allows the webpage to drive the port
                    // https://github.com/MirrorNetworking/Mirror/pull/3477
                    break;
                default: // default case handles ClientWebsocketPortOption.DefaultSameAsServerPort
                    builder.Port = port;
                    break;
            }

            ClientConnect(builder.Uri);
        }

        public override void ClientConnect(Uri uri)
        {
            // connecting or connected
            if (ClientConnected())
            {
                Log.Warn("[SWT-ClientConnect]: Already Connected");
                return;
            }

            client = SimpleWebClient.Create(maxMessageSize, clientMaxMsgsPerTick, TcpConfig);
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

            // We will not invoke OnClientError if minLogLevel is set to None
            // We only send the full exception if minLogLevel is set to Verbose
            switch (Log.minLogLevel)
            {
                case Log.Levels.Flood:
                case Log.Levels.Verbose:
                    client.onError += (Exception e) =>
                    {
                        OnClientError.Invoke(TransportError.Unexpected, e.ToString());
                        ClientDisconnect();
                    };
                    break;
                case Log.Levels.Info:
                case Log.Levels.Warn:
                case Log.Levels.Error:
                    client.onError += (Exception e) =>
                    {
                        OnClientError.Invoke(TransportError.Unexpected, e.Message);
                        ClientDisconnect();
                    };
                    break;
            }

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
                Log.Error("[SWT-ClientSend]: Not Connected");
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SWT-ClientSend]: Message greater than max size");
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SWT-ClientSend]: Message count was zero");
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
                Log.Warn("[SWT-ServerStart]: Server Already Started");

            SslConfig config = SslConfigLoader.Load(sslEnabled, sslCertJson, sslProtocols);
            server = new SimpleWebServer(serverMaxMsgsPerTick, TcpConfig, maxMessageSize, maxHandshakeSize, config);

            server.onConnect += OnServerConnected.Invoke;
            server.onDisconnect += OnServerDisconnected.Invoke;
            server.onData += (int connId, ArraySegment<byte> data) => OnServerDataReceived.Invoke(connId, data, Channels.Reliable);

            // We will not invoke OnServerError if minLogLevel is set to None
            // We only send the full exception if minLogLevel is set to Verbose
            switch (Log.minLogLevel)
            {
                case Log.Levels.Flood:
                case Log.Levels.Verbose:
                    server.onError += (connId, exception) =>
                    {
                        OnServerError(connId, TransportError.Unexpected, exception.ToString());
                        ServerDisconnect(connId);
                    };
                    break;
                case Log.Levels.Info:
                case Log.Levels.Warn:
                case Log.Levels.Error:
                    server.onError += (connId, exception) =>
                    {
                        OnServerError(connId, TransportError.Unexpected, exception.Message);
                        ServerDisconnect(connId);
                    };
                    break;
            }

            SendLoopConfig.batchSend = batchSend || waitBeforeSend;
            SendLoopConfig.sleepBeforeSend = waitBeforeSend;

            server.Start(port);
        }

        public override void ServerStop()
        {
            if (ServerActive())
            {
                server.Stop();
                server = null;
            }
        }

        public override void ServerDisconnect(int connectionId)
        {
            if (ServerActive())
                server.KickClient(connectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (!ServerActive())
            {
                Log.Error("[SWT-ServerSend]: Server Not Active");
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SWT-ServerSend]: Message greater than max size");
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SWT-ServerSend]: Message count was zero");
                return;
            }

            server.SendOne(connectionId, segment);

            // call event. might be null if no statistics are listening etc.
            OnServerDataSent?.Invoke(connectionId, segment, Channels.Reliable);
        }

        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);

        public Request ServerGetClientRequest(int connectionId) => server.GetClientRequest(connectionId);

        // messages should always be processed in early update
        public override void ServerEarlyUpdate()
        {
            server?.ProcessMessageQueue(this);
        }

        #endregion
    }
}
