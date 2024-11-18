using System;
using System.Collections.Generic;
using Mirror.BouncyCastle.Crypto;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

namespace Mirror.Transports.Encryption
{
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport")]
    public class EncryptionTransport : Transport, PortTransport
    {
        public override bool IsEncrypted => true;
        public override string EncryptionCipher => "AES256-GCM";
        [FormerlySerializedAs("inner")]
        [HideInInspector]
        public Transport Inner;

        public ushort Port
        {
            get
            {
                if (Inner is PortTransport portTransport)
                    return portTransport.Port;

                Debug.LogError($"EncryptionTransport can't get Port because {Inner} is not a PortTransport");
                return 0;
            }
            set
            {
                if (Inner is PortTransport portTransport)
                {
                    portTransport.Port = value;
                    return;
                }
                Debug.LogError($"EncryptionTransport can't set Port because {Inner} is not a PortTransport");
            }
        }

        public enum ValidationMode
        {
            Off,
            List,
            Callback
        }

        [FormerlySerializedAs("clientValidateServerPubKey")]
        [HideInInspector]
        public ValidationMode ClientValidateServerPubKey;
        [FormerlySerializedAs("clientTrustedPubKeySignatures")]
        [HideInInspector]
        [Tooltip("List of public key fingerprints the client will accept")]
        public string[] ClientTrustedPubKeySignatures;
        public Func<PubKeyInfo, bool> OnClientValidateServerPubKey;
        [FormerlySerializedAs("serverLoadKeyPairFromFile")]
        [HideInInspector]
        public bool ServerLoadKeyPairFromFile;
        [FormerlySerializedAs("serverKeypairPath")]
        [HideInInspector]
        public string ServerKeypairPath = "./server-keys.json";

        EncryptedConnection client;

        readonly Dictionary<int, EncryptedConnection> serverConnections = new Dictionary<int, EncryptedConnection>();

        readonly List<EncryptedConnection> serverPendingConnections =
            new List<EncryptedConnection>();

        EncryptionCredentials credentials;
        public string EncryptionPublicKeyFingerprint => credentials?.PublicKeyFingerprint;
        public byte[] EncryptionPublicKey => credentials?.PublicKeySerialized;

        void ServerRemoveFromPending(EncryptedConnection con)
        {
            for (int i = 0; i < serverPendingConnections.Count; i++)
                if (serverPendingConnections[i] == con)
                {
                    // remove by swapping with last
                    int lastIndex = serverPendingConnections.Count - 1;
                    serverPendingConnections[i] = serverPendingConnections[lastIndex];
                    serverPendingConnections.RemoveAt(lastIndex);
                    break;
                }
        }

        void HandleInnerServerDisconnected(int connId)
        {
            if (serverConnections.TryGetValue(connId, out EncryptedConnection con))
            {
                ServerRemoveFromPending(con);
                serverConnections.Remove(connId);
            }
            OnServerDisconnected?.Invoke(connId);
        }

        void HandleInnerServerError(int connId, TransportError type, string msg) => OnServerError?.Invoke(connId, type, $"inner: {msg}");

        void HandleInnerServerDataReceived(int connId, ArraySegment<byte> data, int channel)
        {
            if (serverConnections.TryGetValue(connId, out EncryptedConnection c))
                c.OnReceiveRaw(data, channel);
        }

        void HandleInnerServerConnected(int connId) => HandleInnerServerConnected(connId, Inner.ServerGetClientAddress(connId));

        void HandleInnerServerConnected(int connId, string clientRemoteAddress)
        {
            Debug.Log($"[EncryptionTransport] New connection #{connId} from {clientRemoteAddress}");
            EncryptedConnection ec = null;
            ec = new EncryptedConnection(
                credentials,
                false,
                (segment, channel) => Inner.ServerSend(connId, segment, channel),
                (segment, channel) => OnServerDataReceived?.Invoke(connId, segment, channel),
                () =>
                {
                    Debug.Log($"[EncryptionTransport] Connection #{connId} is ready");
                    // ReSharper disable once AccessToModifiedClosure
                    ServerRemoveFromPending(ec);
                    OnServerConnectedWithAddress?.Invoke(connId, clientRemoteAddress);
                },
                (type, msg) =>
                {
                    OnServerError?.Invoke(connId, type, msg);
                    ServerDisconnect(connId);
                });
            serverConnections.Add(connId, ec);
            serverPendingConnections.Add(ec);
        }

        void HandleInnerClientDisconnected()
        {
            client = null;
            OnClientDisconnected?.Invoke();
        }

        void HandleInnerClientError(TransportError arg1, string arg2) => OnClientError?.Invoke(arg1, $"inner: {arg2}");

        void HandleInnerClientDataReceived(ArraySegment<byte> data, int channel) => client?.OnReceiveRaw(data, channel);

