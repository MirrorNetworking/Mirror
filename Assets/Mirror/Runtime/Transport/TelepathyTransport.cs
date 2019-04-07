// wraps Telepathy for use as HLAPI TransportLayer
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror
{
    [HelpURL("https://github.com/vis2k/Telepathy/blob/master/README.md")]
    public class TelepathyTransport : Transport
    {
        public ushort port = 7777;

        [Tooltip("Nagle Algorithm can be disabled by enabling NoDelay")]
        public bool NoDelay = true;

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use MaxMessageSizeFromClient or MaxMessageSizeFromServer instead.")]
        public int MaxMessageSize
        {
            get => serverMaxMessageSize;
            set => serverMaxMessageSize = clientMaxMessageSize = value;
        }

        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker might send multiple fake packets with 2GB headers, causing the server to run out of memory after allocating multiple large packets.")]
        [FormerlySerializedAs("MaxMessageSize")] public int serverMaxMessageSize = 16 * 1024;

        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker host might send multiple fake packets with 2GB headers, causing the connected clients to run out of memory after allocating multiple large packets.")]
        [FormerlySerializedAs("MaxMessageSize")] public int clientMaxMessageSize = 16 * 1024;

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
            client.MaxMessageSize = clientMaxMessageSize;
            server.NoDelay = NoDelay;
            server.MaxMessageSize = serverMaxMessageSize;

            // HLAPI's local connection uses hard coded connectionId '0', so we
            // need to make sure that external connections always start at '1'
            // by simple eating the first one before the server starts
            Telepathy.Server.NextConnectionId();

            Debug.Log("TelepathyTransport initialized!");
        }

        // client
        public override bool ClientConnected() => client.Connected;
        public override void ClientConnect(string address) => client.Connect(address, port);
        public override bool ClientSend(int channelId, byte[] data) => client.Send(data);

        bool ProcessClientMessage()
        {
            if (client.GetNextMessage(out Telepathy.Message message))
            {
                switch (message.eventType)
                {
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
        public override void ClientDisconnect() => client.Disconnect();

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            while (enabled && ProcessClientMessage()) {}
            while (enabled && ProcessServerMessage()) {}
        }

        // server
        public override bool ServerActive() => server.Active;
        public override void ServerStart() => server.Start(port);
        public override bool ServerSend(int connectionId, int channelId, byte[] data) => server.Send(connectionId, data);
        public bool ProcessServerMessage()
        {
            if (server.GetNextMessage(out Telepathy.Message message))
            {
                switch (message.eventType)
                {
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
        public override bool ServerDisconnect(int connectionId) => server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);
        public override void ServerStop() => server.Stop();

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
