using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Mirror.BouncyCastle.Crypto;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Mirror.Transports.Encryption
{
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport")]
    public class ThreadedEncryptionTransport : ThreadedTransport, PortTransport
    {
        public override bool IsEncrypted => true;
        public override string EncryptionCipher => "AES256-GCM";
        [FormerlySerializedAs("inner")]
        public ThreadedTransport Inner;

        public ushort Port
        {
            get
            {
                if (Inner is PortTransport portTransport)
                    return portTransport.Port;

                Debug.LogError($"ThreadedEncryptionTransport can't get Port because {Inner} is not a PortTransport");
                return 0;
            }
            set
            {
                if (Inner is PortTransport portTransport)
                {
                    portTransport.Port = value;
                    return;
                }
                Debug.LogError($"ThreadedEncryptionTransport can't set Port because {Inner} is not a PortTransport");
            }
        }

        public enum ValidationMode
        {
            Off,
            List,
            Callback
        }

        [FormerlySerializedAs("clientValidateServerPubKey")]
        public ValidationMode ClientValidateServerPubKey;
        [FormerlySerializedAs("clientTrustedPubKeySignatures")]
        [Tooltip("List of public key fingerprints the client will accept")]
        public string[] ClientTrustedPubKeySignatures;
        /// <summary>
        /// Called when a client connects to a server
        /// ATTENTION: NOT THREAD SAFE.
        /// This will be called on the worker thread.
        /// </summary>
        public Func<PubKeyInfo, bool> OnClientValidateServerPubKey;
        [FormerlySerializedAs("serverLoadKeyPairFromFile")]
        public bool ServerLoadKeyPairFromFile;
        [FormerlySerializedAs("serverKeypairPath")]
        public string ServerKeypairPath = "./server-keys.json";

        EncryptedConnection client;

        readonly Dictionary<int, EncryptedConnection> serverConnections = new Dictionary<int, EncryptedConnection>();

        readonly List<EncryptedConnection> serverPendingConnections =
            new List<EncryptedConnection>();

        EncryptionCredentials credentials;
        public string EncryptionPublicKeyFingerprint => credentials?.PublicKeyFingerprint;
        public byte[] EncryptionPublicKey => credentials?.PublicKeySerialized;

        // Used for threaded time keeping as unitys Time.time is not thread safe
        Stopwatch stopwatch = Stopwatch.StartNew();

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
            OnThreadedServerDisconnected(connId);
        }

        void HandleInnerServerError(int connId, TransportError type, string msg) => OnThreadedServerError(connId, type, $"inner: {msg}");

        void HandleInnerServerDataReceived(int connId, ArraySegment<byte> data, int channel)
        {
            if (serverConnections.TryGetValue(connId, out EncryptedConnection c))
                c.OnReceiveRaw(data, channel);
        }

        void HandleInnerServerConnected(int connId) => HandleInnerServerConnected(connId, Inner.ServerGetClientAddress(connId));

        void HandleInnerServerConnected(int connId, string clientRemoteAddress)
        {
            Debug.Log($"[ThreadedEncryptionTransport] New connection #{connId} from {clientRemoteAddress}");
            EncryptedConnection ec = null;
            ec = new EncryptedConnection(
                credentials,
                false,
                (segment, channel) => Inner.ServerSend(connId, segment, channel),
                (segment, channel) => OnThreadedServerReceive(connId, segment, channel),
                () =>
                {
                    Debug.Log($"[ThreadedEncryptionTransport] Connection #{connId} is ready");
                    // ReSharper disable once AccessToModifiedClosure
                    ServerRemoveFromPending(ec);
                    OnThreadedServerConnected(connId, new IPEndPoint(IPAddress.Parse(clientRemoteAddress), 0));
                },
                (type, msg) =>
                {
                    OnThreadedServerError(connId, type, msg);
                    ServerDisconnect(connId);
                });
            serverConnections.Add(connId, ec);
            serverPendingConnections.Add(ec);
        }

        void HandleInnerClientDisconnected()
        {
            client = null;
            OnThreadedClientDisconnected();
        }

        void HandleInnerClientError(TransportError arg1, string arg2) => OnThreadedClientError(arg1, $"inner: {arg2}");

        void HandleInnerClientDataReceived(ArraySegment<byte> data, int channel) => client?.OnReceiveRaw(data, channel);

        void HandleInnerClientConnected() =>
            client = new EncryptedConnection(
                credentials,
                true,
                (segment, channel) => Inner.ClientSend(segment, channel),
                (segment, channel) => OnThreadedClientReceive(segment, channel),
                () =>
                {
                    OnThreadedClientConnected();
                },
                (type, msg) =>
                {
                    OnThreadedClientError(type, msg);
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

        protected override void Awake()
        {
            base.Awake();
            // check if encryption via hardware acceleration is supported.
            // this can be useful to know for low end devices.
            //
            // hardware acceleration requires netcoreapp3.0 or later:
            //   https://github.com/bcgit/bc-csharp/blob/449940429c57686a6fcf6bfbb4d368dec19d906e/crypto/src/crypto/AesUtilities.cs#L18
            // because AesEngine_x86 requires System.Runtime.Intrinsics.X86:
            //   https://github.com/bcgit/bc-csharp/blob/449940429c57686a6fcf6bfbb4d368dec19d906e/crypto/src/crypto/engines/AesEngine_X86.cs
            // which Unity does not support yet.
            Debug.Log($"ThreadedEncryptionTransport: IsHardwareAccelerated={AesUtilities.IsHardwareAccelerated}");
        }

        public override bool Available() => Inner.Available();

        protected override void ThreadedClientConnect(string address)
        {
            switch (ClientValidateServerPubKey)
            {
                case ValidationMode.Off:
                    break;
                case ValidationMode.List:
                    if (ClientTrustedPubKeySignatures == null || ClientTrustedPubKeySignatures.Length == 0)
                    {
                        OnThreadedClientError(TransportError.Unexpected, "Validate Server Public Key is set to List, but the clientTrustedPubKeySignatures list is empty.");
                        return;
                    }
                    break;
                case ValidationMode.Callback:
                    if (OnClientValidateServerPubKey == null)
                    {
                        OnThreadedClientError(TransportError.Unexpected, "Validate Server Public Key is set to Callback, but the onClientValidateServerPubKey handler is not set");
                        return;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            credentials = EncryptionCredentials.Generate();
            Inner.OnClientConnected = HandleInnerClientConnected;
            Inner.OnClientDataReceived = HandleInnerClientDataReceived;
            Inner.OnClientDataSent = (bytes, channel) => OnThreadedClientSend(bytes, channel);
            Inner.OnClientError = HandleInnerClientError;
            Inner.OnClientDisconnected = HandleInnerClientDisconnected;
            Inner.ClientConnect(address);
        }

        protected override void ThreadedClientConnect(Uri address) => Inner.ClientConnect(address);

        protected override void ThreadedClientSend(ArraySegment<byte> segment, int channelId) =>
            client?.Send(segment, channelId);

        protected override void ThreadedClientDisconnect() => Inner.ClientDisconnect();

        protected override void ThreadedServerStart()
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
            Inner.OnServerDataSent = (connId, bytes, channel) => OnThreadedServerSend(connId, bytes, channel);
            Inner.OnServerError = HandleInnerServerError;
            Inner.OnServerDisconnected = HandleInnerServerDisconnected;
            Inner.ServerStart();
        }

        protected override void ThreadedServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (serverConnections.TryGetValue(connectionId, out EncryptedConnection connection) && connection.IsReady)
                connection.Send(segment, channelId);
        }

        protected override void ThreadedServerDisconnect(int connectionId) =>
            // cleanup is done via inners disconnect event
            Inner.ServerDisconnect(connectionId);

        protected override void ThreadedClientEarlyUpdate() {}

        protected override void ThreadedServerStop() => Inner.ServerStop();

        public override Uri ServerUri() => Inner.ServerUri();

        public override int GetMaxPacketSize(int channelId = Channels.Reliable) => Inner.GetMaxPacketSize(channelId) - EncryptedConnection.Overhead;

        protected override void ThreadedShutdown() => Inner.Shutdown();

        public override void ClientEarlyUpdate()
        {
            base.ClientEarlyUpdate();
            Inner.ClientEarlyUpdate();
        }

        public override void ClientLateUpdate()
        {
            base.ClientLateUpdate();
            Inner.ClientLateUpdate();
        }

        protected override void ThreadedClientLateUpdate()
        {
            Profiler.BeginSample("ThreadedEncryptionTransport.ServerLateUpdate");
            client?.TickNonReady(stopwatch.Elapsed.TotalSeconds);
            Profiler.EndSample();
        }

        protected override void ThreadedServerEarlyUpdate() {}

        public override void ServerEarlyUpdate()
        {
            base.ServerEarlyUpdate();
            Inner.ServerEarlyUpdate();
        }

        public override void ServerLateUpdate()
        {
            base.ServerLateUpdate();
            Inner.ServerLateUpdate();
        }

        protected override void ThreadedServerLateUpdate()
        {
            Profiler.BeginSample("ThreadedEncryptionTransport.ServerLateUpdate");
            // Reverse iteration as entries can be removed while updating
            for (int i = serverPendingConnections.Count - 1; i >= 0; i--)
                serverPendingConnections[i].TickNonReady(stopwatch.Elapsed.TotalSeconds);
            Profiler.EndSample();
        }
    }
}
