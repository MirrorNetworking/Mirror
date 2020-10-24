using System;
using System.Collections.Generic;

namespace Mirror
{
    /// <summary>
    /// Allows Middleware to override some of the transport methods or let the inner transport handle them.
    /// </summary>
    public abstract class MiddlewareTransport : Transport
    {
        public Transport inner;

        public override bool Available()
        {
            return inner.Available();
        }
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return inner.GetMaxPacketSize(channelId);
        }
        public override void Shutdown()
        {
            inner.Shutdown();
        }

        #region Client
        public override void ClientConnect(string address)
        {
            inner.ClientConnect(address);
        }
        public override bool ClientConnected()
        {
            return inner.ClientConnected();
        }
        public override void ClientDisconnect()
        {
            inner.ClientDisconnect();
        }
        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            return inner.ClientSend(channelId, segment);
        }
        #endregion

        #region Server
        public override bool ServerActive()
        {
            return inner.ServerActive();
        }
        public override void ServerStart()
        {
            inner.ServerStart();
        }
        public override void ServerStop()
        {
            inner.ServerStop();
        }
        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            return inner.ServerSend(connectionIds, channelId, segment);
        }
        public override bool ServerDisconnect(int connectionId)
        {
            return inner.ServerDisconnect(connectionId);
        }
        public override string ServerGetClientAddress(int connectionId)
        {
            return inner.ServerGetClientAddress(connectionId);
        }
        public override Uri ServerUri()
        {
            return inner.ServerUri();
        }
        #endregion
    }
}
