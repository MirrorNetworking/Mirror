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

        internal override bool SendBytes(ArraySegment<byte> bytes, int channelId = Channels.DefaultReliable)
        {
            // must hold on to the data, so copy into a byte[]
            byte[] data = new byte[bytes.Count];
            Array.Copy(bytes.Array, bytes.Offset, data, 0, bytes.Count);

            NetworkClient.localClientPacketQueue.Enqueue(data);
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

        internal override bool SendBytes(ArraySegment<byte> bytes, int channelId = Channels.DefaultReliable)
        {
            if (bytes.Count == 0)
            {
                Debug.LogError("LocalConnection.SendBytes cannot send zero bytes");
                return false;
            }

            // handle the server's message directly
            // TODO any way to do this without NetworkServer.localConnection?
            NetworkServer.localConnection.TransportReceive(bytes);
            return true;
        }
    }
}
