using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mirror.Transports.Encryption
{
    public class EncryptionTransport : Transport
    {
        public Transport inner;


        private EncryptedConnection _client;

        private Dictionary<int, EncryptedConnection> _serverConnections = new Dictionary<int, EncryptedConnection>();

        private Dictionary<int, EncryptedConnection> _serverPendingConnections =
            new Dictionary<int, EncryptedConnection>();

        private EncryptionCredentials _credentials;

        private void Awake()
        {
            _credentials = EncryptionCredentials.Generate(); // todo
            if (!inner)
            {
                throw new Exception("inner Transport needs to be set before Awake is called.");
            }

            inner.OnClientConnected = HandleInnerClientConnected;
            inner.OnClientDataReceived = HandleInnerClientDataReceived;
            inner.OnClientDataSent = (bytes, channel) => OnClientDataSent?.Invoke(bytes, channel);
            inner.OnClientError = HandleInnerClientError;
            inner.OnClientDisconnected = HandleInnerClientDisconnected;

            inner.OnServerConnected = HandleInnerServerConnected;
            inner.OnServerDataReceived = HandleInnerServerDataReceived;
            inner.OnServerDataSent = (connId, bytes, channel) => OnServerDataSent?.Invoke(connId, bytes, channel);
            inner.OnServerError = HandleInnerServerError;
            inner.OnServerDisconnected = HandleInnerServerDisconnected;
        }

        private void HandleInnerServerDisconnected(int connId)
        {
            _serverPendingConnections.Remove(connId);
            _serverConnections.Remove(connId);
        }

        private void HandleInnerServerError(int connId, TransportError type, string msg)
        {
            OnServerError?.Invoke(connId, type, $"inner: {msg}");
        }

        private void HandleInnerServerDataReceived(int connId, ArraySegment<byte> data, int channel)
        {
            if (_serverConnections.TryGetValue(connId, out var c))
            {
                c.OnReceiveRaw(data, channel);
            }
        }

        private void HandleInnerServerConnected(int connId)
        {
            Debug.Log($"[EncryptionTransport] New connection #{connId}");
            var ec = new EncryptedConnection(
                _credentials,
                false,
                (segment, channel) => inner.ServerSend(connId, segment, channel),
                (segment, channel) => OnServerDataReceived?.Invoke(connId, segment, channel),
                () =>
                {
                    Debug.Log($"[EncryptionTransport] Connection #{connId} is ready");
                    _serverPendingConnections.Remove(connId);
                    OnServerConnected?.Invoke(connId);
                },
                (type, msg) =>
                {
                    OnServerError?.Invoke(connId, type, msg);
                    ServerDisconnect(connId);
                });
            _serverConnections.Add(connId, ec);
            _serverPendingConnections.Add(connId, ec);
        }

        private void HandleInnerClientDisconnected()
        {
            _client = null;
            OnClientDisconnected?.Invoke();
        }

        private void HandleInnerClientError(TransportError arg1, string arg2)
        {
            OnClientError?.Invoke(arg1, $"inner: {arg2}");
        }

        private void HandleInnerClientDataReceived(ArraySegment<byte> data, int channel)
        {
            _client?.OnReceiveRaw(data, channel);
        }

        private void HandleInnerClientConnected()
        {
            Debug.Log("Client inner connected");
            _client = new EncryptedConnection(
                _credentials,
                true,
                (segment, channel) => inner.ClientSend(segment, channel),
                (segment, channel) => OnClientDataReceived?.Invoke(segment, channel),
                () =>
                {
                    Debug.Log($"[EncryptionTransport] Client connection is ready");
                    OnClientConnected?.Invoke();
                },
                (type, msg) =>
                {
                    OnClientError?.Invoke(type, msg);
                    ClientDisconnect();
                });
        }


        public override bool Available() => inner.Available();

        public override bool ClientConnected() => _client != null && _client.IsReady;

        public override void ClientConnect(string address) => inner.ClientConnect(address);

        public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            _client?.Send(segment, channelId);

        public override void ClientDisconnect() => inner.ClientDisconnect();

        public override Uri ServerUri() => inner.ServerUri();

        public override bool ServerActive() => inner.ServerActive();

        public override void ServerStart() => inner.ServerStart();

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (_serverConnections.TryGetValue(connectionId, out var connection) && connection.IsReady)
            {
                connection.Send(segment, channelId);
            }
        }

        public override void ServerDisconnect(int connectionId)
        {
            // cleanup is done via inners disconnect event
            inner.ServerDisconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId) => inner.ServerGetClientAddress(connectionId);

        public override void ServerStop() => inner.ServerStop();

        public override int GetMaxPacketSize(int channelId = Channels.Reliable) =>
            inner.GetMaxPacketSize(channelId) - EncryptedConnection.Overhead;

        public override void Shutdown() => inner.Shutdown();

        public override void ClientEarlyUpdate()
        {
            inner.ClientEarlyUpdate();
        }

        public override void ClientLateUpdate()
        {
            inner.ClientLateUpdate();
            _client?.Tick(NetworkTime.localTime);
        }

        public override void ServerEarlyUpdate()
        {
            inner.ServerEarlyUpdate();
        }

        public override void ServerLateUpdate()
        {
            inner.ServerLateUpdate();
            // TODO: need to be able to remove while looping here since Tick might disconnect..
            // figure out a better solution to get rid of ToArray
            foreach (EncryptedConnection c in _serverPendingConnections.Values.ToArray())
            {
                c.Tick(NetworkTime.time);
            }
        }
    }
}
