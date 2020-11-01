using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Debug = UnityEngine.Debug;

namespace kcp2k
{
    enum KcpState { Connected, Authenticated, Disconnected }

    public abstract class KcpConnection
    {
        protected Socket socket;
        protected EndPoint remoteEndpoint;
        internal Kcp kcp;

        // kcp can have several different states, let's use a state machine
        KcpState state = KcpState.Disconnected;

        public Action OnAuthenticated;
        public Action<ArraySegment<byte>> OnData;
        public Action OnDisconnected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int TIMEOUT = 10000;
        uint lastReceiveTime;

        // internal time.
        // StopWatch offers ElapsedMilliSeconds and should be more precise than
        // Unity's time.deltaTime over long periods.
        readonly Stopwatch refTime = new Stopwatch();

        // recv buffer to avoid allocations
        byte[] buffer = new byte[Kcp.MTU_DEF];

        internal static readonly ArraySegment<byte> Hello = new ArraySegment<byte>(new byte[] { 0 });
        static readonly ArraySegment<byte> Goodbye = new ArraySegment<byte>(new byte[] { 1 });
        static readonly ArraySegment<byte> Ping = new ArraySegment<byte>(new byte[] { 2 });

        // send a ping occasionally so we don't time out on the other end.
        // for example, creating a character in an MMO could easily take a
        // minute of no data being sent. which doesn't mean we want to time out.
        // same goes for slow paced card games etc.
        public const int PING_INTERVAL = 1000;
        uint lastPingTime;

        // if we send more than kcp can handle, we will get ever growing
        // send/recv buffers and queues and minutes of latency.
        // => if a connection can't keep up, it should be disconnected instead
        //    to protect the server under heavy load, and because there is no
        //    point in growing to gigabytes of memory or minutes of latency!
        // => 2k isn't enough. we reach 2k when spawning 4k monsters at once
        //    easily, but it does recover over time.
        // => 10k seems safe.
        //
        // note: we have a ChokeConnectionAutoDisconnects test for this too!
        internal const int QueueDisconnectThreshold = 10000;

        // getters for queue and buffer counts, used for debug info
        public int SendQueueCount => kcp.snd_queue.Count;
        public int ReceiveQueueCount => kcp.rcv_queue.Count;
        public int SendBufferCount => kcp.snd_buf.Count;
        public int ReceiveBufferCount => kcp.rcv_buf.Count;

        // NoDelay, interval, window size are the most important configurations.
        // let's force require the parameters so we don't forget it anywhere.
        protected void SetupKcp(bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV)
        {
            kcp = new Kcp(0, RawSend);
            // set nodelay.
            // note that kcp uses 'nocwnd' internally so we negate the parameter
            kcp.SetNoDelay(noDelay ? 1u : 0u, interval, fastResend, !congestionWindow);
            kcp.SetWindowSize(sendWindowSize, receiveWindowSize);
            refTime.Start();
            state = KcpState.Connected;

            Tick();
        }

        void HandleTimeout(uint time)
        {
            // note: we are also sending a ping regularly, so timeout should
            //       only ever happen if the connection is truly gone.
            if (time >= lastReceiveTime + TIMEOUT)
            {
                Debug.LogWarning($"KCP: Connection timed out after {TIMEOUT}ms. Disconnecting.");
                Disconnect();
            }
        }

        void HandleDeadLink()
        {
            // kcp has 'dead_link' detection. might as well use it.
            if (kcp.state == -1)
            {
                Debug.LogWarning("KCP Connection dead_link detected. Disconnecting.");
                Disconnect();
            }
        }

        // send a ping occasionally in order to not time out on the other end.
        void HandlePing(uint time)
        {
            // enough time elapsed since last ping?
            if (time >= lastPingTime + PING_INTERVAL)
            {
                // ping again and reset time
                //Debug.Log("KCP: sending ping...");
                Send(Ping);
                lastPingTime = time;
            }
        }

        void HandleChoked()
        {
            // disconnect connections that can't process the load.
            // see QueueSizeDisconnect comments.
            int total = kcp.rcv_queue.Count + kcp.snd_queue.Count +
                        kcp.rcv_buf.Count + kcp.snd_buf.Count;
            if (total >= QueueDisconnectThreshold)
            {
                Debug.LogWarning($"KCP: disconnecting connection because it can't process data fast enough.\n" +
                                 $"Queue total {total}>{QueueDisconnectThreshold}. rcv_queue={kcp.rcv_queue.Count} snd_queue={kcp.snd_queue.Count} rcv_buf={kcp.rcv_buf.Count} snd_buf={kcp.snd_buf.Count}\n" +
                                 $"* Try to Enable NoDelay, decrease INTERVAL, disable Congestion Window (= enable NOCWND!), increase SEND/RECV WINDOW or compress data.\n" +
                                 $"* Or perhaps the network is simply too slow on our end, or on the other end.\n");

                // let's clear all pending sends before disconnting with 'Bye'.
                // otherwise a single Flush in Disconnect() won't be enough to
                // flush thousands of messages to finally deliver 'Bye'.
                // this is just faster and more robust.
                kcp.snd_queue.Clear();

                Disconnect();
            }
        }

