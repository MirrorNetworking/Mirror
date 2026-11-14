using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Mirror.BouncyCastle.Crypto;
using Mirror.BouncyCastle.Crypto.Agreement;
using Mirror.BouncyCastle.Crypto.Digests;
using Mirror.BouncyCastle.Crypto.Generators;
using Mirror.BouncyCastle.Crypto.Modes;
using Mirror.BouncyCastle.Crypto.Parameters;
using UnityEngine.Profiling;

namespace Mirror.Transports.Encryption
{
    public class EncryptedConnection
    {
        // 256-bit key
        const int KeyLength = 32;
        // 512-bit salt for the key derivation function
        const int HkdfSaltSize = KeyLength * 2;

        // Info tag for the HKDF, this just adds more entropy
        static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("Mirror/EncryptionTransport");

        // fixed size of the unique per-packet nonce. Defaults to 12 bytes/96 bits (not recommended to be changed)
        const int NonceSize = 12;

        // this is the size of the "checksum" included in each encrypted payload
        // 16 bytes/128 bytes is the recommended value for best security
        // can be reduced to 12 bytes for a small space savings, but makes encryption slightly weaker.
        // Setting it lower than 12 bytes is not recommended
        const int MacSizeBytes = 16;

        const int MacSizeBits = MacSizeBytes * 8;

        // How much metadata overhead we have for regular packets
        public const int Overhead = sizeof(OpCodes) + MacSizeBytes + NonceSize;

        // After how many seconds of not receiving a handshake packet we should time out
        const double DurationTimeout = 2; // 2s

        // After how many seconds to assume the last handshake packet got lost and to resend another one
        const double DurationResend = 0.05; // 50ms


        // Static fields for allocation efficiency, makes this not thread safe
        // It'd be as easy as using ThreadLocal though to fix that

        // Set up a global cipher instance, it is initialised/reset before use
        // (AesFastEngine used to exist, but was removed due to side channel issues)
        // use AesUtilities.CreateEngine here as it'll pick the hardware accelerated one if available (which is will not be unless on .net core)
        static readonly ThreadLocal<GcmBlockCipher> Cipher = new ThreadLocal<GcmBlockCipher>(() => new GcmBlockCipher(AesUtilities.CreateEngine()));

        // Set up a global HKDF with a SHA-256 digest
        static readonly ThreadLocal<HkdfBytesGenerator> Hkdf = new ThreadLocal<HkdfBytesGenerator>(() => new HkdfBytesGenerator(new Sha256Digest()));

        // Global byte array to store nonce sent by the remote side, they're used immediately after
        static readonly ThreadLocal<byte[]> ReceiveNonce = new ThreadLocal<byte[]>(() => new byte[NonceSize]);

        // Buffer for the remote salt, as bouncycastle needs to take a byte[] *rolls eyes*
        static readonly ThreadLocal<byte[]> TMPRemoteSaltBuffer = new ThreadLocal<byte[]>(() => new byte[HkdfSaltSize]);

        // buffer for encrypt/decrypt operations, resized larger as needed
        static ThreadLocal<byte[]> TMPCryptBuffer = new ThreadLocal<byte[]>(() => new byte[2048]);

        // packet headers
        enum OpCodes : byte
        {
            // start at 1 to maybe filter out random noise
            Data = 1,
            HandshakeStart = 2,
            HandshakeAck = 3,
            HandshakeFin = 4
        }

        enum State
        {
            // Waiting for a handshake to arrive
            // this is for _sendsFirst:
            // - false: OpCodes.HandshakeStart
            // - true: Opcodes.HandshakeAck
            WaitingHandshake,

            // Waiting for a handshake reply/acknowledgement to arrive
            // this is for _sendsFirst:
            // - false: OpCodes.HandshakeFine
            // - true: Opcodes.Data (implicitly)
            WaitingHandshakeReply,

            // Both sides have confirmed the keys are exchanged and data can be sent freely
            Ready
        }

        State state = State.WaitingHandshake;

