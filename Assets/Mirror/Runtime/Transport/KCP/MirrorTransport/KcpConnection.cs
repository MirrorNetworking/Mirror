using System;
using System.Net;
using System.Net.Sockets;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Mirror.KCP
{
    public abstract class KcpConnection
    {
        protected Socket socket;
        protected EndPoint remoteEndpoint;
        internal Kcp kcp;
        volatile bool open;

        internal event Action OnConnected;
        internal event Action<ArraySegment<byte>> OnData;
        internal event Action OnDisconnected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int TIMEOUT = 3000;

        // recv buffer to avoid allocations
        byte[] buffer = new byte[Kcp.MTU_DEF];

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
        }

        protected abstract void RawSend(byte[] data, int length);

        public void Send(ArraySegment<byte> data)
        {
            kcp.Send(data.Array, data.Offset, data.Count);
        }

        protected virtual void Dispose()
        {
        }

        // ArraySegment content comparison without Linq
        public static unsafe bool SegmentsEqual(ArraySegment<byte> a, ArraySegment<byte> b)
        {
            if (a.Count == b.Count)
            {
                fixed (byte* aPtr = &a.Array[a.Offset],
                             bPtr = &b.Array[b.Offset])
                {
                    return UnsafeUtility.MemCmp(aPtr, bPtr, a.Count) == 0;
                }
            }
            return false;
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public void Receive()
        {
            if (!open)
            {
                OnDisconnected?.Invoke();
                Debug.LogWarning("DISCO a");
                return;
            }

            // read as long as we have things to read
            int msgSize;
            while ((msgSize = kcp.PeekSize()) > 0)
            {
                kcp.Receive(buffer, 0, msgSize);

                ArraySegment<byte> dataSegment = new ArraySegment<byte>(buffer, 0, msgSize);

                // handshake message?
                if (SegmentsEqual(dataSegment, Hello))
                {
                    // we are only connected if we received the handshake.
                    // not just after receiving any first data.
                    Debug.LogWarning("Kcp recv handshake");
                    OnConnected?.Invoke();
                }
                // disconnect message?
                else if (SegmentsEqual(dataSegment, Goodby))
                {
                    // if we receive a disconnect message,  then close everything
                    Debug.LogWarning("Kcp recv disconnected");
                    open = false;
                    OnDisconnected?.Invoke();
                }
                // otherwise regular message
                else
                {
                    //Debug.LogWarning($"Kcp recv msg: {BitConverter.ToString(buffer, 0, msgSize)}");
                    OnData?.Invoke(dataSegment);
                }
            }
        }

        public void Handshake()
        {
            // send a greeting and see if the server replies
            Debug.LogWarning("KcpConnection: sending Handshake to other end!");
            Send(Hello);
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
                    Send(Goodby);
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
