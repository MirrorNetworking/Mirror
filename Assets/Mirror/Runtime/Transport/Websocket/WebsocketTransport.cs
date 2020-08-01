using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using UnityEngine;

namespace Mirror.Websocket
{
    [HelpURL("https://mirror-networking.com/docs/Transports/WebSockets.html")]
    public class WebsocketTransport : Transport
    {
        public const string Scheme = "ws";
        public const string SecureScheme = "wss";

        protected Client client = new Client();
        protected Server server = new Server();

        [Header("Transport Settings")]

        [Tooltip("Connection Port.")]
        public int port = 7778;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay.")]
        public bool NoDelay = true;

        [Header("Secure Sockets (SSL/WSS).")]

        [Tooltip("Indicates if SSL/WSS protocol will be used with the PFX Certificate file below.")]
        public bool Secure;

        [Tooltip("Full path and filename to PFX Certificate file generated from web hosting environment.")]
        public string CertificatePath;

        [Tooltip("Password for PFX Certificate file above.")]
        public string CertificatePassword;

        [Tooltip("SSL and TLS Protocols")]
        public SslProtocols EnabledSslProtocols = SslProtocols.Default;

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

        void OnEnable()
        {
            server.enabled = true;
            client.enabled = true;
        }

        void OnDisable()
        {
            server.enabled = false;
            client.enabled = false;
        }

        void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled)
                return;

            // process a maximum amount of client messages per tick
            // TODO add clientMaxReceivesPerTick same as telepathy
            while (true)
            {
                // stop when there is no more message
                if (!client.ProcessClientMessage())
                {
                    break;
                }

                // Some messages can disable transport
                // If this is disabled stop processing message in queue
                if (!enabled)
                {
                    break;
                }
            }
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

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Secure ? SecureScheme : Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = port;
            return builder.Uri;
        }


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
                    Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(CertificatePath, CertificatePassword),
                    ClientCertificateRequired = false,
                    CheckCertificateRevocation = false,
                    EnabledSslProtocols = EnabledSslProtocols
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
