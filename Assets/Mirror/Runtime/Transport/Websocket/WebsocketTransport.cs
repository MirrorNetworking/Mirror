using System;
using UnityEngine;

namespace Mirror.Websocket
{
    public class WebsocketTransport : Transport
    {

        protected Client client = new Client();
        protected Server server = new Server();

        public int port = 7778;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        public WebsocketTransport()
        {
            // dispatch the events from the server
            server.Connected += (connectionId) => OnServerConnected.Invoke(connectionId);
            server.Disconnected += (connectionId) => OnServerDisconnected.Invoke(connectionId);
            server.ReceivedData += (connectionId, data) => OnServerDataReceived.Invoke(connectionId, data);
            server.ReceivedError += (connectionId, error) => OnServerError.Invoke(connectionId, error);

            // dispatch events from the client
            client.Connected += () => OnClientConnected.Invoke();
            client.Disconnected += () => OnClientDisconnected.Invoke();
            client.ReceivedData += (data) => OnClientDataReceived.Invoke(data);
            client.ReceivedError += (error) => OnClientError.Invoke(error);

            // configure
            client.NoDelay = NoDelay;
            server.NoDelay = NoDelay;

            // HLAPI's local connection uses hard coded connectionId '0', so we
            // need to make sure that external connections always start at '1'
            // by simple eating the first one before the server starts
            Server.NextConnectionId();

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
            client.Connect(new Uri($"ws://{host}:{port}"));
        }

        public override bool ClientSend(int channelId, byte[] data) { client.Send(data); return true; }

        public override void ClientDisconnect() => client.Disconnect();

        // server
        public override bool ServerActive() => server.Active;

        public override void ServerStart()
        {
            server.Listen(port);
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            server.Send(connectionId, data);
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
