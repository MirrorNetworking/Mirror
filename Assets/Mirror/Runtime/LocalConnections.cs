using System;
using UnityEngine;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    class ULocalConnectionToClient : NetworkConnection
    {
        public ULocalConnectionToClient() : base ("localClient")
        {
            // local player always has connectionId == 0
            connectionId = 0;
        }

        public override bool Send<T>(T msg, int channelId = 0)
        {
            // TODO this can be done by queueing the msg itsef
            // instead of serializing and deserializing
            byte[] packet = MessagePacker.Pack(msg);
            NetworkClient.localClientPacketQueue.Enqueue(packet);
            return true;
        }

        [Obsolete]
        internal override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            NetworkClient.localClientPacketQueue.Enqueue(bytes);
            return true;
        }
    }

    // a localClient's connection TO a server.
    // send messages on this connection causes the server's handler function to be invoked directly.
    internal class ULocalConnectionToServer : NetworkConnection
    {
        public ULocalConnectionToServer() : base("localServer")
        {
            // local player always has connectionId == 0
            connectionId = 0;
        }

        public override bool Send<T>(T msg, int channelId = Channels.DefaultReliable)
        {
            // handle the server's message directly
            // TODO any way to do this without serializing the message?
            byte[] data = MessagePacker.Pack(msg);
            NetworkServer.localConnection.TransportReceive(new ArraySegment<byte>(data));
            return true;
        }

        [Obsolete]
        internal override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            if (bytes.Length == 0)
            {
                Debug.LogError("LocalConnection.SendBytes cannot send zero bytes");
                return false;
            }

            // handle the server's message directly
            // TODO any way to do this without NetworkServer.localConnection?
            NetworkServer.localConnection.TransportReceive(new ArraySegment<byte>(bytes));
            return true;
        }
    }
}
