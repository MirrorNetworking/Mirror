// wraps Telepathy for use as HLAPI TransportLayer
using System;
using UnityEngine;
namespace Mirror
{
    public class TelepathyTransport : Transport
    {
        public ushort port = 7777;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        protected Telepathy.Client client = new Telepathy.Client();
        protected Telepathy.Server server = new Telepathy.Server();

        void Awake()
        {
            // tell Telepathy to use Unity's Debug.Log
            Telepathy.Logger.Log = Debug.Log;
            Telepathy.Logger.LogWarning = Debug.LogWarning;
            Telepathy.Logger.LogError = Debug.LogError;

            // configure
            client.NoDelay = NoDelay;
            server.NoDelay = NoDelay;

            // HLAPI's local connection uses hard coded connectionId '0', so we
            // need to make sure that external connections always start at '1'
            // by simple eating the first one before the server starts
            Telepathy.Server.NextConnectionId();

            Debug.Log("TelepathyTransport initialized!");
        }

        // client
        public override bool ClientConnected() { return client.Connected; }
        public override void ClientConnect(string address) { client.Connect(address, port); }
        public override bool ClientSend(int channelId, byte[] data) { return client.Send(data); }

        bool ProcessClientMessage()
        {
            Telepathy.Message message;
            if (client.GetNextMessage(out message))
            {
                switch (message.eventType)
                {
                    // convert Telepathy EventType to TransportEvent
                    case Telepathy.EventType.Connected:
                        OnClientConnected.Invoke();
                        break;
                    case Telepathy.EventType.Data:
                        OnClientDataReceived.Invoke(message.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        OnClientDisconnected.Invoke();
                        break;
                    default:
                        // TODO:  Telepathy does not report errors at all
                        // it just disconnects,  should be fixed
                        OnClientDisconnected.Invoke();
                        break;
                }
                return true;
            }
            return false;
        }
        public override void ClientDisconnect() { client.Disconnect(); }

        public void LateUpdate()
        {
            while (ProcessClientMessage()) { }
            while (ProcessServerMessage()) { }
        }

        // server
        public override bool ServerActive() { return server.Active; }
        public override void ServerStart() { server.Start(port); }
        public override bool ServerSend(int connectionId, int channelId, byte[] data) { return server.Send(connectionId, data); }
        public bool ProcessServerMessage()
        {
            Telepathy.Message message;
            if (server.GetNextMessage(out message))
            {
                switch (message.eventType)
                {
                    // convert Telepathy EventType to TransportEvent
                    case Telepathy.EventType.Connected:
                        OnServerConnected.Invoke(message.connectionId);
                        break;
                    case Telepathy.EventType.Data:
                        OnServerDataReceived.Invoke(message.connectionId, message.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        OnServerDisconnected.Invoke(message.connectionId);
                        break;
                    default:
                        // TODO handle errors from Telepathy when telepathy can report errors
                        OnServerDisconnected.Invoke(message.connectionId);
                        break;
                }
                return true;
            }
            return false;
        }
        public override bool ServerDisconnect(int connectionId) { return server.Disconnect(connectionId); }
        public override bool GetConnectionInfo(int connectionId, out string address) { return server.GetConnectionInfo(connectionId, out address); }
        public override void ServerStop() { server.Stop(); }

        // common
        public override void Shutdown()
        {
            Debug.Log("TelepathyTransport Shutdown()");
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
            if (server.Active && server.listener != null)
            {
                return "Telepathy Server port: " + server.listener.LocalEndpoint;
            }
            else if (client.Connecting || client.Connected)
            {
                return "Telepathy Client ip: " + client.client.Client.RemoteEndPoint;
            }
            return "Telepathy (inactive/disconnected)";
        }
    }
}
