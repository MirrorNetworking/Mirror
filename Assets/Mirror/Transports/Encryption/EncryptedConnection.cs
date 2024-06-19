using System;
using System.Security.Cryptography;
using System.Text;
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
        private const int KeyLength = 32;
        // 512-bit salt for the key derivation function
        private const int HkdfSaltSize = KeyLength * 2;

        // Info tag for the HKDF, this just adds more entropy
        private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("Mirror/EncryptionTransport");

        // fixed size of the unique per-packet nonce. Defaults to 12 bytes/96 bits (not recommended to be changed)
        private const int NonceSize = 12;

        // this is the size of the "checksum" included in each encrypted payload
        // 16 bytes/128 bytes is the recommended value for best security
        // can be reduced to 12 bytes for a small space savings, but makes encryption slightly weaker.
        // Setting it lower than 12 bytes is not recommended
        private const int MacSizeBytes = 16;

        private const int MacSizeBits = MacSizeBytes * 8;

        // How much metadata overhead we have for regular packets
        public const int Overhead = sizeof(OpCodes) + MacSizeBytes + NonceSize;

        // After how many seconds of not receiving a handshake packet we should time out
        private const double DurationTimeout = 2; // 2s

        // After how many seconds to assume the last handshake packet got lost and to resend another one
        private const double DurationResend = 0.05; // 50ms


        // Static fields for allocation efficiency, makes this not thread safe
        // It'd be as easy as using ThreadLocal though to fix that

        // Set up a global cipher instance, it is initialised/reset before use
        // (AesFastEngine used to exist, but was removed due to side channel issues)
        // use AesUtilities.CreateEngine here as it'll pick the hardware accelerated one if available (which is will not be unless on .net core)
        private static readonly GcmBlockCipher Cipher = new GcmBlockCipher(AesUtilities.CreateEngine());

        // Set up a global HKDF with a SHA-256 digest
        private static readonly HkdfBytesGenerator Hkdf = new HkdfBytesGenerator(new Sha256Digest());

        // Global byte array to store nonce sent by the remote side, they're used immediately after
        private static readonly byte[] ReceiveNonce = new byte[NonceSize];

        // Buffer for the remote salt, as bouncycastle needs to take a byte[] *rolls eyes*
        private static byte[] _tmpRemoteSaltBuffer = new byte[HkdfSaltSize];
        // buffer for encrypt/decrypt operations, resized larger as needed
        // this is also the buffer that will be returned to mirror via ArraySegment
        // so any thread safety concerns would need to take extra care here
        private static byte[] _tmpCryptBuffer = new byte[2048];

        // packet headers
        enum OpCodes : byte
        {
            // start at 1 to maybe filter out random noise
            Data = 1,
            HandshakeStart = 2,
            HandshakeAck = 3,
            HandshakeFin = 4,
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

        private State _state = State.WaitingHandshake;

        // Key exchange confirmed and data can be sent freely
        public bool IsReady => _state == State.Ready;
        // Callback to send off encrypted data
        private Action<ArraySegment<byte>, int> _send;
        // Callback when received data has been decrypted
        private Action<ArraySegment<byte>, int> _receive;
        // Callback when the connection becomes ready
        private Action _ready;
        // On-error callback, disconnect expected
        private Action<TransportError, string> _error;
        // Optional callback to validate the remotes public key, validation on one side is necessary to ensure MITM resistance
        // (usually client validates the server key)
        private Func<PubKeyInfo, bool> _validateRemoteKey;
        // Our asymmetric credentials for the initial DH exchange
        private EncryptionCredentials _credentials;
        private byte[] _hkdfSalt;

        // After no handshake packet in this many seconds, the handshake fails
        private double _handshakeTimeout;
        // When to assume the last handshake packet got lost and to resend another one
        private double _nextHandshakeResend;


        // we can reuse the _cipherParameters here since the nonce is stored as the byte[] reference we pass in
        // so we can update it without creating a new AeadParameters instance
        // this might break in the future! (will cause bad data)
        private byte[] _nonce = new byte[NonceSize];
        private AeadParameters _cipherParametersEncrypt;
        private AeadParameters _cipherParametersDecrypt;


        /*
         * Specifies if we send the first key, then receive ack, then send fin
         * Or the opposite if set to false
         *
         * The client does this, since the fin is not acked explicitly, but by receiving data to decrypt
         */
        private readonly bool _sendsFirst;

        public EncryptedConnection(EncryptionCredentials credentials,
            bool isClient,
            Action<ArraySegment<byte>, int> sendAction,
            Action<ArraySegment<byte>, int> receiveAction,
            Action readyAction,
            Action<TransportError, string> errorAction,
            Func<PubKeyInfo, bool> validateRemoteKey = null)
        {
            _credentials = credentials;
            _sendsFirst = isClient;
            if (!_sendsFirst)
            {
                // salt is controlled by the server
                _hkdfSalt = GenerateSecureBytes(HkdfSaltSize);
            }
            _send = sendAction;
            _receive = receiveAction;
            _ready = readyAction;
            _error = errorAction;
            _validateRemoteKey = validateRemoteKey;
        }

        // Generates a random starting nonce
        private static byte[] GenerateSecureBytes(int size)
        {
            byte[] bytes = new byte[size];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return bytes;
        }

        public void OnReceiveRaw(ArraySegment<byte> data, int channel)
        {
            if (data.Count < 1)
            {
                _error(TransportError.Unexpected, "Received empty packet");
                return;
            }

            using (NetworkReaderPooled reader = NetworkReaderPool.Get(data))
            {
                OpCodes opcode = (OpCodes)reader.ReadByte();
                switch (opcode)
                {
                    case OpCodes.Data:
                        // first sender ready is implicit when data is received
                        if (_sendsFirst && _state == State.WaitingHandshakeReply)
                        {
                            SetReady();
                        }
                        else if (!IsReady)
                        {
                            _error(TransportError.Unexpected, "Unexpected data while not ready.");
                        }

                        if (reader.Remaining < Overhead)
                        {
                            _error(TransportError.Unexpected, "received data packet smaller than metadata size");
                            return;
                        }

                        ArraySegment<byte> ciphertext = reader.ReadBytesSegment(reader.Remaining - NonceSize);
                        reader.ReadBytes(ReceiveNonce, NonceSize);

                        Profiler.BeginSample("EncryptedConnection.Decrypt");
                        ArraySegment<byte> plaintext = Decrypt(ciphertext);
                        Profiler.EndSample();
                        if (plaintext.Count == 0)
                        {
                            // error
                            return;
                        }
                        _receive(plaintext, channel);
                        break;
                    case OpCodes.HandshakeStart:
                        if (_sendsFirst)
                        {
                            _error(TransportError.Unexpected, "Received HandshakeStart packet, we don't expect this.");
                            return;
                        }

                        if (_state == State.WaitingHandshakeReply)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }

                        _state = State.WaitingHandshakeReply;
                        ResetTimeouts();
                        CompleteExchange(reader.ReadBytesSegment(reader.Remaining), _hkdfSalt);
                        SendHandshakeAndPubKey(OpCodes.HandshakeAck);
                        break;
                    case OpCodes.HandshakeAck:
                        if (!_sendsFirst)
                        {
                            _error(TransportError.Unexpected, "Received HandshakeAck packet, we don't expect this.");
                            return;
                        }

                        if (IsReady)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }

                        if (_state == State.WaitingHandshakeReply)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }


                        _state = State.WaitingHandshakeReply;
                        ResetTimeouts();
                        reader.ReadBytes(_tmpRemoteSaltBuffer, HkdfSaltSize);
                        CompleteExchange(reader.ReadBytesSegment(reader.Remaining), _tmpRemoteSaltBuffer);
                        SendHandshakeFin();
                        break;
                    case OpCodes.HandshakeFin:
                        if (_sendsFirst)
                        {
                            _error(TransportError.Unexpected, "Received HandshakeFin packet, we don't expect this.");
                            return;
                        }

                        if (IsReady)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }

                        if (_state != State.WaitingHandshakeReply)
                        {
                            _error(TransportError.Unexpected,
                                "Received HandshakeFin packet, we didn't expect this yet.");
                            return;
                        }

                        SetReady();

                        break;
                    default:
                        _error(TransportError.InvalidReceive, $"Unhandled opcode {(byte)opcode:x}");
                        break;
                }
            }
        }
        private void SetReady()
        {
            // done with credentials, null out the reference
            _credentials = null;

            _state = State.Ready;
            _ready();
        }

        private void ResetTimeouts()
        {
            _handshakeTimeout = 0;
            _nextHandshakeResend = -1;
        }

        public void Send(ArraySegment<byte> data, int channel)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.WriteByte((byte)OpCodes.Data);
                Profiler.BeginSample("EncryptedConnection.Encrypt");
                ArraySegment<byte> encrypted = Encrypt(data);
                Profiler.EndSample();

                if (encrypted.Count == 0)
                {
                    // error
                    return;
                }
                writer.WriteBytes(encrypted.Array, 0, encrypted.Count);
                // write nonce after since Encrypt will update it
                writer.WriteBytes(_nonce, 0, NonceSize);
                _send(writer.ToArraySegment(), channel);
            }
        }

        private ArraySegment<byte> Encrypt(ArraySegment<byte> plaintext)
        {
            if (plaintext.Count == 0)
            {
                // Invalid
                return new ArraySegment<byte>();
            }
            // Need to make the nonce unique again before encrypting another message
            UpdateNonce();
            // Re-initialize the cipher with our cached parameters
            Cipher.Init(true, _cipherParametersEncrypt);

            // Calculate the expected output size, this should always be input size + mac size
            int outSize = Cipher.GetOutputSize(plaintext.Count);
#if UNITY_EDITOR
            // expecting the outSize to be input size + MacSize
            if (outSize != plaintext.Count + MacSizeBytes)
            {
                throw new Exception($"Encrypt: Unexpected output size (Expected {plaintext.Count + MacSizeBytes}, got {outSize}");
            }
#endif
            // Resize the static buffer to fit
            EnsureSize(ref _tmpCryptBuffer, outSize);
            int resultLen;
            try
            {
                // Run the plain text through the cipher, ProcessBytes will only process full blocks
                resultLen =
                    Cipher.ProcessBytes(plaintext.Array, plaintext.Offset, plaintext.Count, _tmpCryptBuffer, 0);
                // Then run any potentially remaining partial blocks through with DoFinal (and calculate the mac)
                resultLen += Cipher.DoFinal(_tmpCryptBuffer, resultLen);
            }
            // catch all Exception's since BouncyCastle is fairly noisy with both standard and their own exception types
            //
            catch (Exception e)
            {
                _error(TransportError.Unexpected, $"Unexpected exception while encrypting {e.GetType()}: {e.Message}");
                return new ArraySegment<byte>();
            }
#if UNITY_EDITOR
            // expecting the result length to match the previously calculated input size + MacSize
            if (resultLen != outSize)
            {
                throw new Exception($"Encrypt: resultLen did not match outSize (expected {outSize}, got {resultLen})");
            }
#endif
            return new ArraySegment<byte>(_tmpCryptBuffer, 0, resultLen);
        }

        private ArraySegment<byte> Decrypt(ArraySegment<byte> ciphertext)
        {
            if (ciphertext.Count <= MacSizeBytes)
            {
                _error(TransportError.Unexpected, $"Received too short data packet (min {{MacSizeBytes + 1}}, got {ciphertext.Count})");
                // Invalid
                return new ArraySegment<byte>();
            }
            // Re-initialize the cipher with our cached parameters
            Cipher.Init(false, _cipherParametersDecrypt);

            // Calculate the expected output size, this should always be input size - mac size
            int outSize = Cipher.GetOutputSize(ciphertext.Count);
#if UNITY_EDITOR
            // expecting the outSize to be input size - MacSize
            if (outSize != ciphertext.Count - MacSizeBytes)
            {
                throw new Exception($"Decrypt: Unexpected output size (Expected {ciphertext.Count - MacSizeBytes}, got {outSize}");
            }
#endif
            // Resize the static buffer to fit
            EnsureSize(ref _tmpCryptBuffer, outSize);
            int resultLen;
            try
            {
                // Run the ciphertext through the cipher, ProcessBytes will only process full blocks
                resultLen =
                    Cipher.ProcessBytes(ciphertext.Array, ciphertext.Offset, ciphertext.Count, _tmpCryptBuffer, 0);
                // Then run any potentially remaining partial blocks through with DoFinal (and calculate/check the mac)
                resultLen += Cipher.DoFinal(_tmpCryptBuffer, resultLen);
            }
            // catch all Exception's since BouncyCastle is fairly noisy with both standard and their own exception types
            catch (Exception e)
            {
                _error(TransportError.Unexpected, $"Unexpected exception while decrypting {e.GetType()}: {e.Message}. This usually signifies corrupt data");
                return new ArraySegment<byte>();
            }
#if UNITY_EDITOR
            // expecting the result length to match the previously calculated input size + MacSize
            if (resultLen != outSize)
            {
                throw new Exception($"Decrypt: resultLen did not match outSize (expected {outSize}, got {resultLen})");
            }
#endif
            return new ArraySegment<byte>(_tmpCryptBuffer, 0, resultLen);
        }

        private void UpdateNonce()
        {
            // increment the nonce by one
            // we need to ensure the nonce is *always* unique and not reused
            // easiest way to do this is by simply incrementing it
            for (int i = 0; i < NonceSize; i++)
            {
                _nonce[i]++;
                if (_nonce[i] != 0)
                {
                    break;
                }
            }
        }

        private static void EnsureSize(ref byte[] buffer, int size)
        {
            if (buffer.Length < size)
            {
                // double buffer to avoid constantly resizing by a few bytes
                Array.Resize(ref buffer, Math.Max(size, buffer.Length * 2));
            }
        }

        private void SendHandshakeAndPubKey(OpCodes opcode)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.WriteByte((byte)opcode);
                if (opcode == OpCodes.HandshakeAck)
                {
                    writer.WriteBytes(_hkdfSalt, 0, HkdfSaltSize);
                }
                writer.WriteBytes(_credentials.PublicKeySerialized, 0, _credentials.PublicKeySerialized.Length);
                _send(writer.ToArraySegment(), Channels.Unreliable);
            }
        }

        private void SendHandshakeFin()
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.WriteByte((byte)OpCodes.HandshakeFin);
                _send(writer.ToArraySegment(), Channels.Unreliable);
            }
        }

        private void CompleteExchange(ArraySegment<byte> remotePubKeyRaw, byte[] salt)
        {
            AsymmetricKeyParameter remotePubKey;
            try
            {
                remotePubKey = EncryptionCredentials.DeserializePublicKey(remotePubKeyRaw);
            }
            catch (Exception e)
            {
                _error(TransportError.Unexpected, $"Failed to deserialize public key of remote. {e.GetType()}: {e.Message}");
                return;
            }

            if (_validateRemoteKey != null)
            {
                PubKeyInfo info = new PubKeyInfo
                {
                    Fingerprint = EncryptionCredentials.PubKeyFingerprint(remotePubKeyRaw),
                    Serialized = remotePubKeyRaw,
                    Key = remotePubKey
                };
                if (!_validateRemoteKey(info))
                {
                    _error(TransportError.Unexpected, $"Remote public key (fingerprint: {info.Fingerprint}) failed validation. ");
                    return;
                }
            }

            // Calculate a common symmetric key from our private key and the remotes public key
            // This gives us the same key on the other side, with our public key and their remote
            // It's like magic, but with math!
            ECDHBasicAgreement ecdh = new ECDHBasicAgreement();
            ecdh.Init(_credentials.PrivateKey);
            byte[] sharedSecret;
            try
            {
                sharedSecret = ecdh.CalculateAgreement(remotePubKey).ToByteArrayUnsigned();
            }
            catch
                (Exception e)
            {
                _error(TransportError.Unexpected, $"Failed to calculate the ECDH key exchange. {e.GetType()}: {e.Message}");
                return;
            }

            if (salt.Length != HkdfSaltSize)
            {
                _error(TransportError.Unexpected, $"Salt is expected to be {HkdfSaltSize} bytes long, got {salt.Length}.");
                return;
            }

            Hkdf.Init(new HkdfParameters(sharedSecret, salt, HkdfInfo));

            // Allocate a buffer for the output key
            byte[] keyRaw = new byte[KeyLength];

            // Generate the output keying material
            Hkdf.GenerateBytes(keyRaw, 0, keyRaw.Length);

            KeyParameter key = new KeyParameter(keyRaw);

            // generate a starting nonce
            _nonce = GenerateSecureBytes(NonceSize);

            // we pass in the nonce array once (as it's stored by reference) so we can cache the AeadParameters instance
            // instead of creating a new one each encrypt/decrypt
            _cipherParametersEncrypt = new AeadParameters(key, MacSizeBits, _nonce);
            _cipherParametersDecrypt = new AeadParameters(key, MacSizeBits, ReceiveNonce);
        }

        /**
         * non-ready connections need to be ticked for resending key data over unreliable
         */
        public void TickNonReady(double time)
        {
            if (IsReady)
            {
                return;
            }

            // Timeout reset
            if (_handshakeTimeout == 0)
            {
                _handshakeTimeout = time + DurationTimeout;
            }
            else if (time > _handshakeTimeout)
            {
                _error?.Invoke(TransportError.Timeout, $"Timed out during {_state}, this probably just means the other side went away which is fine.");
                return;
            }

            // Timeout reset
            if (_nextHandshakeResend < 0)
            {
                _nextHandshakeResend = time + DurationResend;
                return;
            }

            if (time < _nextHandshakeResend)
            {
                // Resend isn't due yet
                return;
            }

            _nextHandshakeResend = time + DurationResend;
            switch (_state)
            {
                case State.WaitingHandshake:
                    if (_sendsFirst)
                    {
                        SendHandshakeAndPubKey(OpCodes.HandshakeStart);
                    }

                    break;
                case State.WaitingHandshakeReply:
                    if (_sendsFirst)
                    {
                        SendHandshakeFin();
                    }
                    else
                    {
                        SendHandshakeAndPubKey(OpCodes.HandshakeAck);
                    }

                    break;
                case State.Ready: // IsReady is checked above & early-returned
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
