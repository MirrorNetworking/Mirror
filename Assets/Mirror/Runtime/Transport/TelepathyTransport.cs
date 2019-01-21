// wraps Telepathy for use as HLAPI TransportLayer
using System;
using UnityEngine;
namespace Mirror
{
    public class TelepathyTransport : MonoBehaviour, ITransport
    {
        public ushort port = 7777;
        protected Telepathy.Client client = new Telepathy.Client();
        protected Telepathy.Server server = new Telepathy.Server();

        void Awake()
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
        public event Action ClientConnected;
        public event Action<byte[]> ClientDataReceived;
        public event Action<Exception> ClientErrored;
        public event Action ClientDisconnected;
        bool paused = false;

        public bool IsClientConnected() { return client.Connected; }
        public void ClientConnect(string address) { client.Connect(address, port); }
        public bool ClientSend(int channelId, byte[] data) { return client.Send(data); }

        bool ProcessClientMessage()
        {
            Telepathy.Message message;
            if (client.GetNextMessage(out message))
            {
                switch (message.eventType)
                {
                    // convert Telepathy EventType to TransportEvent
                    case Telepathy.EventType.Connected:
                        if (ClientConnected != null)
                            ClientConnected();
                        break;
                    case Telepathy.EventType.Data:
                        if (ClientDataReceived != null)
                            ClientDataReceived(message.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        if (ClientDisconnected != null)
                            ClientDisconnected();
                        break;
                    default:
                        // TODO:  Telepathy does not report errors at all
                        // it just disconnects,  should be fixed
                        if (ClientDisconnected != null)
                            ClientDisconnected();
                        break;
                }
                return true;
            }
            return false;
        }

        public void ClientDisconnect() { client.Disconnect(); }

        public void Pause()
        {
            paused = true;
        }
        public void Resume()
        {
            paused = false;
        }

        public void Update()
        {
            // process all messages
            if (paused)
                return;

            while (ProcessClientMessage()) { }
            while (ProcessServerMessage()) { }
        }

        // server
        public event Action<int> ServerConnected;
        public event Action<int, byte[]> ServerDataReceived;
        public event Action<int, Exception> ServerErrored;
        public event Action<int> ServerDisconnected;

        public bool IsServerActive() { return server.Active; }
        public void ServerStart() { server.Start(port); }
        public bool ServerSend(int connectionId, int channelId, byte[] data) { return server.Send(connectionId, data); }
        public bool ProcessServerMessage()
        {
            Telepathy.Message message;
            if (server.GetNextMessage(out message))
            {
                switch (message.eventType)
                {
                    // convert Telepathy EventType to TransportEvent
                    case Telepathy.EventType.Connected:
                        if (ServerConnected != null)
                            ServerConnected(message.connectionId);
                        break;
                    case Telepathy.EventType.Data:
                        if (ServerDataReceived != null)
                            ServerDataReceived(message.connectionId, message.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        if (ServerDisconnected != null)
                            ServerDisconnected(message.connectionId);
                        break;
                    default:
                        // TODO handle errors from Telepathy when telepathy can report errors
                        if (ServerDisconnected != null)
                            ServerDisconnected(message.connectionId);
                        break;
                }
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(int connectionId)
        {
            return server.Disconnect(connectionId);
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

        public int GetMaxPacketSize(int channelId)
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
