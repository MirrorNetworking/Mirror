using System;
using System.Linq;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using UnityEngine;

namespace Mirror.Transports.Encryption
{
    // NOTES:
    // this is very allocation heavy due to bouncycastle
    // currently at least 16 bytes per packet due to bouncycastle allocating a new byte[MacSize] every time.
    public class EncryptedConnection
    {
        private static GcmBlockCipher _cipher = new GcmBlockCipher(new AesEngine());

        enum OpCodes : byte
        {
            // TODO
            Data = 1,
            PubKey,
            PubKeyAck,
            HandshakeFin,
        }

        enum State
        {
            WaitingPubKey,
            WaitingPubKeyAck,
            Ready
        }

        private State _state = State.WaitingPubKey;

        public const int Overhead = sizeof(OpCodes) + MacSize + NonceSize;
        public bool IsReady => _state == State.Ready;
        private Action<ArraySegment<byte>, int> _send;
        private Action<ArraySegment<byte>, int> _receive;
        private Action _ready;
        private Action<TransportError, string> _error;
        private readonly EncryptionCredentials _credentials;


        private const int NonceSize = 12; // 96 bits
        private const int MacSize = 16; // 128 bits for GCM tag length

        // we can reuse the _cipherParameters here since the nonce is stored as the byte[] reference we pass in
        // so we can update it without creating a new AeadParameters instance
        // this might break in the future! (will cause bad data)
        private AeadParameters _cipherParametersEncrypt;
        private AeadParameters _cipherParametersDecrypt;
        private byte[] _nonce = new byte[NonceSize];

        private static byte[] _receiveNonce = new byte[NonceSize];

        /*
         * Specifies if we send the first key, then receive ack, then send fin
         * Or the opposite if set to false
         *
         * The client does this, since the fin is not acked explicitly, but by receiving data to decrypt
         */
        private bool _sendsFirst;

        public EncryptedConnection(
            EncryptionCredentials credentials,
            bool isClient,
            Action<ArraySegment<byte>, int> sendAction,
            Action<ArraySegment<byte>, int> receiveAction,
            Action readyAction,
            Action<TransportError, string> errorAction
        )
        {
            _credentials = credentials;
            _sendsFirst = isClient;
            _send = sendAction;
            _receive = receiveAction;
            _ready = readyAction;
            _error = errorAction;
        }

        private static byte[] GenerateStartingNonce() // Default size for AES-GCM
        {
            byte[] nonce = new byte[NonceSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            return nonce;
        }

        public void OnReceiveRaw(ArraySegment<byte> data, int channel)
        {
            if (data.Count < 1)
            {
                _error(TransportError.Unexpected, "received empty packet");
                return;
            }

            using (var reader = NetworkReaderPool.Get(data))
            {
                var opcode = (OpCodes)reader.ReadByte();
                Debug.Log($"[{(_sendsFirst ? 1 : 0)}] Recv: {opcode}");
                switch (opcode)
                {
                    case OpCodes.Data:
                        if (_sendsFirst && _state == State.WaitingPubKeyAck)
                        {
                            _state = State.Ready;
                            _ready();
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

                        var ciphertext = reader.ReadBytesSegment(reader.Remaining - NonceSize);
                        reader.ReadBytes(_receiveNonce, NonceSize);
                        var cleartext = Decrypt(ciphertext);
                        _receive(cleartext, channel);
                        break;
                    case OpCodes.PubKey:
                        if (_sendsFirst)
                        {
                            _error(TransportError.Unexpected, "Received PubKey packet, we don't expect this.");
                            return;
                        }

                        if (_state == State.WaitingPubKeyAck)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }

                        _state = State.WaitingPubKeyAck;
                        // todo: doesn't reset timeout or resend
                        CompleteExchange(reader.ReadBytesSegment(reader.Remaining));
                        SendPubKey(OpCodes.PubKeyAck);
                        break;
                    case OpCodes.PubKeyAck:
                        if (!_sendsFirst)
                        {
                            _error(TransportError.Unexpected, "Received PubKeyAck packet, we don't expect this.");
                            return;
                        }

                        if (IsReady)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }
                        if (_state == State.WaitingPubKeyAck)
                        {
                            // this is fine, packets may arrive out of order
                            return;
                        }
                        // todo: doesn't reset timeout or resend
                        _state = State.WaitingPubKeyAck;
                        CompleteExchange(reader.ReadBytesSegment(reader.Remaining));
                        SendFin();
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

                        if (_state != State.WaitingPubKeyAck)
                        {
                            _error(TransportError.Unexpected,
                                "Received HandshakeFin packet, we didn't expect this yet.");
                            return;
                        }

                        _state = State.Ready;
                        _ready();

                        break;
                    default:
                        _error(TransportError.InvalidReceive, $"Unhandled opcode {data[0]:x}");
                        break;
                }
            }
        }

        public void Send(ArraySegment<byte> data, int channel)
        {
            Debug.Log($"[{(_sendsFirst ? 1 : 0)}] Sending {OpCodes.Data}");
            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteByte((byte)OpCodes.Data);
                var encrypted = Encrypt(data);
                writer.WriteBytes(encrypted.Array, 0, encrypted.Count);
                // write nonce after since Encrypt will update it
                writer.WriteBytes(_nonce, 0, NonceSize);
                _send(writer.ToArraySegment(), channel);
            }
        }

