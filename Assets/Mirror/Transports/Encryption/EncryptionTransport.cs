using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

namespace Mirror.Transports.Encryption
{
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport")]
    public class EncryptionTransport : Transport
    {
        public override bool IsEncrypted => true;
        public override string EncryptionCipher => "AES256-GCM";
        public Transport inner;

        public enum ValidationMode
        {
            Off,
            List,
            Callback,
        }

        public ValidationMode clientValidateServerPubKey;
        [Tooltip("List of public key fingerprints the client will accept")]
        public string[] clientTrustedPubKeySignatures;
        public Func<PubKeyInfo, bool> onClientValidateServerPubKey;
        public bool serverLoadKeyPairFromFile;
        public string serverKeypairPath = "./server-keys.json";

        private EncryptedConnection _client;

        private Dictionary<int, EncryptedConnection> _serverConnections = new Dictionary<int, EncryptedConnection>();

        private List<EncryptedConnection> _serverPendingConnections =
            new List<EncryptedConnection>();

        private EncryptionCredentials _credentials;
        public string EncryptionPublicKeyFingerprint => _credentials?.PublicKeyFingerprint;
        public byte[] EncryptionPublicKey => _credentials?.PublicKeySerialized;

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
            if (_serverConnections.TryGetValue(connId, out EncryptedConnection c))
            {
                c.OnReceiveRaw(data, channel);
            }
        }

        private void HandleInnerServerConnected(int connId) => HandleInnerServerConnected(connId, inner.ServerGetClientAddress(connId));

        private void HandleInnerServerConnected(int connId, string clientRemoteAddress)
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
                    //OnServerConnected?.Invoke(connId);
                    OnServerConnectedWithAddress?.Invoke(connId, clientRemoteAddress);
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
                },
                HandleClientValidateServerPubKey);
        }

        private bool HandleClientValidateServerPubKey(PubKeyInfo pubKeyInfo)
        {
            switch (clientValidateServerPubKey)
            {
                case ValidationMode.Off:
                    return true;
                case ValidationMode.List:
                    return Array.IndexOf(clientTrustedPubKeySignatures, pubKeyInfo.Fingerprint) >= 0;
                case ValidationMode.Callback:
                    return onClientValidateServerPubKey(pubKeyInfo);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override bool Available() => inner.Available();

        public override bool ClientConnected() => _client != null && _client.IsReady;

        public override void ClientConnect(string address)
        {
            switch (clientValidateServerPubKey)
            {
                case ValidationMode.Off:
                    break;
                case ValidationMode.List:
                    if (clientTrustedPubKeySignatures == null || clientTrustedPubKeySignatures.Length == 0)
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, "Validate Server Public Key is set to List, but the clientTrustedPubKeySignatures list is empty.");
                        return;
                    }
                    break;
                case ValidationMode.Callback:
                    if (onClientValidateServerPubKey == null)
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, "Validate Server Public Key is set to Callback, but the onClientValidateServerPubKey handler is not set");
                        return;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _credentials = EncryptionCredentials.Generate();
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
            if (serverLoadKeyPairFromFile)
            {
                _credentials = EncryptionCredentials.LoadFromFile(serverKeypairPath);
            }
            else
            {
                _credentials = EncryptionCredentials.Generate();
            }
#pragma warning disable CS0618 // Type or member is obsolete
            inner.OnServerConnected = HandleInnerServerConnected;
#pragma warning restore CS0618 // Type or member is obsolete
            inner.OnServerConnectedWithAddress = HandleInnerServerConnected;
            inner.OnServerDataReceived = HandleInnerServerDataReceived;
            inner.OnServerDataSent = (connId, bytes, channel) => OnServerDataSent?.Invoke(connId, bytes, channel);
            inner.OnServerError = HandleInnerServerError;
            inner.OnServerDisconnected = HandleInnerServerDisconnected;
            inner.ServerStart();
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (_serverConnections.TryGetValue(connectionId, out EncryptedConnection connection) && connection.IsReady)
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
