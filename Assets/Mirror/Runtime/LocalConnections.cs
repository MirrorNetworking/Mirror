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

        internal override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            if (bytes.Length == 0)
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
