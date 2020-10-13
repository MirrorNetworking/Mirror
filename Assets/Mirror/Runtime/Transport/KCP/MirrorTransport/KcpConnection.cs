using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Mirror.KCP
{
    public abstract class KcpConnection
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

            Tick();
        }

        // TODO respect check result.
        // for now let's call every update.
        public void Tick()
        {
            try
            {
                lastReceived = kcp.CurrentMS;

                if (open && kcp.CurrentMS < lastReceived + TIMEOUT)
                {
                    kcp.Update();

                    int check = kcp.Check();

                    // call every 10 ms unless check says we can wait longer
                    if (check < 10)
                        check = 10;
                }
            }
            catch (SocketException)
            {
                // this is ok, the connection was closed
                open = false;
            }
            catch (ObjectDisposedException)
            {
                // fine,  socket was closed,  no more ticking needed
                open = false;
            }
            catch (Exception ex)
            {
                open = false;
                Debug.LogException(ex);
            }
        }

        internal void RawInput(byte[] buffer, int msgLength)
        {
            kcp.Input(buffer, 0, msgLength, true, false);

            lastReceived = kcp.CurrentMS;

            if (kcp.PeekSize() > 0)
            {
                // we just got a full message
                // Let the receivers know
                //dataAvailable?.TrySetResult();
                Debug.LogWarning("KcpConnection: TODO received message!");
            }
        }

        protected abstract void RawSend(byte[] data, int length);

        public void Send(ArraySegment<byte> data)
        {
            kcp.Send(data.Array, data.Offset, data.Count);
        }

        protected virtual void Dispose()
        {
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        /*public bool Receive(MemoryStream buffer)
        {
            int msgSize = kcp.PeekSize();

            while (msgSize < 0 && open)
            {
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
            if (dataSegment.SequenceEqual(Goodby))
            {
                open = false;
                Disconnected?.Invoke();
                return false;
            }

            return true;
        }*/

        protected void Handshake()
        {
            Debug.LogWarning("KcpConnection: TODO Handshake!");

            // send a greeting and see if the server replies
            /*await SendAsync(Hello);
            var stream = new MemoryStream();
            if (!await ReceiveAsync(stream))
            {
                throw new OperationCanceledException("Unable to establish connection, no Handshake message received.");
            }*/
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
                    // TODO_ = SendAsync(Goodby);
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
            //dataAvailable?.TrySetResult();
        }
    }
}
