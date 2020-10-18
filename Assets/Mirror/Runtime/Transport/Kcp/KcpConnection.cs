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
        readonly KcpDelayMode delayMode;
        volatile bool open;

        internal event Action Disconnected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int TIMEOUT = 15000;

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

                while (open && kcp.CurrentMS < lastReceived + TIMEOUT)
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
                Dispose();
            }
        }

        protected virtual void Dispose()
        {
        }

        volatile bool isWaiting;

        AutoResetUniTaskCompletionSource dataAvailable;

        internal void RawInput(byte[] buffer, int msgLength)
        {
            // check packet integrity
            if (!Validate(buffer, msgLength))
                return;

            kcp.Input(buffer, RESERVED, msgLength - RESERVED, true, false);

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
            (int offset, ulong receivedCrc) = Utils.Decode64U(buffer, 0);
            ulong calculatedCrc = Crc64.Compute(buffer, offset, msgLength - offset);
            return receivedCrc == calculatedCrc;
        }

        protected abstract void RawSend(byte[] data, int length);

        private void SendWithChecksum(byte [] data, int length)
        {
            // add a CRC64 checksum in the reserved space
            ulong crc = Crc64.Compute(data, RESERVED, length - RESERVED);
            Utils.Encode64U(data, 0, crc);
            RawSend(data, length);

            if (kcp.WaitSnd > 1000)
            {
                Debug.LogWarningFormat("Too many packets waiting in the send queue {0}, you are sending too much data,  the transport can't keep up", kcp.WaitSnd);
            }
        }

        public UniTask SendAsync(ArraySegment<byte> data)
        {
            kcp.Send(data.Array, data.Offset, data.Count);
            return UniTask.CompletedTask;
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public async UniTask<bool> ReceiveAsync(MemoryStream buffer)
        {
            int msgSize = kcp.PeekSize();

            while (msgSize < 0 && open) { 
                isWaiting = true;
                dataAvailable = AutoResetUniTaskCompletionSource.Create();
                await dataAvailable.Task;
                isWaiting = false;
                msgSize = kcp.PeekSize();
            }

            if (!open)
            {
                Disconnected?.Invoke();
                return false;
            }

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
                return false;
            }

            return true;
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
    }
}