        // reads the next message from connection.
        bool ReceiveNext(out ArraySegment<byte> message)
        {
            // read only one message
            int msgSize = kcp.PeekSize();
            if (msgSize > 0)
            {
                // only allow receiving up to MaxMessageSize sized messages.
                // otherwise we would get BlockCopy ArgumentException anyway.
                if (msgSize <= Kcp.MTU_DEF)
                {
                    int received = kcp.Receive(buffer, msgSize);
                    if (received >= 0)
                    {
                        message = new ArraySegment<byte>(buffer, 0, msgSize);
                        lastReceiveTime = (uint)refTime.ElapsedMilliseconds;

                        // return false if it was a ping message. true otherwise.
                        if (Utils.SegmentsEqual(message, Ping))
                        {
                            //Debug.Log("KCP: received ping.");
                            return false;
                        }
                        return true;
                    }
                    else
                    {
                        // if receive failed, close everything
                        Debug.LogWarning($"Receive failed with error={received}. closing connection.");
                        Disconnect();
                    }
                }
                // we don't allow sending messages > Max, so this must be an
                // attacker. let's disconnect to avoid allocation attacks etc.
                else
                {
                    Debug.LogWarning($"KCP: possible allocation attack for msgSize {msgSize} > max {Kcp.MTU_DEF}. Disconnecting the connection.");
                    Disconnect();
                }
            }
            return false;
        }

        void TickConnected(uint time)
        {
            // detect common events & ping
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            kcp.Update(time);

            // any message received?
            if (ReceiveNext(out ArraySegment<byte> message))
            {
                // handshake message?
                if (Utils.SegmentsEqual(message, Hello))
                {
                    Debug.Log("KCP: received handshake");
                    state = KcpState.Authenticated;
                    OnAuthenticated?.Invoke();
                }
                // otherwise it's random data from the internet, not
                // from a legitimate player. disconnect.
                else
                {
                    Debug.LogWarning("KCP: received random data before handshake. Disconnecting the connection.");
                    Disconnect();
                }
            }
        }

        void TickAuthenticated(uint time)
        {
            // detect common events & ping
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            kcp.Update(time);

            // process all received messages
            while (ReceiveNext(out ArraySegment<byte> message))
            {
                // disconnect message?
                if (Utils.SegmentsEqual(message, Goodbye))
                {
                    Debug.Log("KCP: received disconnect message");
                    Disconnect();
                    break;
                }
                // otherwise regular message
                else
                {
                    // only accept regular messages
                    //Debug.LogWarning($"Kcp recv msg: {BitConverter.ToString(buffer, 0, msgSize)}");
                    OnData?.Invoke(message);
                }
            }
        }

        public void Tick()
        {
            uint time = (uint)refTime.ElapsedMilliseconds;

            try
            {
                switch (state)
                {
                    case KcpState.Connected:
                    {
                        TickConnected(time);
                        break;
                    }
                    case KcpState.Authenticated:
                    {
                        TickAuthenticated(time);
                        break;
                    }
                    case KcpState.Disconnected:
                    {
                        // do nothing while disconnected
                        break;
                    }
                }
            }
            catch (SocketException exception)
            {
                // this is ok, the connection was closed
                Debug.Log($"KCP Connection: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                // fine, socket was closed
                Debug.Log($"KCP Connection: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (Exception ex)
            {
                // unexpected
                Debug.LogException(ex);
                Disconnect();
            }
        }

        public void RawInput(byte[] buffer, int msgLength)
        {
            int input = kcp.Input(buffer, msgLength);
            if (input != 0)
            {
                Debug.LogWarning($"Input failed with error={input} for buffer with length={msgLength}");
            }
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

        // server & client need to send handshake at different times, so we need
        // to expose the function.
        // * client should send it immediately.
        // * server should send it as reply to client's handshake, not before
        //   (server should not reply to random internet messages with handshake)
        public void SendHandshake()
        {
            Debug.Log("KcpConnection: sending Handshake to other end!");
            Send(Hello);
        }

        protected virtual void Dispose()
        {
        }

        // disconnect this connection
        public void Disconnect()
        {
            // only if not disconnected yet
            if (state == KcpState.Disconnected)
                return;

            // send a disconnect message
            if (socket.Connected)
            {
                try
                {
                    Send(Goodbye);
                    kcp.Flush();
                }
                catch (SocketException)
                {
                    // this is ok, the connection was already closed
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

            // set as Disconnected, call event
            Debug.Log("KCP Connection: Disconnected.");
            state = KcpState.Disconnected;
            OnDisconnected?.Invoke();
        }

        // get remote endpoint
        public EndPoint GetRemoteEndPoint() => remoteEndpoint;
    }
}
