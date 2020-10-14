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
        volatile bool open;


        internal event Action Disconnected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int TIMEOUT = 3000;

        volatile uint lastReceived;

        internal static readonly ArraySegment<byte> Hello = new ArraySegment<byte>(new byte[] { 0 });
        private static readonly ArraySegment<byte> Goodby = new ArraySegment<byte>(new byte[] { 1 });

        protected KcpConnection()
        {
        }

        protected void SetupKcp()
        {
            kcp = new Kcp(0, RawSend);
            kcp.SetNoDelay();
            open = true;

            _ = Tick();
        }

        async UniTask Tick()
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

        volatile bool isWaiting = false;

        UniTaskCompletionSource dataAvailable;

        internal void RawInput(byte[] buffer, int msgLength)
        {
            kcp.Input(buffer, 0, msgLength, true, false);

            lastReceived = kcp.CurrentMS;

            if (isWaiting && kcp.PeekSize() > 0)
            {
                // we just got a full message
                // Let the receivers know
                dataAvailable?.TrySetResult();
            }
        }

        protected abstract void RawSend(byte[] data, int length);

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
                dataAvailable = new UniTaskCompletionSource();
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

        internal async UniTask Handshake()
        {
            // send a greeting and see if the server replies
            await SendAsync(Hello);
            var stream = new MemoryStream();
            if (!await ReceiveAsync(stream))
            {
                throw new OperationCanceledException("Unable to establish connection, no Handshake message received.");
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
                    _ = SendAsync(Goodby);
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
