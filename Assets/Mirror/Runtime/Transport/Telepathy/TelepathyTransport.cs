// wraps Telepathy for use as HLAPI TransportLayer
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

// Replaced by Kcp November 2020
namespace Mirror
{
    [HelpURL("https://github.com/vis2k/Telepathy/blob/master/README.md")]
    [DisallowMultipleComponent]
    public class TelepathyTransport : Transport
    {
        // scheme used by this transport
        // "tcp4" means tcp with 4 bytes header, network byte order
        public const string Scheme = "tcp4";

        public ushort port = 7777;

        [Header("Common")]
        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        [Tooltip("Send timeout in milliseconds.")]
        public int SendTimeout = 5000;

        [Tooltip("Receive timeout in milliseconds. High by default so users don't time out during scene changes.")]
        public int ReceiveTimeout = 30000;

        [Header("Server")]
        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker might send multiple fake packets with 2GB headers, causing the server to run out of memory after allocating multiple large packets.")]
        public int serverMaxMessageSize = 16 * 1024;

        [Tooltip("Server processes a limit amount of messages per tick to avoid a deadlock where it might end up processing forever if messages come in faster than we can process them.")]
        public int serverMaxReceivesPerTick = 10000;

        [Tooltip("Server send queue limit per connection for pending messages. Telepathy will disconnect a connection's queues reach that limit for load balancing. Better to kick one slow client than slowing down the whole server.")]
        public int serverSendQueueLimitPerConnection = 10000;

        [Tooltip("Server receive queue limit per connection for pending messages. Telepathy will disconnect a connection's queues reach that limit for load balancing. Better to kick one slow client than slowing down the whole server.")]
        public int serverReceiveQueueLimitPerConnection = 10000;

        [Header("Client")]
        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker host might send multiple fake packets with 2GB headers, causing the connected clients to run out of memory after allocating multiple large packets.")]
        public int clientMaxMessageSize = 16 * 1024;

        [Tooltip("Client processes a limit amount of messages per tick to avoid a deadlock where it might end up processing forever if messages come in faster than we can process them.")]
        public int clientMaxReceivesPerTick = 1000;

        [Tooltip("Client send queue limit for pending messages. Telepathy will disconnect if the connection's queues reach that limit in order to avoid ever growing latencies.")]
        public int clientSendQueueLimit = 10000;

        [Tooltip("Client receive queue limit for pending messages. Telepathy will disconnect if the connection's queues reach that limit in order to avoid ever growing latencies.")]
        public int clientReceiveQueueLimit = 10000;

        Telepathy.Client client;
        Telepathy.Server server;

        // scene change message needs to halt  message processing immediately
        // Telepathy.Tick() has a enabledCheck parameter that we can use, but
        // let's only allocate it once.
        Func<bool> enabledCheck;

        void Awake()
        {
            // create client & server
            client = new Telepathy.Client(clientMaxMessageSize);
            server = new Telepathy.Server(serverMaxMessageSize);

            // tell Telepathy to use Unity's Debug.Log
            Telepathy.Log.Info = Debug.Log;
            Telepathy.Log.Warning = Debug.LogWarning;
            Telepathy.Log.Error = Debug.LogError;

            // client hooks
            // other systems hook into transport events in OnCreate or
            // OnStartRunning in no particular order. the only way to avoid
            // race conditions where telepathy uses OnConnected before another
            // system's hook (e.g. statistics OnData) was added is to wrap
            // them all in a lambda and always call the latest hook.
            // (= lazy call)
            client.OnConnected = () => OnClientConnected.Invoke();
            client.OnData = (segment) => OnClientDataReceived.Invoke(segment, Channels.Reliable);
            client.OnDisconnected = () => OnClientDisconnected.Invoke();

            // client configuration
            client.NoDelay = NoDelay;
            client.SendTimeout = SendTimeout;
            client.ReceiveTimeout = ReceiveTimeout;
            client.SendQueueLimit = clientSendQueueLimit;
            client.ReceiveQueueLimit = clientReceiveQueueLimit;

            // server hooks
            // other systems hook into transport events in OnCreate or
            // OnStartRunning in no particular order. the only way to avoid
            // race conditions where telepathy uses OnConnected before another
            // system's hook (e.g. statistics OnData) was added is to wrap
            // them all in a lambda and always call the latest hook.
            // (= lazy call)
            server.OnConnected = (connectionId) => OnServerConnected.Invoke(connectionId);
            server.OnData = (connectionId, segment) => OnServerDataReceived.Invoke(connectionId, segment, Channels.Reliable);
            server.OnDisconnected = (connectionId) => OnServerDisconnected.Invoke(connectionId);

            // server configuration
            server.NoDelay = NoDelay;
            server.SendTimeout = SendTimeout;
            server.ReceiveTimeout = ReceiveTimeout;
            server.SendQueueLimit = serverSendQueueLimitPerConnection;
            server.ReceiveQueueLimit = serverReceiveQueueLimitPerConnection;

            // allocate enabled check only once
            enabledCheck = () => enabled;

            Debug.Log("TelepathyTransport initialized!");
        }

