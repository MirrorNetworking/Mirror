using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using kcp2k;
using Mirror.BouncyCastle.Crypto;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Mirror.Transports.Encryption
{
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport")]
    public class ThreadedEncryptionKcpTransport : ThreadedKcpTransport
    {
        public override bool IsEncrypted => true;
        public override string EncryptionCipher => "AES256-GCM";
        public override string ToString() => $"Encrypted {base.ToString()}";

        public enum ValidationMode
        {
            Off,
            List,
            Callback
        }

        [HideInInspector]
        public ValidationMode ClientValidateServerPubKey;

        [Tooltip("List of public key fingerprints the client will accept")]
        [HideInInspector]
        public string[] ClientTrustedPubKeySignatures;
        /// <summary>
        /// Called when a client connects to a server
        /// ATTENTION: NOT THREAD SAFE.
        /// This will be called on the worker thread.
        /// </summary>
        public Func<PubKeyInfo, bool> OnClientValidateServerPubKey;
        [HideInInspector]
        [FormerlySerializedAs("serverLoadKeyPairFromFile")]
        public bool ServerLoadKeyPairFromFile;
        [HideInInspector]
        [FormerlySerializedAs("serverKeypairPath")]
        public string ServerKeypairPath = "./server-keys.json";

        EncryptedConnection encryptedClient;

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

        void HandleInnerServerDataReceived(int connId, ArraySegment<byte> data, int channel)
        {
            if (serverConnections.TryGetValue(connId, out EncryptedConnection c))
                c.OnReceiveRaw(data, channel);
        }


        void HandleInnerServerConnected(int connId, IPEndPoint clientRemoteAddress)
        {
            Debug.Log($"[ThreadedEncryptionKcpTransport] New connection #{connId} from {clientRemoteAddress}");
            EncryptedConnection ec = null;
            ec = new EncryptedConnection(
                credentials,
                false,
                (segment, channel) =>
                {
                    server.Send(connId, segment, KcpTransport.ToKcpChannel(channel));
                    OnThreadedServerSend(connId, segment,channel);
                },
                (segment, channel) => OnThreadedServerReceive(connId, segment, channel),
                () =>
                {
                    Debug.Log($"[ThreadedEncryptionKcpTransport] Connection #{connId} is ready");
                    // ReSharper disable once AccessToModifiedClosure
                    ServerRemoveFromPending(ec);
                    OnThreadedServerConnected(connId, clientRemoteAddress);
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
            encryptedClient = null;
            OnThreadedClientDisconnected();
        }

        void HandleInnerClientDataReceived(ArraySegment<byte> data, int channel) => encryptedClient?.OnReceiveRaw(data, channel);

        void HandleInnerClientConnected() =>
            encryptedClient = new EncryptedConnection(
                credentials,
                true,
                (segment, channel) =>
                {
                    client.Send(segment, KcpTransport.ToKcpChannel(channel));
                    OnThreadedClientSend(segment, channel);
                },
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
            // client (NonAlloc version is not necessary anymore)
            client = new KcpClient(
                HandleInnerClientConnected,
                (message, channel) => HandleInnerClientDataReceived(message, KcpTransport.FromKcpChannel(channel)),
                HandleInnerClientDisconnected,
                (error, reason) => OnThreadedClientError(KcpTransport.ToTransportError(error), reason),
                config
            );

            // server
            server = new KcpServer(
                HandleInnerServerConnected,
                (connectionId, message, channel) => HandleInnerServerDataReceived(connectionId, message, KcpTransport.FromKcpChannel(channel)),
                HandleInnerServerDisconnected,
                (connectionId, error, reason) => OnThreadedServerError(connectionId, KcpTransport.ToTransportError(error), reason),
                config
            );
            // check if encryption via hardware acceleration is supported.
            // this can be useful to know for low end devices.
            //
            // hardware acceleration requires netcoreapp3.0 or later:
            //   https://github.com/bcgit/bc-csharp/blob/449940429c57686a6fcf6bfbb4d368dec19d906e/crypto/src/crypto/AesUtilities.cs#L18
            // because AesEngine_x86 requires System.Runtime.Intrinsics.X86:
            //   https://github.com/bcgit/bc-csharp/blob/449940429c57686a6fcf6bfbb4d368dec19d906e/crypto/src/crypto/engines/AesEngine_X86.cs
            // which Unity does not support yet.
            Debug.Log($"ThreadedEncryptionKcpTransport: IsHardwareAccelerated={AesUtilities.IsHardwareAccelerated}");
        }

        protected override void ThreadedClientConnect(string address)
        {
            if (!SetupEncryptionForClient())
                return;
            base.ThreadedClientConnect(address);
        }

        bool SetupEncryptionForClient()
        {

            switch (ClientValidateServerPubKey)
            {
                case ValidationMode.Off:
                    break;
                case ValidationMode.List:
                    if (ClientTrustedPubKeySignatures == null || ClientTrustedPubKeySignatures.Length == 0)
                    {
                        OnThreadedClientError(TransportError.Unexpected, "Validate Server Public Key is set to List, but the clientTrustedPubKeySignatures list is empty.");
                        return false;
                    }
                    break;
                case ValidationMode.Callback:
                    if (OnClientValidateServerPubKey == null)
                    {
                        OnThreadedClientError(TransportError.Unexpected, "Validate Server Public Key is set to Callback, but the onClientValidateServerPubKey handler is not set");
                        return false;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            credentials = EncryptionCredentials.Generate();
            return true;
        }

        protected override void ThreadedClientConnect(Uri address)
        {
            if (!SetupEncryptionForClient())
                return;
            base.ThreadedClientConnect(address);
        }

        protected override void ThreadedClientSend(ArraySegment<byte> segment, int channelId)
        {
            encryptedClient?.Send(segment, channelId);
        }

        protected override void ThreadedServerStart()
        {
            if (ServerLoadKeyPairFromFile)
                credentials = EncryptionCredentials.LoadFromFile(ServerKeypairPath);
            else
                credentials = EncryptionCredentials.Generate();
            base.ThreadedServerStart();
        }

        protected override void ThreadedServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (serverConnections.TryGetValue(connectionId, out EncryptedConnection connection) && connection.IsReady)
                connection.Send(segment, channelId);
        }


        public override int GetMaxPacketSize(int channelId = Channels.Reliable) => base.GetMaxPacketSize(channelId) - EncryptedConnection.Overhead;
        public override int GetBatchThreshold(int channelId) => base.GetBatchThreshold(channelId) - EncryptedConnection.Overhead;

        protected override void ThreadedClientLateUpdate()
        {
            base.ThreadedClientLateUpdate();
            Profiler.BeginSample("ThreadedEncryptionKcpTransport.ServerLateUpdate");
            encryptedClient?.TickNonReady(stopwatch.Elapsed.TotalSeconds);
            Profiler.EndSample();
        }



        protected override void ThreadedServerLateUpdate()
        {
            base.ThreadedServerLateUpdate();
            Profiler.BeginSample("ThreadedEncryptionKcpTransport.ServerLateUpdate");
            // Reverse iteration as entries can be removed while updating
            for (int i = serverPendingConnections.Count - 1; i >= 0; i--)
                serverPendingConnections[i].TickNonReady(stopwatch.Elapsed.TotalSeconds);
            Profiler.EndSample();
        }
    }
}
