// wraps Telepathy for use as HLAPI TransportLayer
using System;
using UnityEngine;
namespace Mirror
{
    public class TelepathyTransport : TransportLayer
    {
        // events for the client
        public event Action OnClientConnect;
        public event Action<byte[]> OnClientData;
        public event Action<Exception> OnClientError;
        public event Action OnClientDisconnect;

        // events for the server
        public event Action<int> OnServerConnect;
        public event Action<int, byte[]> OnServerData;
        public event Action<int, Exception> OnServerError;
        public event Action<int> OnServerDisconnect;

        protected Telepathy.Client client = new Telepathy.Client();
        protected Telepathy.Server server = new Telepathy.Server();

        public TelepathyTransport()
        {
            // dispatch the events from the server
            server.Connected += (id) => OnServerConnect?.Invoke(id);
            server.Disconnected += (id) => OnServerDisconnect?.Invoke(id);
            server.ReceivedData += (id, data) => OnServerData?.Invoke(id, data);
            server.ReceivedError += (id, exception) => OnServerError?.Invoke(id, exception);

            // dispatch events from the client
            client.OnConnected += () => OnClientConnect?.Invoke();
            client.Disconnected += () => OnClientDisconnect?.Invoke();
            client.ReceivedData += (data) => OnClientData?.Invoke(data);
            client.ReceivedError += (exception) => OnClientError?.Invoke(exception);


            // tell Telepathy to use Unity's Debug.Log
            Telepathy.Logger.LogMethod = Debug.Log;
            Telepathy.Logger.LogWarningMethod = Debug.LogWarning;
            Telepathy.Logger.LogErrorMethod = Debug.LogError;

            // HLAPI's local connection uses hard coded connectionId '0', so we
            // need to make sure that external connections always start at '1'
            // by simple eating the first one before the server starts
            Telepathy.Server.NextConnectionId();

            Debug.Log("TelepathyTransport initialized!");
        }

        // client
        public virtual bool ClientConnected() { return client.Connected; }
        public virtual void ClientConnect(string address, int port) { client.Connect(address, port); }
        public virtual void ClientSend(int channelId, byte[] data) { client.Send(data); }
        public virtual void ClientDisconnect() { client.Disconnect(); }

        // server
        public virtual bool ServerActive() { return server.Active; }
        public virtual void ServerStart(string address, int port, int maxConnections)
        {
            server.Listen(port, maxConnections);
        }

        public virtual void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            Debug.LogWarning("TelepathyTransport.ServerStartWebsockets not implemented yet!");
        }

        public virtual void ServerSend(int connectionId, int channelId, byte[] data) { server.Send(connectionId, data); }

        public virtual bool ServerDisconnect(int connectionId)
        {
            return server.Disconnect(connectionId);
        }

        public virtual bool GetConnectionInfo(int connectionId, out string address) { return server.GetConnectionInfo(connectionId, out address); }
        public virtual void ServerStop() { server.Stop(); }

        // common
        public virtual void Shutdown()
        {
            client.Disconnect();
            server.Stop();
        }

        public int GetMaxPacketSize(int channelId)
        {
            // Telepathy's limit is Array.Length, which is int
            return int.MaxValue;
        }
    }
}