        // Key exchange confirmed and data can be sent freely
        public bool IsReady => state == State.Ready;
        // Callback to send off encrypted data
        readonly Action<ArraySegment<byte>, int> send;
        // Callback when received data has been decrypted
        readonly Action<ArraySegment<byte>, int> receive;
        // Callback when the connection becomes ready
        readonly Action ready;
        // On-error callback, disconnect expected
        readonly Action<TransportError, string> error;
        // Optional callback to validate the remotes public key, validation on one side is necessary to ensure MITM resistance
        // (usually client validates the server key)
        readonly Func<PubKeyInfo, bool> validateRemoteKey;
        // Our asymmetric credentials for the initial DH exchange
        EncryptionCredentials credentials;
        readonly byte[] hkdfSalt;
        NetworkReader _tmpReader = new NetworkReader(new ArraySegment<byte>());

        // After no handshake packet in this many seconds, the handshake fails
        double handshakeTimeout;
        // When to assume the last handshake packet got lost and to resend another one
        double nextHandshakeResend;


        // we can reuse the _cipherParameters here since the nonce is stored as the byte[] reference we pass in
        // so we can update it without creating a new AeadParameters instance
        // this might break in the future! (will cause bad data)
        byte[] nonce = new byte[NonceSize];
        AeadParameters cipherParametersEncrypt;
        AeadParameters cipherParametersDecrypt;


        /*
         * Specifies if we send the first key, then receive ack, then send fin
         * Or the opposite if set to false
         *
         * The client does this, since the fin is not acked explicitly, but by receiving data to decrypt
         */
        readonly bool sendsFirst;

        public EncryptedConnection(EncryptionCredentials credentials,
            bool isClient,
            Action<ArraySegment<byte>, int> sendAction,
            Action<ArraySegment<byte>, int> receiveAction,
            Action readyAction,
            Action<TransportError, string> errorAction,
            Func<PubKeyInfo, bool> validateRemoteKey = null)
        {
            this.credentials = credentials;
            sendsFirst = isClient;
            if (!sendsFirst)
                // salt is controlled by the server
                hkdfSalt = GenerateSecureBytes(HkdfSaltSize);
            send = sendAction;
            receive = receiveAction;
            ready = readyAction;
            error = errorAction;
            this.validateRemoteKey = validateRemoteKey;
        }

        // Generates a random starting nonce
        static byte[] GenerateSecureBytes(int size)
        {
            byte[] bytes = new byte[size];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            return bytes;
        }