        private static byte[] _tmpCryptBuffer = new byte[512];
        private double _timeout;
        private double _nextSend;

        private ArraySegment<byte> Encrypt(ArraySegment<byte> plaintext)
        {
            UpdateNonce();
            PrintBytes(_nonce, "EncryptNonce");
            _cipher = new GcmBlockCipher(new AesEngine());
            _cipher.Init(true, _cipherParametersEncrypt);

            var outSize = _cipher.GetOutputSize(plaintext.Count);
            if (outSize != plaintext.Count + MacSize)
            {
                // TODO: this is only for double checking me
                throw new Exception("Unexpected output size");
            }

            EnsureSize(ref _tmpCryptBuffer, outSize);
            int resultLen =
                _cipher.ProcessBytes(plaintext.Array, plaintext.Offset, plaintext.Count, _tmpCryptBuffer, 0);
            resultLen += _cipher.DoFinal(_tmpCryptBuffer, resultLen);
            var res = new ArraySegment<byte>(_tmpCryptBuffer, 0, resultLen);

            PrintBytes(res, "EncryptData");
            return res;
        }

        private ArraySegment<byte> Decrypt(ArraySegment<byte> ciphertext)
        {
            PrintBytes(_receiveNonce, "DecryptNonce");
            PrintBytes(ciphertext, "DecryptData");

            _cipher = new GcmBlockCipher(new AesEngine());
            _cipher.Init(false, _cipherParametersDecrypt);

            var outSize = _cipher.GetOutputSize(ciphertext.Count);
            if (outSize != ciphertext.Count - MacSize)
            {
                // TODO: this is only for double checking me
                throw new Exception("Unexpected output size");
            }

            EnsureSize(ref _tmpCryptBuffer, outSize);
            int resultLen =
                _cipher.ProcessBytes(ciphertext.Array, ciphertext.Offset, ciphertext.Count, _tmpCryptBuffer, 0);
            resultLen += _cipher.DoFinal(_tmpCryptBuffer, resultLen);
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

        private void EnsureSize(ref byte[] buffer, int size)
        {
            if (buffer.Length < size)
            {
                Array.Resize(ref buffer, size);
            }
        }

        private void SendPubKey(OpCodes opcode)
        {
            Debug.Log($"[{(_sendsFirst ? 1 : 0)}] Sending  {opcode}: " +
                      BitConverter.ToString(_credentials.PublicKeySerialized).Replace("-", string.Empty));
            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteByte((byte)opcode);
                writer.WriteBytes(_credentials.PublicKeySerialized, 0, _credentials.PublicKeySerialized.Length);
                _send(writer.ToArraySegment(), Channels.Unreliable);
            }
        }

        private void SendFin()
        {
            Debug.Log($"[{(_sendsFirst ? 1 : 0)}] Sending {OpCodes.HandshakeFin}");
            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteByte((byte)OpCodes.HandshakeFin);
                _send(writer.ToArraySegment(), Channels.Unreliable);
            }
        }

        private void PrintBytes(ArraySegment<byte> data, string prefix)
        {
            Debug.Log(
                $"[{(_sendsFirst ? 1 : 0)}] {prefix}: {BitConverter.ToString(data.Array, data.Offset, data.Count).Replace("-", string.Empty)}");
        }

        private void CompleteExchange(ArraySegment<byte> remotePubKeyRaw)
        {
            PrintBytes(remotePubKeyRaw, "remote pub key");
            var remotePubKey = EncryptionCredentials.DeserializePublicKey(remotePubKeyRaw);
            // TODO: validation
            ECDHBasicAgreement ecdh = new ECDHBasicAgreement();
            ecdh.Init(_credentials.PrivateKey);
            var keyRaw = ecdh.CalculateAgreement(remotePubKey).ToByteArrayUnsigned();
            PrintBytes(keyRaw, "exchanged symmetric key");
            var key = new KeyParameter(keyRaw);
            _nonce = GenerateStartingNonce();
            _cipherParametersEncrypt = new AeadParameters(key, MacSize * 8 /* in bits */, _nonce);
            _cipherParametersDecrypt = new AeadParameters(key, MacSize * 8 /* in bits */, _receiveNonce);
        }

        /**
         * non-ready connections need to be ticked for resending key data over unreliable
         */
        public void Tick(double time)
        {
            if (!IsReady)
            {
                if (_timeout == 0)
                {
                    _timeout = time + DurationTimeout;
                }
                else if (time > _timeout)
                {
                    _error?.Invoke(TransportError.Timeout, $"Timed out during {_state}");
                    return;
                }

                if (time > _nextSend)
                {
                    _nextSend = time + DurationResend;
                    switch (_state)
                    {
                        case State.WaitingPubKey:
                            if (_sendsFirst)
                            {
                                SendPubKey(OpCodes.PubKey);
                            }

                            break;
                        case State.WaitingPubKeyAck:
                            if (_sendsFirst)
                            {
                                SendFin();
                            }
                            else
                            {
                                SendPubKey(OpCodes.PubKeyAck);
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        private const double DurationTimeout = 2;
        private const double DurationResend = 0.05; // 50ms
    }
}
