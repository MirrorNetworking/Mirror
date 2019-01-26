// wraps Telepathy for use as HLAPI TransportLayer
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
            Telepathy.Logger.LogMethod = Debug.Log;
            Telepathy.Logger.LogWarningMethod = Debug.LogWarning;
            Telepathy.Logger.LogErrorMethod = Debug.LogError;

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
        public override bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
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
        public override void ClientDisconnect() { client.Disconnect(); }

        // server
        public override bool ServerActive() { return server.Active; }
        public override void ServerStart() { server.Start(port); }
        public override bool ServerSend(int connectionId, int channelId, byte[] data) { return server.Send(connectionId, data); }
        public override bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
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
            if (server.Active)
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
