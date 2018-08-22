// wraps Telepathy for use as HLAPI TransportLayer
namespace UnityEngine.Networking
{
    public class TelepathyTransport : TransportLayer
    {
        Telepathy.Client client = new Telepathy.Client();
        Telepathy.Server server = new Telepathy.Server();

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
        public bool ClientConnected() { return client.Connected; }
        public void ClientConnect(string address, int port) { client.Connect(address, port); }
        public bool ClientSend(byte[] data) { return client.Send(data); }
        public bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
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
        public float ClientGetRTT() { return 0; } // TODO
        public void ClientDisconnect() { client.Disconnect(); }

        // server
        public bool ServerActive() { return server.Active; }
        public void ServerStart(string address, int port, int maxConnections) { server.Start(port, maxConnections); }
        public void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            Debug.LogWarning("TelepathyTransport.ServerStartWebsockets not implemented yet!");
        }
        public bool ServerSend(int connectionId, byte[] data) { return server.Send(connectionId, data); }
        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
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
        public bool GetConnectionInfo(int connectionId, out string address) { return server.GetConnectionInfo(connectionId, out address); }
        public void ServerStop() { server.Stop(); }

        // common
        public void Shutdown()
        {
            Debug.Log("TelepathyTransport Shutdown()");
            client.Disconnect();
            server.Stop();
        }
    }
}