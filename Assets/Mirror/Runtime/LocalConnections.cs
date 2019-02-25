using System;
using UnityEngine;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    class ULocalConnectionToClient : NetworkConnection
    {
        public LocalClient localClient { get; }

        public ULocalConnectionToClient(LocalClient localClient) : base ("localClient")
        {
            this.localClient = localClient;
        }

        internal override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            localClient.InvokeBytesOnClient(bytes);
            return true;
        }
    }

    // a localClient's connection TO a server.
    // send messages on this connection causes the server's handler function to be invoked directly.
    internal class ULocalConnectionToServer : NetworkConnection
    {
        public ULocalConnectionToServer() : base("localServer")
        {
        }

        internal override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            if (bytes.Length == 0)
            {
                Debug.LogError("LocalConnection:SendBytes cannot send zero bytes");
                return false;
            }
            return NetworkServer.InvokeBytes(this, bytes);
        }
    }
}
