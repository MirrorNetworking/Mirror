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
        public virtual void ClientConnect(string address, int port) { client.Connect(address, port); }
        public virtual bool ClientSend(int channelId, byte[] data) { return client.Send(data); }
        public virtual bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            Telepathy.Message message;
            if (client.GetNextMessage(out message))
            {
                // convert Telepathy EventType to TransportEvent
                if (message.eventType == Telepathy.EventType.Connected)
                    transportEvent = TransportEvent.Connected;
                else if (message.eventType == Telepathy.EventType.Data)
                    transportEvent = TransportEvent.Data;
                else if (message.eventType == Telepathy.EventType.Disconnected)
                    transportEvent = TransportEvent.Disconnected;
                else
                    transportEvent = TransportEvent.Disconnected;

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
        public virtual void ServerStart(string address, int port, int maxConnections) { server.Start(port, maxConnections); }
        public virtual void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            Debug.LogWarning("TelepathyTransport.ServerStartWebsockets not implemented yet!");
        }
        public virtual bool ServerSend(int connectionId, int channelId, byte[] data) { return server.Send(connectionId, data); }
        public virtual bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            Telepathy.Message message;
            if (server.GetNextMessage(out message))
            {
                // convert Telepathy EventType to TransportEvent
                if (message.eventType == Telepathy.EventType.Connected)
                    transportEvent = TransportEvent.Connected;
                else if (message.eventType == Telepathy.EventType.Data)
                    transportEvent = TransportEvent.Data;
                else if (message.eventType == Telepathy.EventType.Disconnected)
                    transportEvent = TransportEvent.Disconnected;
                else
                    transportEvent = TransportEvent.Disconnected;

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
    }
}