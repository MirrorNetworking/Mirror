using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Websocket
{
    public class WebsocketTransport : Transport
    {
        public const string Scheme = "ws";
        public const string SecureScheme = "wss";


        protected Client client = new Client();
        protected Server server = new Server();

        public int port = 7778;

        public bool Secure;
        public string CertificatePath;
        public string CertificatePassword;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        public WebsocketTransport()
        {
            // dispatch the events from the server
            server.Connected += (connectionId) => OnServerConnected.Invoke(connectionId);
            server.Disconnected += (connectionId) => OnServerDisconnected.Invoke(connectionId);
            server.ReceivedData += (connectionId, data) => OnServerDataReceived.Invoke(connectionId, data, Channels.DefaultReliable);
            server.ReceivedError += (connectionId, error) => OnServerError.Invoke(connectionId, error);

            // dispatch events from the client
            client.Connected += () => OnClientConnected.Invoke();
            client.Disconnected += () => OnClientDisconnected.Invoke();
            client.ReceivedData += (data) => OnClientDataReceived.Invoke(data, Channels.DefaultReliable);
            client.ReceivedError += (error) => OnClientError.Invoke(error);

            // configure
            client.NoDelay = NoDelay;
            server.NoDelay = NoDelay;

            Debug.Log("Websocket transport initialized!");
        }

        public override bool Available()
        {
            // WebSockets should be available on all platforms, including WebGL (automatically) using our included JSLIB code
            return true;
        }

        // client
        public override bool ClientConnected() => client.IsConnected;

        public override void ClientConnect(string host)
        {
            if (Secure)
            {
                client.Connect(new Uri($"wss://{host}:{port}"));
            }
            else
            {
                client.Connect(new Uri($"ws://{host}:{port}"));
            }
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != Scheme && uri.Scheme != SecureScheme)
                throw new ArgumentException($"Invalid url {uri}, use {Scheme}://host:port or {SecureScheme}://host:port instead", nameof(uri));

            if (uri.IsDefaultPort)
            {
                UriBuilder uriBuilder = new UriBuilder(uri);
                uriBuilder.Port = port;
                uri = uriBuilder.Uri;
            }

            client.Connect(uri);
        }

        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            client.Send(segment);
            return true;
        }

        public override void ClientDisconnect() => client.Disconnect();

        // server
        public override bool ServerActive() => server.Active;

        public override void ServerStart()
        {
            server._secure = Secure;
            if (Secure)
            {
                server._secure = Secure;
                server._sslConfig = new Server.SslConfiguration
                {
                    Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                        System.IO.Path.Combine(Application.dataPath, CertificatePath),
                        CertificatePassword),
                    ClientCertificateRequired = false,
                    CheckCertificateRevocation = false,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Default
                };
            }
            _ = server.Listen(port);
        }

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            // send to all
            foreach (int connectionId in connectionIds)
                server.Send(connectionId, segment);
            return true;
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return server.Disconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return server.GetClientAddress(connectionId);
        }
        public override void ServerStop() => server.Stop();

        // common
        public override void Shutdown()
        {
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId)
        {
            // Telepathy's limit is Array.Length, which is int
            return int.MaxValue;
        }

        public override string ToString()
        {
            if (client.Connecting || client.IsConnected)
            {
                return client.ToString();
            }
            if (server.Active)
            {
                return server.ToString();
            }
            return "";
        }
    }
}
