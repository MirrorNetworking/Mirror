// wraps Telepathy for use as HLAPI TransportLayer
using UnityEngine;
namespace Mirror
{
    public class TelepathyTransport : TransportLayer
    {
        protected Telepathy.Client client = new Telepathy.Client();
        protected Telepathy.Server server = new Telepathy.Server();

        public TelepathyTransport()
        {
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
        public virtual void ClientConnect(string address, ushort port) { client.Connect(address, port); }
        public virtual bool ClientSend(byte channelId, byte[] data) { return client.Send(data); }
        public virtual bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            Telepathy.Message message;
            if (client.GetNextMessage(out message))
            {
                switch (message.eventType)
                {
                    // convert Telepathy EventType to TransportEvent
                    case Telepathy.EventType.Connected:
                        transportEvent = TransportEvent.Connected;
                        break;
                    case Telepathy.EventType.Data:
                        transportEvent = TransportEvent.Data;
                        break;
                    case Telepathy.EventType.Disconnected:
                        transportEvent = TransportEvent.Disconnected;
                        break;
                    default:
                        transportEvent = TransportEvent.Disconnected;
                        break;
                }

                // assign rest of the values and return true
                data = message.data;
                return true;
            }

            transportEvent = TransportEvent.Disconnected;
            data = null;
            return false;
        }
        public virtual void ClientDisconnect() { client.Disconnect(); }

        // server
        public virtual bool ServerActive() { return server.Active; }
        public virtual void ServerStart(string address, ushort port) { server.Start(port); }
        public virtual void ServerStartWebsockets(string address, ushort port)
        {
            Debug.LogWarning("TelepathyTransport.ServerStartWebsockets not implemented yet!");
        }
        public virtual bool ServerSend(int connectionId, byte channelId, byte[] data) { return server.Send(connectionId, data); }
        public virtual bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            Telepathy.Message message;
            if (server.GetNextMessage(out message))
            {
                switch (message.eventType)
                {
                    // convert Telepathy EventType to TransportEvent
                    case Telepathy.EventType.Connected:
                        transportEvent = TransportEvent.Connected;
                        break;
                    case Telepathy.EventType.Data:
                        transportEvent = TransportEvent.Data;
                        break;
                    case Telepathy.EventType.Disconnected:
                        transportEvent = TransportEvent.Disconnected;
                        break;
                    default:
                        transportEvent = TransportEvent.Disconnected;
                        break;
                }

                // assign rest of the values and return true
                connectionId = message.connectionId;
                data = message.data;
                return true;
            }

            connectionId = -1;
            transportEvent = TransportEvent.Disconnected;
            data = null;
            return false;
        }
        public virtual bool ServerDisconnect(int connectionId)
        {
            return server.Disconnect(connectionId);
        }
        public virtual bool GetConnectionInfo(int connectionId, out string address) { return server.GetConnectionInfo(connectionId, out address); }
        public virtual void ServerStop() { server.Stop(); }

        // common
        public virtual void Shutdown()
        {
            Debug.Log("TelepathyTransport Shutdown()");
            client.Disconnect();
            server.Stop();
        }

        public int GetMaxPacketSize(byte channelId)
        {
            // Telepathy's limit is Array.Length, which is int
            return int.MaxValue;
        }
    }
}