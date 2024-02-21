using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace Mirror.Transports.Encryption
{
    public class EncryptionTransport : Transport
    {
        public Transport inner;


        private EncryptedConnection _client;

        private Dictionary<int, EncryptedConnection> _serverConnections = new Dictionary<int, EncryptedConnection>();

        private List<EncryptedConnection> _serverPendingConnections =
            new List<EncryptedConnection>();

        private EncryptionCredentials _credentials;

        void Awake()
        {
            _credentials = EncryptionCredentials.Generate(); // todo
        }

        private void ServerRemoveFromPending(EncryptedConnection con)
        {
            for (int i = 0; i < _serverPendingConnections.Count; i++)
            {
                if (_serverPendingConnections[i] == con)
                {
                    // remove by swapping with last
                    int lastIndex = _serverPendingConnections.Count - 1;
                    _serverPendingConnections[i] = _serverPendingConnections[lastIndex];
                    _serverPendingConnections.RemoveAt(lastIndex);
                    break;
                }
            }
        }

        private void HandleInnerServerDisconnected(int connId)
        {
            if (_serverConnections.TryGetValue(connId, out EncryptedConnection con))
            {
                ServerRemoveFromPending(con);
                _serverConnections.Remove(connId);
            }
            OnServerDisconnected?.Invoke(connId);
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
            EncryptedConnection ec = null;
            ec = new EncryptedConnection(
                _credentials,
                false,
                (segment, channel) => inner.ServerSend(connId, segment, channel),
                (segment, channel) => OnServerDataReceived?.Invoke(connId, segment, channel),
                () =>
                {
                    Debug.Log($"[EncryptionTransport] Connection #{connId} is ready");
                    ServerRemoveFromPending(ec);
                    OnServerConnected?.Invoke(connId);
                },
                (type, msg) =>
                {
                    OnServerError?.Invoke(connId, type, msg);
                    ServerDisconnect(connId);
                });
            _serverConnections.Add(connId, ec);
            _serverPendingConnections.Add(ec);
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
            _client = new EncryptedConnection(
                _credentials,
                true,
                (segment, channel) => inner.ClientSend(segment, channel),
                (segment, channel) => OnClientDataReceived?.Invoke(segment, channel),
                () =>
                {
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

        public override void ClientConnect(string address)
        {
            inner.OnClientConnected = HandleInnerClientConnected;
            inner.OnClientDataReceived = HandleInnerClientDataReceived;
            inner.OnClientDataSent = (bytes, channel) => OnClientDataSent?.Invoke(bytes, channel);
            inner.OnClientError = HandleInnerClientError;
            inner.OnClientDisconnected = HandleInnerClientDisconnected;
            inner.ClientConnect(address);
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            _client?.Send(segment, channelId);

        public override void ClientDisconnect() => inner.ClientDisconnect();

        public override Uri ServerUri() => inner.ServerUri();

        public override bool ServerActive() => inner.ServerActive();

        public override void ServerStart()
        {
            inner.OnServerConnected = HandleInnerServerConnected;
            inner.OnServerDataReceived = HandleInnerServerDataReceived;
            inner.OnServerDataSent = (connId, bytes, channel) => OnServerDataSent?.Invoke(connId, bytes, channel);
            inner.OnServerError = HandleInnerServerError;
            inner.OnServerDisconnected = HandleInnerServerDisconnected;
            inner.ServerStart();
        }

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
            Profiler.BeginSample("EncryptionTransport.ServerLateUpdate");
            _client?.TickNonReady(NetworkTime.localTime);
            Profiler.EndSample();
        }

        public override void ServerEarlyUpdate()
        {
            inner.ServerEarlyUpdate();
        }

        public override void ServerLateUpdate()
        {
            inner.ServerLateUpdate();
            Profiler.BeginSample("EncryptionTransport.ServerLateUpdate");
            // Reverse iteration as entries can be removed while updating
            for (int i = _serverPendingConnections.Count - 1; i >= 0; i--)
            {
                _serverPendingConnections[i].TickNonReady(NetworkTime.time);
            }
            Profiler.EndSample();
        }
    }
}
