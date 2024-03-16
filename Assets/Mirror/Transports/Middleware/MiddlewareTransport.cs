using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Allows Middleware to override some of the transport methods or let the inner transport handle them.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MiddlewareTransport : Transport
    {
        /// <summary>
        /// Transport to call to after middleware
        /// </summary>
        public Transport inner;

        public override bool Available() => inner.Available();
        public override int GetMaxPacketSize(int channelId = 0) => inner.GetMaxPacketSize(channelId);
        public override int GetBatchThreshold(int channelId = Channels.Reliable) => inner.GetBatchThreshold(channelId);
        public override void Shutdown() => inner.Shutdown();

        #region Client
        public override void ClientConnect(string address)
        {
            inner.OnClientConnected = OnClientConnected;
            inner.OnClientDataReceived = OnClientDataReceived;
            inner.OnClientDisconnected = OnClientDisconnected;
            inner.OnClientError = OnClientError;
            inner.OnClientTransportException = OnClientTransportException;
            inner.ClientConnect(address);
        }

        public override bool ClientConnected() => inner.ClientConnected();
        public override void ClientDisconnect() => inner.ClientDisconnect();
        public override void ClientSend(ArraySegment<byte> segment, int channelId) => inner.ClientSend(segment, channelId);

        public override void ClientEarlyUpdate() => inner.ClientEarlyUpdate();
        public override void ClientLateUpdate() => inner.ClientLateUpdate();
        #endregion

        #region Server
        public override bool ServerActive() => inner.ServerActive();
        public override void ServerStart()
        {
            inner.OnServerConnected = OnServerConnected;
            inner.OnServerDataReceived = OnServerDataReceived;
            inner.OnServerDisconnected = OnServerDisconnected;
            inner.OnServerError = OnServerError;
            inner.OnServerTransportException = OnServerTransportException;
            inner.ServerStart();
        }

        public override void ServerStop() => inner.ServerStop();
        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId) => inner.ServerSend(connectionId, segment, channelId);
        public override void ServerDisconnect(int connectionId) => inner.ServerDisconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId) => inner.ServerGetClientAddress(connectionId);
        public override Uri ServerUri() => inner.ServerUri();

        public override void ServerEarlyUpdate() => inner.ServerEarlyUpdate();
        public override void ServerLateUpdate() => inner.ServerLateUpdate();
        #endregion
    }
}
