using System;

#if ENABLE_UNET

namespace UnityEngine.Networking
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's
    // handler function to be invoked directly
    class ULocalConnectionToClient : NetworkConnection
    {
        LocalClient m_LocalClient;

        public LocalClient localClient { get {  return m_LocalClient; } }

        public ULocalConnectionToClient(LocalClient localClient)
        {
            address = "localClient";
            m_LocalClient = localClient;
        }

        public override bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            m_LocalClient.InvokeHandlerOnClient(msgType, msg, channelId);
            return true;
        }
        public override bool Send(short msgType, MessageBase msg) { return SendByChannel(msgType, msg, Channels.DefaultReliable); }
        public override bool SendUnreliable(short msgType, MessageBase msg) { return SendByChannel(msgType, msg, Channels.DefaultUnreliable); }

        public override bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            m_LocalClient.InvokeBytesOnClient(bytes, channelId);
            return true;
        }

        public override bool SendWriter(NetworkWriter writer, int channelId)
        {
            m_LocalClient.InvokeBytesOnClient(writer.ToArray(), channelId);
            return true;
        }
    }

    // a localClient's connection TO a server.
    // send messages on this connection causes the server's
    // handler function to be invoked directly.

    internal class ULocalConnectionToServer : NetworkConnection
    {
        public ULocalConnectionToServer()
        {
            address = "localServer";
        }

        public override bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            return NetworkServer.InvokeHandlerOnServer(this, msgType, msg, channelId);
        }
        public override bool Send(short msgType, MessageBase msg) { return SendByChannel(msgType, msg, Channels.DefaultReliable); }
        public override bool SendUnreliable(short msgType, MessageBase msg) { return SendByChannel(msgType, msg, Channels.DefaultUnreliable); }

        public override bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            if (numBytes <= 0)
            {
                if (LogFilter.logError) { Debug.LogError("LocalConnection:SendBytes cannot send zero bytes"); }
                return false;
            }
            return NetworkServer.InvokeBytes(this, bytes, numBytes, channelId);
        }

        public override bool SendWriter(NetworkWriter writer, int channelId)
        {
            // write relevant data, which is until .Position
            return NetworkServer.InvokeBytes(this, writer.ToArray(), (short)writer.Position, channelId);
        }
    }
}
#endif //ENABLE_UNET
