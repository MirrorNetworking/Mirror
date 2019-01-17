using System;
using UnityEngine;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    class ULocalConnectionToClient : NetworkConnection
    {
        LocalClient m_LocalClient;

        public LocalClient localClient { get {  return m_LocalClient; } }

        public ULocalConnectionToClient(LocalClient localClient) : base ("localClient")
        {
            m_LocalClient = localClient;
        }

        protected override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            m_LocalClient.InvokeBytesOnClient(bytes);
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

        protected override bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
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