        void HandleInnerClientConnected() =>
            client = new EncryptedConnection(
                credentials,
                true,
                (segment, channel) => Inner.ClientSend(segment, channel),
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

        bool HandleClientValidateServerPubKey(PubKeyInfo pubKeyInfo)
        {
            switch (ClientValidateServerPubKey)
            {
                case ValidationMode.Off:
                    return true;
                case ValidationMode.List:
                    return Array.IndexOf(ClientTrustedPubKeySignatures, pubKeyInfo.Fingerprint) >= 0;
                case ValidationMode.Callback:
                    return OnClientValidateServerPubKey(pubKeyInfo);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void Awake() =>
            // check if encryption via hardware acceleration is supported.
            // this can be useful to know for low end devices.
            //
            // hardware acceleration requires netcoreapp3.0 or later:
            //   https://github.com/bcgit/bc-csharp/blob/449940429c57686a6fcf6bfbb4d368dec19d906e/crypto/src/crypto/AesUtilities.cs#L18
            // because AesEngine_x86 requires System.Runtime.Intrinsics.X86:
            //   https://github.com/bcgit/bc-csharp/blob/449940429c57686a6fcf6bfbb4d368dec19d906e/crypto/src/crypto/engines/AesEngine_X86.cs
            // which Unity does not support yet.
            Debug.Log($"EncryptionTransport: IsHardwareAccelerated={AesUtilities.IsHardwareAccelerated}");

        public override bool Available() => Inner.Available();

        public override bool ClientConnected() => client != null && client.IsReady;

        public override void ClientConnect(string address)
        {
            switch (ClientValidateServerPubKey)
            {
                case ValidationMode.Off:
                    break;
                case ValidationMode.List:
                    if (ClientTrustedPubKeySignatures == null || ClientTrustedPubKeySignatures.Length == 0)
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, "Validate Server Public Key is set to List, but the clientTrustedPubKeySignatures list is empty.");
                        return;
                    }
                    break;
                case ValidationMode.Callback:
                    if (OnClientValidateServerPubKey == null)
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, "Validate Server Public Key is set to Callback, but the onClientValidateServerPubKey handler is not set");
                        return;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            credentials = EncryptionCredentials.Generate();
            Inner.OnClientConnected = HandleInnerClientConnected;
            Inner.OnClientDataReceived = HandleInnerClientDataReceived;
            Inner.OnClientDataSent = (bytes, channel) => OnClientDataSent?.Invoke(bytes, channel);
            Inner.OnClientError = HandleInnerClientError;
            Inner.OnClientDisconnected = HandleInnerClientDisconnected;
            Inner.ClientConnect(address);
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            client?.Send(segment, channelId);

        public override void ClientDisconnect() => Inner.ClientDisconnect();

        public override Uri ServerUri() => Inner.ServerUri();

        public override bool ServerActive() => Inner.ServerActive();

        public override void ServerStart()
        {
            if (ServerLoadKeyPairFromFile)
                credentials = EncryptionCredentials.LoadFromFile(ServerKeypairPath);
            else
                credentials = EncryptionCredentials.Generate();
#pragma warning disable CS0618 // Type or member is obsolete
            Inner.OnServerConnected = HandleInnerServerConnected;
#pragma warning restore CS0618 // Type or member is obsolete
            Inner.OnServerConnectedWithAddress = HandleInnerServerConnected;
            Inner.OnServerDataReceived = HandleInnerServerDataReceived;
            Inner.OnServerDataSent = (connId, bytes, channel) => OnServerDataSent?.Invoke(connId, bytes, channel);
            Inner.OnServerError = HandleInnerServerError;
            Inner.OnServerDisconnected = HandleInnerServerDisconnected;
            Inner.ServerStart();
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (serverConnections.TryGetValue(connectionId, out EncryptedConnection connection) && connection.IsReady)
                connection.Send(segment, channelId);
        }

        public override void ServerDisconnect(int connectionId) =>
            // cleanup is done via inners disconnect event
            Inner.ServerDisconnect(connectionId);

        public override string ServerGetClientAddress(int connectionId) => Inner.ServerGetClientAddress(connectionId);

        public override void ServerStop() => Inner.ServerStop();

        public override int GetMaxPacketSize(int channelId = Channels.Reliable) =>
            Inner.GetMaxPacketSize(channelId) - EncryptedConnection.Overhead;

        public override int GetBatchThreshold(int channelId = Channels.Reliable) => Inner.GetBatchThreshold(channelId) - EncryptedConnection.Overhead;

        public override void Shutdown() => Inner.Shutdown();

        public override void ClientEarlyUpdate() => Inner.ClientEarlyUpdate();

        public override void ClientLateUpdate()
        {
            Inner.ClientLateUpdate();
            Profiler.BeginSample("EncryptionTransport.ServerLateUpdate");
            client?.TickNonReady(NetworkTime.localTime);
            Profiler.EndSample();
        }

        public override void ServerEarlyUpdate() => Inner.ServerEarlyUpdate();

        public override void ServerLateUpdate()
        {
            Inner.ServerLateUpdate();
            Profiler.BeginSample("EncryptionTransport.ServerLateUpdate");
            // Reverse iteration as entries can be removed while updating
            for (int i = serverPendingConnections.Count - 1; i >= 0; i--)
                serverPendingConnections[i].TickNonReady(NetworkTime.time);
            Profiler.EndSample();
        }
    }
}