        public override bool Available()
        {
            // C#'s built in TCP sockets run everywhere except on WebGL
            return Application.platform != RuntimePlatform.WebGLPlayer;
        }

        // client
        public override bool ClientConnected() => client.Connected;
        public override void ClientConnect(string address) => client.Connect(address, port);
        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != Scheme)
                throw new ArgumentException($"Invalid url {uri}, use {Scheme}://host:port instead", nameof(uri));

            int serverPort = uri.IsDefaultPort ? port : uri.Port;
            client.Connect(uri.Host, serverPort);
        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId) => client.Send(segment);
        public override void ClientDisconnect() => client.Disconnect();
        // messages should always be processed in early update
        public override void ClientEarlyUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled) return;

            // process a maximum amount of client messages per tick
            // IMPORTANT: check .enabled to stop processing immediately after a
            //            scene change message arrives!
            client.Tick(clientMaxReceivesPerTick, enabledCheck);
        }

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = port;
            return builder.Uri;
        }
        public override bool ServerActive() => server.Active;
        public override void ServerStart() => server.Start(port);
        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId) => server.Send(connectionId, segment);
        public override void ServerDisconnect(int connectionId) => server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId)
        {
            try
            {
                return server.GetClientAddress(connectionId);
            }
            catch (SocketException)
            {
                // using server.listener.LocalEndpoint causes an Exception
                // in UWP + Unity 2019:
                //   Exception thrown at 0x00007FF9755DA388 in UWF.exe:
                //   Microsoft C++ exception: Il2CppExceptionWrapper at memory
                //   location 0x000000E15A0FCDD0. SocketException: An address
                //   incompatible with the requested protocol was used at
                //   System.Net.Sockets.Socket.get_LocalEndPoint ()
                // so let's at least catch it and recover
                return "unknown";
            }
        }
        public override void ServerStop() => server.Stop();
        // messages should always be processed in early update
        public override void ServerEarlyUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled) return;

            // process a maximum amount of server messages per tick
            // IMPORTANT: check .enabled to stop processing immediately after a
            //            scene change message arrives!
            server.Tick(serverMaxReceivesPerTick, enabledCheck);
        }

        // common
        public override void Shutdown()
        {
            Debug.Log("TelepathyTransport Shutdown()");
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId)
        {
            return serverMaxMessageSize;
        }

        public override string ToString()
        {
            if (server.Active && server.listener != null)
            {
                // printing server.listener.LocalEndpoint causes an Exception
                // in UWP + Unity 2019:
                //   Exception thrown at 0x00007FF9755DA388 in UWF.exe:
                //   Microsoft C++ exception: Il2CppExceptionWrapper at memory
                //   location 0x000000E15A0FCDD0. SocketException: An address
                //   incompatible with the requested protocol was used at
                //   System.Net.Sockets.Socket.get_LocalEndPoint ()
                // so let's use the regular port instead.
                return "Telepathy Server port: " + port;
            }
            else if (client.Connecting || client.Connected)
            {
                return "Telepathy Client port: " + port;
            }
            return "Telepathy (inactive/disconnected)";
        }
    }
}
