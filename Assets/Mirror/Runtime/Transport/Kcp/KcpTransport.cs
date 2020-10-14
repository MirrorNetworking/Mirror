using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpTransport : Transport
    {
        Socket socket;

        [Header("Transport Configuration")]
        public ushort Port = 7777;

        [SerializeField] private string _bindAddress = "localhost";

        internal readonly Dictionary<EndPoint, KcpServerConnection> connectedClients = new Dictionary<EndPoint, KcpServerConnection>();
        readonly Channel<KcpServerConnection> acceptedConnections = Channel.CreateSingleConsumerUnbounded<KcpServerConnection>();

        public override IEnumerable<string> Scheme => new[] { "kcp" };

        readonly byte[] buffer = new byte[1500];
        /// <summary>
        ///     Open up the port and listen for connections
        ///     Use in servers.
        /// </summary>
        /// <exception>If we cannot start the transport</exception>
        /// <returns></returns>
        public override UniTask ListenAsync()
        {
            socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            socket.DualMode = true;
            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));
            return UniTask.CompletedTask;
        }

        EndPoint newClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
        public void Update()
        {
            while (socket != null && socket.Poll(0, SelectMode.SelectRead)) {
                int msgLength = socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref newClientEP);
                RawInput(newClientEP, buffer, msgLength);
            }
        }

        void RawInput(EndPoint endpoint, byte[] data, int msgLength)
        {
            // is this a new connection?                    
            if (!connectedClients.TryGetValue(endpoint, out KcpServerConnection connection))
            {
                // add it to a queue
                connection = new KcpServerConnection(socket, endpoint);
                acceptedConnections.Writer.TryWrite(connection);
                connectedClients.Add(endpoint, connection);
                connection.Disconnected += () =>
                {
                    connectedClients.Remove(endpoint);
                };
            }

            connection.RawInput(data, msgLength);
        }

        /// <summary>
        ///     Stop listening to the port
        /// </summary>
        public override void Disconnect()
        {
            socket?.Close();
            socket = null;
        }

        /// <summary>
        ///     Accepts a connection from a client.
        ///     After ListenAsync completes,  clients will queue up until you call AcceptAsync
        ///     then you get the connection to the client
        /// </summary>
        /// <returns>The connection to a client</returns>
        public override async UniTask<IConnection> AcceptAsync()
        {
            KcpServerConnection connection = await acceptedConnections.Reader.ReadAsync();

            await connection.Handshake();

            return connection;
        }

        /// <summary>
        ///     Retrieves the address of this server.
        ///     Useful for network discovery
        /// </summary>
        /// <returns>the url at which this server can be reached</returns>
        public override IEnumerable<Uri> ServerUri()
        {
            var builder = new UriBuilder
            {
                Scheme = "kcp",
                Host = _bindAddress,
                Port = Port
            };
            return new[] { builder.Uri };
        }

        /// <summary>
        ///     Determines if this transport is supported in the current platform
        /// </summary>
        /// <returns>true if the transport works in this platform</returns>
        public override bool Supported => Application.platform != RuntimePlatform.WebGLPlayer;

        /// <summary>
        ///     Connect to a server located at a provided uri
        /// </summary>
        /// <param name="uri">address of the server to connect to</param>
        /// <returns>The connection to the server</returns>
        /// <exception>If connection cannot be established</exception>
        public override async UniTask<IConnection> ConnectAsync(Uri uri)
        {
            var client = new KcpClientConnection();

            ushort port = (ushort)(uri.IsDefaultPort? Port : uri.Port);

            await client.ConnectAsync(uri.Host, port);
            return client;
        }
    }
}
