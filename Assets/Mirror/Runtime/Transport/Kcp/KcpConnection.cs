using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public abstract class KcpConnection : IConnection
    {
        protected Socket socket;
        protected EndPoint remoteEndpoint;
        protected Kcp kcp;
        protected Unreliable unreliable;

        readonly KcpDelayMode delayMode;
        volatile bool open;

        public int CHANNEL_SIZE = 4;

        internal event Action Disconnected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public int Timeout { get; set; } = 15000;

        volatile uint lastReceived;

        /// <summary>
        /// Space for CRC64
        /// </summary>
        public const int RESERVED = sizeof(ulong);

        internal static readonly ArraySegment<byte> Hello = new ArraySegment<byte>(new byte[] { 0 });
        private static readonly ArraySegment<byte> Goodby = new ArraySegment<byte>(new byte[] { 1 });

        protected KcpConnection(KcpDelayMode delayMode)
        {
            this.delayMode = delayMode;
        }

        protected void SetupKcp()
        {
            unreliable = new Unreliable(SendWithChecksum)
            {
                Reserved = RESERVED
            };

            kcp = new Kcp(0, SendWithChecksum);
            kcp.SetNoDelay(delayMode);

            // reserve some space for CRC64
            kcp.ReserveBytes(RESERVED);
            open = true;

            Tick().Forget();
        }

        async UniTaskVoid Tick()
        {
            try
            {
                lastReceived = kcp.CurrentMS;

                while (open && kcp.CurrentMS < lastReceived + Timeout)
                {
                    kcp.Update();

                    int check = kcp.Check();

                    // call every 10 ms unless check says we can wait longer
                    if (check < 10)
                        check = 10;

                    await UniTask.Delay(check);
                }
            }
            catch (SocketException)
            {
                // this is ok, the connection was closed
            }
            catch (ObjectDisposedException)
            {
                // fine,  socket was closed,  no more ticking needed
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                open = false;
                dataAvailable?.TrySetResult();
                Close();
            }
        }

        protected virtual void Close()
        {
        }

        volatile bool isWaiting;

        AutoResetUniTaskCompletionSource dataAvailable;

        internal void RawInput(byte[] buffer, int msgLength)
        {
            // check packet integrity
            if (!Validate(buffer, msgLength))
                return;

            int channel = GetChannel(buffer);
            if (channel == Channel.Reliable)
                InputReliable(buffer, msgLength);
            else if (channel == Channel.Unreliable)
                InputUnreliable(buffer, msgLength);
        }

        private void InputUnreliable(byte[] buffer, int msgLength)
        {
            unreliable.Input(buffer, RESERVED, msgLength - RESERVED);
            lastReceived = kcp.CurrentMS;

            if (isWaiting && unreliable.PeekSize() > 0)
            {
                dataAvailable?.TrySetResult();
            }
        }

        private void InputReliable(byte[] buffer, int msgLength)
        {
            kcp.Input(buffer, RESERVED, msgLength - RESERVED);

            lastReceived = kcp.CurrentMS;

            if (isWaiting && kcp.PeekSize() > 0)
            {
                // we just got a full message
                // Let the receivers know
                dataAvailable?.TrySetResult();
            }
        }

        private bool Validate(byte[] buffer, int msgLength)
        {
            // Recalculate CRC64 and check against checksum in the head
            var decoder = new Decoder(buffer, 0);
            ulong receivedCrc = decoder.Decode64U();
            ulong calculatedCrc = Crc64.Compute(buffer, decoder.Position, msgLength - decoder.Position);
            return receivedCrc == calculatedCrc;
        }

        protected abstract void RawSend(byte[] data, int length);

        private void SendWithChecksum(byte [] data, int length)
        {
            // add a CRC64 checksum in the reserved space
            ulong crc = Crc64.Compute(data, RESERVED, length - RESERVED);
            var encoder = new Encoder(data, 0);
            encoder.Encode64U(crc);
            RawSend(data, length);

            if (kcp.WaitSnd > 1000)
            {
                Debug.LogWarningFormat("Too many packets waiting in the send queue {0}, you are sending too much data,  the transport can't keep up", kcp.WaitSnd);
            }
        }

        public UniTask SendAsync(ArraySegment<byte> data, int channel = Channel.Reliable)
        {
            if (channel == Channel.Reliable)
                kcp.Send(data.Array, data.Offset, data.Count);
            else if (channel == Channel.Unreliable)
                unreliable.Send(data.Array, data.Offset, data.Count);

            return UniTask.CompletedTask;
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public async UniTask<(bool next, int channel)> ReceiveAsync(MemoryStream buffer)
        {
            while (kcp.PeekSize() < 0 && unreliable.PeekSize() < 0 && open) { 
                isWaiting = true;
                dataAvailable = AutoResetUniTaskCompletionSource.Create();
                await dataAvailable.Task;
                isWaiting = false;
            }

            if (!open)
            {
                Disconnected?.Invoke();
                return (false, Channel.Reliable);
            }

            if (unreliable.PeekSize() >= 0)
            {
                // we got a message in the unreliable channel
                int msgSize = unreliable.PeekSize();
                buffer.SetLength(msgSize);
                buffer.Position = 0;
                buffer.TryGetBuffer(out ArraySegment<byte> data);
                unreliable.Receive(data.Array, data.Offset, data.Count);
                return (true, Channel.Unreliable);
            }
            else
            {
                int msgSize = kcp.PeekSize();
                // we have some data,  return it
                buffer.SetLength(msgSize);
                buffer.Position = 0;
                buffer.TryGetBuffer(out ArraySegment<byte> data);
                kcp.Receive(data.Array, data.Offset, data.Count);

                // if we receive a disconnect message,  then close everything

                var dataSegment = new ArraySegment<byte>(buffer.GetBuffer(), 0, msgSize);
                if (Utils.Equal(dataSegment, Goodby))
                {
                    open = false;
                    Disconnected?.Invoke();
                    return (false, Channel.Reliable);
                }
                return (true, Channel.Reliable);
            }
        }

        /// <summary>
        ///     Disconnect this connection
        /// </summary>
        public virtual void Disconnect()
        {
            // send a disconnect message and disconnect
            if (open && socket.Connected)
            {
                try
                {
                    SendAsync(Goodby).Forget();
                    kcp.Flush(false);
                }
                catch (SocketException)
                {
                    // this is ok,  the connection was already closed
                }
                catch (ObjectDisposedException)
                {
                    // this is normal when we stop the server
                    // the socket is stopped so we can't send anything anymore
                    // to the clients

                    // the clients will eventually timeout and realize they
                    // were disconnected
                }
            }
            open = false;

            // EOF is now available
            dataAvailable?.TrySetResult();
        }

        /// <summary>
        ///     the address of endpoint we are connected to
        ///     Note this can be IPEndPoint or a custom implementation
        ///     of EndPoint, which depends on the transport
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            return remoteEndpoint;
        }

        public static int GetChannel(byte[] data)
        {
            var decoder = new Decoder(data, RESERVED);
            return (int)decoder.Decode32U();
        }
    }
}
