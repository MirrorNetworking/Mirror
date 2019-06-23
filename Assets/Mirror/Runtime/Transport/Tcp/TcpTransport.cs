// wraps Telepathy for use as HLAPI TransportLayer
using System;
using UnityEngine;

namespace Mirror.Tcp
{
    public class TcpTransport : Transport
    {
        protected Client client = new Client();
        protected Server server = new Server();

        public int port = 7777;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        public void Awake()
        {
            // dispatch the events from the server
            server.Connected += (connectionId) => OnServerConnected.Invoke(connectionId);
            server.Disconnected += (connectionId) => OnServerDisconnected.Invoke(connectionId);
            server.ReceivedData += (connectionId, data) => OnServerDataReceived.Invoke(connectionId, new ArraySegment<byte>(data));
            server.ReceivedError += (connectionId, error) => OnServerError.Invoke(connectionId, error);

            // dispatch events from the client
            client.Connected += () => OnClientConnected.Invoke();
            client.Disconnected += () => OnClientDisconnected.Invoke();
            client.ReceivedData += (data) => OnClientDataReceived.Invoke(new ArraySegment<byte>(data));
            client.ReceivedError += (error) => OnClientError.Invoke(error);

            // configure
            client.NoDelay = NoDelay;
            server.NoDelay = NoDelay;

            Debug.Log("Tcp transport initialized!");
        }

        // client
        public override bool ClientConnected() { return client.IsConnected; }
        public override void ClientConnect(string address) { client.Connect(address, port); }
        public override bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            client.Send(data);
            return true;
        }
        public override void ClientDisconnect() 
        {
            client.Disconnect(); 
        }

        // server
        public override bool ServerActive() { return server.Active; }
        public override void ServerStart()
        {
            server.Listen(port);
        }

        public override bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
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

        public override void ServerStop() { server.Stop(); }

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