        public void OnReceiveRaw(ArraySegment<byte> data, int channel)
        {
            if (data.Count < 1)
            {
                error(TransportError.Unexpected, "Received empty packet");
                return;
            }

            _tmpReader.SetBuffer(data);
            OpCodes opcode = (OpCodes)_tmpReader.ReadByte();
            switch (opcode)
            {
                case OpCodes.Data:
                    // first sender ready is implicit when data is received
                    if (sendsFirst && state == State.WaitingHandshakeReply)
                        SetReady();
                    else if (!IsReady)
                        error(TransportError.Unexpected, "Unexpected data while not ready.");

                    if (_tmpReader.Remaining < Overhead)
                    {
                        error(TransportError.Unexpected, "received data packet smaller than metadata size");
                        return;
                    }

                    ArraySegment<byte> ciphertext = _tmpReader.ReadBytesSegment(_tmpReader.Remaining - NonceSize);
                    _tmpReader.ReadBytes(ReceiveNonce.Value, NonceSize);

                    Profiler.BeginSample("EncryptedConnection.Decrypt");
                    ArraySegment<byte> plaintext = Decrypt(ciphertext);
                    Profiler.EndSample();
                    if (plaintext.Count == 0)
                        // error
                        return;
                    receive(plaintext, channel);
                    break;
                case OpCodes.HandshakeStart:
                    if (sendsFirst)
                    {
                        error(TransportError.Unexpected, "Received HandshakeStart packet, we don't expect this.");
                        return;
                    }

                    if (state == State.WaitingHandshakeReply)
                        // this is fine, packets may arrive out of order
                        return;

                    state = State.WaitingHandshakeReply;
                    ResetTimeouts();
                    CompleteExchange(_tmpReader.ReadBytesSegment(_tmpReader.Remaining), hkdfSalt);
                    SendHandshakeAndPubKey(OpCodes.HandshakeAck);
                    break;
                case OpCodes.HandshakeAck:
                    if (!sendsFirst)
                    {
                        error(TransportError.Unexpected, "Received HandshakeAck packet, we don't expect this.");
                        return;
                    }

                    if (IsReady)
                        // this is fine, packets may arrive out of order
                        return;

                    if (state == State.WaitingHandshakeReply)
                        // this is fine, packets may arrive out of order
                        return;


                    state = State.WaitingHandshakeReply;
                    ResetTimeouts();
                    _tmpReader.ReadBytes(TMPRemoteSaltBuffer.Value, HkdfSaltSize);
                    CompleteExchange(_tmpReader.ReadBytesSegment(_tmpReader.Remaining), TMPRemoteSaltBuffer.Value);
                    SendHandshakeFin();
                    break;
                case OpCodes.HandshakeFin:
                    if (sendsFirst)
                    {
                        error(TransportError.Unexpected, "Received HandshakeFin packet, we don't expect this.");
                        return;
                    }

                    if (IsReady)
                        // this is fine, packets may arrive out of order
                        return;

                    if (state != State.WaitingHandshakeReply)
                    {
                        error(TransportError.Unexpected,
                            "Received HandshakeFin packet, we didn't expect this yet.");
                        return;
                    }

                    SetReady();

                    break;
                default:
                    error(TransportError.InvalidReceive, $"Unhandled opcode {(byte)opcode:x}");
                    break;
            }
        }

        void SetReady()
        {
            // done with credentials, null out the reference
            credentials = null;

            state = State.Ready;
            ready();
        }

        void ResetTimeouts()
        {
            handshakeTimeout = 0;
            nextHandshakeResend = -1;
        }

        public void Send(ArraySegment<byte> data, int channel)
        {
            using (ConcurrentNetworkWriterPooled writer = ConcurrentNetworkWriterPool.Get())
            {
                writer.WriteByte((byte)OpCodes.Data);
                Profiler.BeginSample("EncryptedConnection.Encrypt");
                ArraySegment<byte> encrypted = Encrypt(data);
                Profiler.EndSample();

                if (encrypted.Count == 0)
                    // error
                    return;
                writer.WriteBytes(encrypted.Array, 0, encrypted.Count);
                // write nonce after since Encrypt will update it
                writer.WriteBytes(nonce, 0, NonceSize);
                send(writer.ToArraySegment(), channel);
            }
        }

        ArraySegment<byte> Encrypt(ArraySegment<byte> plaintext)
        {
            if (plaintext.Count == 0)
                // Invalid
                return new ArraySegment<byte>();
            // Need to make the nonce unique again before encrypting another message
            UpdateNonce();
            // Re-initialize the cipher with our cached parameters
            Cipher.Value.Init(true, cipherParametersEncrypt);

            // Calculate the expected output size, this should always be input size + mac size
            int outSize = Cipher.Value.GetOutputSize(plaintext.Count);
#if UNITY_EDITOR
            // expecting the outSize to be input size + MacSize
            if (outSize != plaintext.Count + MacSizeBytes)
                throw new Exception($"Encrypt: Unexpected output size (Expected {plaintext.Count + MacSizeBytes}, got {outSize}");
#endif
            // Resize the static buffer to fit
            byte[] cryptBuffer = TMPCryptBuffer.Value;
            EnsureSize(ref cryptBuffer, outSize);
            TMPCryptBuffer.Value = cryptBuffer;

            int resultLen;
            try
            {
                // Run the plain text through the cipher, ProcessBytes will only process full blocks
                resultLen =
                    Cipher.Value.ProcessBytes(plaintext.Array, plaintext.Offset, plaintext.Count, cryptBuffer, 0);
                // Then run any potentially remaining partial blocks through with DoFinal (and calculate the mac)
                resultLen += Cipher.Value.DoFinal(cryptBuffer, resultLen);
            }
            // catch all Exception's since BouncyCastle is fairly noisy with both standard and their own exception types
            //
            catch (Exception e)
            {
                error(TransportError.Unexpected, $"Unexpected exception while encrypting {e.GetType()}: {e.Message}");
                return new ArraySegment<byte>();
            }
#if UNITY_EDITOR
            // expecting the result length to match the previously calculated input size + MacSize
            if (resultLen != outSize)
                throw new Exception($"Encrypt: resultLen did not match outSize (expected {outSize}, got {resultLen})");
#endif
            return new ArraySegment<byte>(cryptBuffer, 0, resultLen);
        }

