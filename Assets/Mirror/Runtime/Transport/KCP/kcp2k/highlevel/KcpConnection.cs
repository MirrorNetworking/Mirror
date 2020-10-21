using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace kcp2k
{
    public abstract class KcpConnection
    {
        protected Socket socket;
        protected EndPoint remoteEndpoint;
        internal Kcp kcp;
        volatile bool open;

        public event Action OnConnected;
        public event Action<ArraySegment<byte>> OnData;
        public event Action OnDisconnected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int TIMEOUT = 3000;

        // internal time.
        // StopWatch offers ElapsedMilliSeconds and should be more precise than
        // Unity's time.deltaTime over long periods.
        readonly Stopwatch refTime = new Stopwatch();

        // recv buffer to avoid allocations
        byte[] buffer = new byte[Kcp.MTU_DEF];

        volatile uint lastReceived;

        internal static readonly ArraySegment<byte> Hello = new ArraySegment<byte>(new byte[] { 0 });
        private static readonly ArraySegment<byte> Goodby = new ArraySegment<byte>(new byte[] { 1 });

        // a connection is authenticated after sending the correct handshake.
        // useful to protect against random data from the internet.
        bool authenticated;

        protected KcpConnection()
        {
        }

        // NoDelay & interval are the most important configurations.
        // let's force require the parameters so we don't forget it anywhere.
        protected void SetupKcp(bool noDelay, uint interval = Kcp.INTERVAL)
        {
            kcp = new Kcp(0, RawSend);
            kcp.SetNoDelay(noDelay ? 1u : 0u, interval);
            refTime.Start();
            open = true;

            Tick();
        }

        public void Tick()
        {
            try
            {
                uint time = (uint)refTime.ElapsedMilliseconds;

                // TODO MirorrNG KCP used to set lastReceived here. but this
                // would make the below time check always true.
                // should we set lastReceived after updating instead?
                //lastReceived = time;

                if (open && time < lastReceived + TIMEOUT)
                {
                    kcp.Update(time);

                    // check can be used to skip updates IF:
                    // - time < what check returned
                    // - AND send / recv haven't been called in that time
                    // (see Check() comments)
                    //
                    // for now, let's just always update and not call check.
                    //uint check = kcp.Check();
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

        public void RawInput(byte[] buffer, int msgLength)
        {
            int input = kcp.Input(buffer, msgLength);
            if (input == 0)
            {
                lastReceived = (uint)refTime.ElapsedMilliseconds;
            }
            else Debug.LogWarning($"Input failed with error={input} for buffer with length={msgLength}");
        }

        protected abstract void RawSend(byte[] data, int length);

        public void Send(ArraySegment<byte> data)
        {
            // only allow sending up to MaxMessageSize sized messages.
            // other end won't process bigger messages anyway.
            if (data.Count <= Kcp.MTU_DEF)
            {
                int sent = kcp.Send(data.Array, data.Offset, data.Count);
                if (sent < 0)
                {
                    Debug.LogWarning($"Send failed with error={sent} for segment with length={data.Count}");
                }
            }
            else Debug.LogError($"Failed to send message of size {data.Count} because it's larger than MaxMessageSize={Kcp.MTU_DEF}");
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
                // only allow receiving up to MaxMessageSize sized messages.
                // otherwise we would get BlockCopy ArgumentException anyway.
                if (msgSize <= Kcp.MTU_DEF)
                {
                    int received = kcp.Receive(buffer, msgSize);
                    if (received >= 0)
                    {
                        ArraySegment<byte> dataSegment = new ArraySegment<byte>(buffer, 0, msgSize);

                        // not authenticated yet?
                        if (!authenticated)
                        {
                            // handshake message?
                            if (SegmentsEqual(dataSegment, Hello))
                            {
                                // we are only connected if we received the handshake.
                                // not just after receiving any first data.
                                authenticated = true;
                                //Debug.Log("KCP: received handshake");
                                OnConnected?.Invoke();
                            }
                            // otherwise it's random data from the internet, not
                            // from a legitimate player.
                            else
                            {
                                Debug.LogWarning("KCP: received random data before handshake. Disconnecting the connection.");
                                open = false;
                                OnDisconnected?.Invoke();
                                break;
                            }
                        }
                        // authenticated.
                        else
                        {
                            // disconnect message?
                            if (SegmentsEqual(dataSegment, Goodby))
                            {
                                // if we receive a disconnect message,  then close everything
                                //Debug.Log("KCP: received disconnect message");
                                open = false;
                                OnDisconnected?.Invoke();
                                break;
                            }
                            // otherwise regular message
                            else
                            {
                                // only accept regular messages
                                //Debug.LogWarning($"Kcp recv msg: {BitConverter.ToString(buffer, 0, msgSize)}");
                                OnData?.Invoke(dataSegment);
                            }
                        }
                    }
                    else
                    {
                        // if receive failed, close everything
                        Debug.LogWarning($"Receive failed with error={received}. closing connection.");
                        open = false;
                        OnDisconnected?.Invoke();
                        break;
                    }
                }
                // we don't allow sending messages > Max, so this must be an
                // attacker. let's disconnect to avoid allocation attacks etc.
                else
                {
                    Debug.LogWarning($"KCP: possible allocation attack for msgSize {msgSize} > max {Kcp.MTU_DEF}. Disconnecting the connection.");
                    open = false;
                    OnDisconnected?.Invoke();
                    break;
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
                    kcp.Flush();

                    // call OnDisconnected event, even if we manually
                    // disconnected
                    OnDisconnected?.Invoke();
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

        // get remote endpoint
        public EndPoint GetRemoteEndPoint() => remoteEndpoint;
    }
}
