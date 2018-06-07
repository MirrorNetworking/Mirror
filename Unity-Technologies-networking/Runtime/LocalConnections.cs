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

        public override bool Send(short msgType, MessageBase msg)
        {
            m_LocalClient.InvokeHandlerOnClient(msgType, msg, Channels.DefaultReliable);
            return true;
        }

        public override bool SendUnreliable(short msgType, MessageBase msg)
        {
            m_LocalClient.InvokeHandlerOnClient(msgType, msg, Channels.DefaultUnreliable);
            return true;
        }

        public override bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            m_LocalClient.InvokeHandlerOnClient(msgType, msg, channelId);
            return true;
        }

        public override bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            m_LocalClient.InvokeBytesOnClient(bytes, channelId);
            return true;
        }

        public override bool SendWriter(NetworkWriter writer, int channelId)
        {
            m_LocalClient.InvokeBytesOnClient(writer.AsArray(), channelId);
            return true;
        }

        public override void GetStatsOut(out int numMsgs, out int numBufferedMsgs, out int numBytes, out int lastBufferedPerSecond)
        {
            numMsgs = 0;
            numBufferedMsgs = 0;
            numBytes = 0;
            lastBufferedPerSecond = 0;
        }

        public override void GetStatsIn(out int numMsgs, out int numBytes)
        {
            numMsgs = 0;
            numBytes = 0;
        }
    }

    // a localClient's connection TO a server.
    // send messages on this connection causes the server's
    // handler function to be invoked directly.

    internal class ULocalConnectionToServer : NetworkConnection
    {
        NetworkServer m_LocalServer;

        public ULocalConnectionToServer(NetworkServer localServer)
        {
            address = "localServer";
            m_LocalServer = localServer;
        }

        public override bool Send(short msgType, MessageBase msg)
        {
            return m_LocalServer.InvokeHandlerOnServer(this, msgType, msg, Channels.DefaultReliable);
        }

        public override bool SendUnreliable(short msgType, MessageBase msg)
        {
            return m_LocalServer.InvokeHandlerOnServer(this, msgType, msg, Channels.DefaultUnreliable);
        }

        public override bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            return m_LocalServer.InvokeHandlerOnServer(this, msgType, msg, channelId);
        }

        public override bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            if (numBytes <= 0)
            {
                if (LogFilter.logError) { Debug.LogError("LocalConnection:SendBytes cannot send zero bytes"); }
                return false;
            }
            return m_LocalServer.InvokeBytes(this, bytes, numBytes, channelId);
        }

        public override bool SendWriter(NetworkWriter writer, int channelId)
        {
            return m_LocalServer.InvokeBytes(this, writer.AsArray(), (short)writer.AsArray().Length, channelId);
        }

        public override void GetStatsOut(out int numMsgs, out int numBufferedMsgs, out int numBytes, out int lastBufferedPerSecond)
        {
            numMsgs = 0;
            numBufferedMsgs = 0;
            numBytes = 0;
            lastBufferedPerSecond = 0;
        }

        public override void GetStatsIn(out int numMsgs, out int numBytes)
        {
            numMsgs = 0;
            numBytes = 0;
        }
    }
}
#endif //ENABLE_UNET