        ArraySegment<byte> Decrypt(ArraySegment<byte> ciphertext)
        {
            if (ciphertext.Count <= MacSizeBytes)
            {
                error(TransportError.Unexpected, $"Received too short data packet (min {{MacSizeBytes + 1}}, got {ciphertext.Count})");
                // Invalid
                return new ArraySegment<byte>();
            }
            // Re-initialize the cipher with our cached parameters
            Cipher.Value.Init(false, cipherParametersDecrypt);

            // Calculate the expected output size, this should always be input size - mac size
            int outSize = Cipher.Value.GetOutputSize(ciphertext.Count);
#if UNITY_EDITOR
            // expecting the outSize to be input size - MacSize
            if (outSize != ciphertext.Count - MacSizeBytes)
                throw new Exception($"Decrypt: Unexpected output size (Expected {ciphertext.Count - MacSizeBytes}, got {outSize}");
#endif

            byte[] cryptBuffer = TMPCryptBuffer.Value;
            EnsureSize(ref cryptBuffer, outSize);
            TMPCryptBuffer.Value = cryptBuffer;

            int resultLen;
            try
            {
                // Run the ciphertext through the cipher, ProcessBytes will only process full blocks
                resultLen =
                    Cipher.Value.ProcessBytes(ciphertext.Array, ciphertext.Offset, ciphertext.Count, cryptBuffer, 0);
                // Then run any potentially remaining partial blocks through with DoFinal (and calculate/check the mac)
                resultLen += Cipher.Value.DoFinal(cryptBuffer, resultLen);
            }
            // catch all Exception's since BouncyCastle is fairly noisy with both standard and their own exception types
            catch (Exception e)
            {
                error(TransportError.Unexpected, $"Unexpected exception while decrypting {e.GetType()}: {e.Message}. This usually signifies corrupt data");
                return new ArraySegment<byte>();
            }
#if UNITY_EDITOR
            // expecting the result length to match the previously calculated input size + MacSize
            if (resultLen != outSize)
                throw new Exception($"Decrypt: resultLen did not match outSize (expected {outSize}, got {resultLen})");
#endif
            return new ArraySegment<byte>(cryptBuffer, 0, resultLen);
        }

        void UpdateNonce()
        {
            // increment the nonce by one
            // we need to ensure the nonce is *always* unique and not reused
            // easiest way to do this is by simply incrementing it
            for (int i = 0; i < NonceSize; i++)
            {
                nonce[i]++;
                if (nonce[i] != 0)
                    break;
            }
        }

        static void EnsureSize(ref byte[] buffer, int size)
        {
            if (buffer.Length < size)
                // double buffer to avoid constantly resizing by a few bytes
                Array.Resize(ref buffer, Math.Max(size, buffer.Length * 2));
        }

        void SendHandshakeAndPubKey(OpCodes opcode)
        {
            using (ConcurrentNetworkWriterPooled writer = ConcurrentNetworkWriterPool.Get())
            {
                writer.WriteByte((byte)opcode);
                if (opcode == OpCodes.HandshakeAck)
                    writer.WriteBytes(hkdfSalt, 0, HkdfSaltSize);
                writer.WriteBytes(credentials.PublicKeySerialized, 0, credentials.PublicKeySerialized.Length);
                send(writer.ToArraySegment(), Channels.Unreliable);
            }
        }

