using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Ninja.WebSockets;

namespace Mirror.Websocket
{
    public class AsyncWsTransport : AsyncTransport
    {
        public int Port = 7778;

        #region Server
        private TcpListener listener;
        private readonly IWebSocketServerFactory webSocketServerFactory = new WebSocketServerFactory();

        public override string Scheme => "ws";

        public override async Task<IConnection> AcceptAsync()
        {
            try
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();
                var options = new WebSocketServerOptions { KeepAliveInterval = TimeSpan.FromSeconds(30), SubProtocol = "binary" };

                Stream stream = tcpClient.GetStream();

                WebSocketHttpContext context = await webSocketServerFactory.ReadHttpHeaderFromStreamAsync(tcpClient, stream);

                WebSocket webSocket = await webSocketServerFactory.AcceptWebSocketAsync(context, options);
                return new WebsocketConnection(webSocket);
            }
            catch (ObjectDisposedException)
            {
                // expected,  the connection was closed
                return null;
            }
        }

        public override void Disconnect()
        {
            listener.Stop();
        }

        public override Task ListenAsync()
        {
            listener = TcpListener.Create(Port);
            listener.Server.NoDelay = true;
            listener.Start();
            return Task.CompletedTask;
        }

        public override Uri ServerUri()
        {
            var builder = new UriBuilder
            {
                Host = Dns.GetHostName(),
                Port = this.Port,
                Scheme = "ws"
            };

            return builder.Uri;
        }
        #endregion

        #region Client
        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            var options = new WebSocketClientOptions
            {
                NoDelay = true,
                KeepAliveInterval = TimeSpan.Zero,
                SecWebSocketProtocol = "binary"
            };

            if (uri.IsDefaultPort)
            {
                var builder = new UriBuilder(uri)
                {
                    Port = Port
                };
                uri = builder.Uri;
            }

            var clientFactory = new WebSocketClientFactory();
            WebSocket webSocket = await clientFactory.ConnectAsync(uri, options);

            return new WebsocketConnection(webSocket);
        }

        #endregion

    }
}