        void SendHandshakeFin()
        {
            using (ConcurrentNetworkWriterPooled writer = ConcurrentNetworkWriterPool.Get())
            {
                writer.WriteByte((byte)OpCodes.HandshakeFin);
                send(writer.ToArraySegment(), Channels.Unreliable);
            }
        }

        void CompleteExchange(ArraySegment<byte> remotePubKeyRaw, byte[] salt)
        {
            AsymmetricKeyParameter remotePubKey;
            try
            {
                remotePubKey = EncryptionCredentials.DeserializePublicKey(remotePubKeyRaw);
            }
            catch (Exception e)
            {
                error(TransportError.Unexpected, $"Failed to deserialize public key of remote. {e.GetType()}: {e.Message}");
                return;
            }

            if (validateRemoteKey != null)
            {
                PubKeyInfo info = new PubKeyInfo
                {
                    Fingerprint = EncryptionCredentials.PubKeyFingerprint(remotePubKeyRaw),
                    Serialized = remotePubKeyRaw,
                    Key = remotePubKey
                };
                if (!validateRemoteKey(info))
                {
                    error(TransportError.Unexpected, $"Remote public key (fingerprint: {info.Fingerprint}) failed validation. ");
                    return;
                }
            }

            // Calculate a common symmetric key from our private key and the remotes public key
            // This gives us the same key on the other side, with our public key and their remote
            // It's like magic, but with math!
            ECDHBasicAgreement ecdh = new ECDHBasicAgreement();
            ecdh.Init(credentials.PrivateKey);
            byte[] sharedSecret;
            try
            {
                sharedSecret = ecdh.CalculateAgreement(remotePubKey).ToByteArrayUnsigned();
            }
            catch
                (Exception e)
            {
                error(TransportError.Unexpected, $"Failed to calculate the ECDH key exchange. {e.GetType()}: {e.Message}");
                return;
            }

            if (salt.Length != HkdfSaltSize)
            {
                error(TransportError.Unexpected, $"Salt is expected to be {HkdfSaltSize} bytes long, got {salt.Length}.");
                return;
            }

            Hkdf.Value.Init(new HkdfParameters(sharedSecret, salt, HkdfInfo));

            // Allocate a buffer for the output key
            byte[] keyRaw = new byte[KeyLength];

            // Generate the output keying material
            Hkdf.Value.GenerateBytes(keyRaw, 0, keyRaw.Length);

            KeyParameter key = new KeyParameter(keyRaw);

            // generate a starting nonce
            nonce = GenerateSecureBytes(NonceSize);

            // we pass in the nonce array once (as it's stored by reference) so we can cache the AeadParameters instance
            // instead of creating a new one each encrypt/decrypt
            cipherParametersEncrypt = new AeadParameters(key, MacSizeBits, nonce);
            cipherParametersDecrypt = new AeadParameters(key, MacSizeBits, ReceiveNonce.Value);
        }

        /**
         * non-ready connections need to be ticked for resending key data over unreliable
         */
        public void TickNonReady(double time)
        {
            if (IsReady)
                return;

            // Timeout reset
            if (handshakeTimeout == 0)
                handshakeTimeout = time + DurationTimeout;
            else if (time > handshakeTimeout)
            {
                error?.Invoke(TransportError.Timeout, $"Timed out during {state}, this probably just means the other side went away which is fine.");
                return;
            }

            // Timeout reset
            if (nextHandshakeResend < 0)
            {
                nextHandshakeResend = time + DurationResend;
                return;
            }

            if (time < nextHandshakeResend)
                // Resend isn't due yet
                return;

            nextHandshakeResend = time + DurationResend;
            switch (state)
            {
                case State.WaitingHandshake:
                    if (sendsFirst)
                        SendHandshakeAndPubKey(OpCodes.HandshakeStart);

                    break;
                case State.WaitingHandshakeReply:
                    if (sendsFirst)
                        SendHandshakeFin();
                    else
                        SendHandshakeAndPubKey(OpCodes.HandshakeAck);

                    break;
                case State.Ready: // IsReady is checked above & early-returned
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
