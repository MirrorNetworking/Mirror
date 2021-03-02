using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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

        // Mirror needs a way to stop the kcp message processing while loop
        // immediately after a scene change message. Mirror can't process any
        // other messages during a scene change.
        // (could be useful for others too)
        bool paused;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int TIMEOUT = 10000;
        uint lastReceiveTime;

        // internal time.
        // StopWatch offers ElapsedMilliSeconds and should be more precise than
        // Unity's time.deltaTime over long periods.
        readonly Stopwatch refTime = new Stopwatch();

        // we need to subtract the channel byte from every MaxMessageSize
        // calculation.
        // we also need to tell kcp to use MTU-1 to leave space for the byte.
        const int CHANNEL_HEADER_SIZE = 1;

        // reliable channel (= kcp) MaxMessageSize so the outside knows largest
        // allowed message to send the calculation in Send() is not obvious at
        // all, so let's provide the helper here.
        //
        // kcp does fragmentation, so max message is way larger than MTU.
        //
        // -> runtime MTU changes are disabled: mss is always MTU_DEF-OVERHEAD
        // -> Send() checks if fragment count < WND_RCV, so we use WND_RCV - 1.
        //    note that Send() checks WND_RCV instead of wnd_rcv which may or
        //    may not be a bug in original kcp. but since it uses the define, we
        //    can use that here too.
        // -> we add 1 byte KcpHeader enum to each message, so -1
        //
        // IMPORTANT: max message is MTU * WND_RCV, in other words it completely
        //            fills the receive window! due to head of line blocking,
        //            all other messages have to wait while a maxed size message
        //            is being delivered.
        //            => in other words, DO NOT use max size all the time like
        //               for batching.
        //            => sending UNRELIABLE max message size most of the time is
        //               best for performance (use that one for batching!)
        public const int ReliableMaxMessageSize = (Kcp.MTU_DEF - Kcp.OVERHEAD - CHANNEL_HEADER_SIZE) * (Kcp.WND_RCV - 1) - 1;

        // unreliable max message size is simply MTU - channel header size
        public const int UnreliableMaxMessageSize = Kcp.MTU_DEF - CHANNEL_HEADER_SIZE;

        // buffer to receive kcp's processed messages (avoids allocations).
        // IMPORTANT: this is for KCP messages. so it needs to be of size:
        //            1 byte header + MaxMessageSize content
        byte[] kcpMessageBuffer = new byte[1 + ReliableMaxMessageSize];

        // send buffer for handing user messages to kcp for processing.
        // (avoids allocations).
        // IMPORTANT: needs to be of size:
        //            1 byte header + MaxMessageSize content
        byte[] kcpSendBuffer = new byte[1 + ReliableMaxMessageSize];

        // raw send buffer is exactly MTU.
        byte[] rawSendBuffer = new byte[Kcp.MTU_DEF];

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

        // maximum send rate per second can be calculated from kcp parameters
        // source: https://translate.google.com/translate?sl=auto&tl=en&u=https://wetest.qq.com/lab/view/391.html
        //
        // KCP can send/receive a maximum of WND*MTU per interval.
        // multiple by 1000ms / interval to get the per-second rate.
        //
        // example:
        //   WND(32) * MTU(1400) = 43.75KB
        //   => 43.75KB * 1000 / INTERVAL(10) = 4375KB/s
        //
        // returns bytes/second!
        public uint MaxSendRate =>
            kcp.snd_wnd * kcp.mtu * 1000 / kcp.interval;

        public uint MaxReceiveRate =>
            kcp.rcv_wnd * kcp.mtu * 1000 / kcp.interval;

        // NoDelay, interval, window size are the most important configurations.
        // let's force require the parameters so we don't forget it anywhere.
        protected void SetupKcp(bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV)
        {
            // set up kcp over reliable channel (that's what kcp is for)
            kcp = new Kcp(0, RawSendReliable);
            // set nodelay.
            // note that kcp uses 'nocwnd' internally so we negate the parameter
            kcp.SetNoDelay(noDelay ? 1u : 0u, interval, fastResend, !congestionWindow);
            kcp.SetWindowSize(sendWindowSize, receiveWindowSize);

            // IMPORTANT: high level needs to add 1 channel byte to each raw
            // message. so while Kcp.MTU_DEF is perfect, we actually need to
            // tell kcp to use MTU-1 so we can still put the header into the
            // message afterwards.
            kcp.SetMtu(Kcp.MTU_DEF - CHANNEL_HEADER_SIZE);

            state = KcpState.Connected;

            refTime.Start();
        }

        void HandleTimeout(uint time)
        {
            // note: we are also sending a ping regularly, so timeout should
            //       only ever happen if the connection is truly gone.
            if (time >= lastReceiveTime + TIMEOUT)
            {
                Log.Warning($"KCP: Connection timed out after not receiving any message for {TIMEOUT}ms. Disconnecting.");
                Disconnect();
            }
        }

        void HandleDeadLink()
        {
            // kcp has 'dead_link' detection. might as well use it.
            if (kcp.state == -1)
            {
                Log.Warning("KCP Connection dead_link detected. Disconnecting.");
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
                //Log.Debug("KCP: sending ping...");
                SendPing();
                lastPingTime = time;
            }
        }

        void HandleChoked()
        {
            // disconnect connections that can't process the load.
            // see QueueSizeDisconnect comments.
            // => include all of kcp's buffers and the unreliable queue!
            int total = kcp.rcv_queue.Count + kcp.snd_queue.Count +
                        kcp.rcv_buf.Count + kcp.snd_buf.Count;
            if (total >= QueueDisconnectThreshold)
            {
                Log.Warning($"KCP: disconnecting connection because it can't process data fast enough.\n" +
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

        // reads the next reliable message type & content from kcp.
        // -> to avoid buffering, unreliable messages call OnData directly.
        bool ReceiveNextReliable(out KcpHeader header, out ArraySegment<byte> message)
        {
            int msgSize = kcp.PeekSize();
            if (msgSize > 0)
            {
                // only allow receiving up to buffer sized messages.
                // otherwise we would get BlockCopy ArgumentException anyway.
                if (msgSize <= kcpMessageBuffer.Length)
                {
                    // receive from kcp
                    int received = kcp.Receive(kcpMessageBuffer, msgSize);
                    if (received >= 0)
                    {
                        // extract header & content without header
                        header = (KcpHeader)kcpMessageBuffer[0];
                        message = new ArraySegment<byte>(kcpMessageBuffer, 1, msgSize - 1);
                        lastReceiveTime = (uint)refTime.ElapsedMilliseconds;
                        return true;
                    }
                    else
                    {
                        // if receive failed, close everything
                        Log.Warning($"Receive failed with error={received}. closing connection.");
                        Disconnect();
                    }
                }
                // we don't allow sending messages > Max, so this must be an
                // attacker. let's disconnect to avoid allocation attacks etc.
                else
                {
                    Log.Warning($"KCP: possible allocation attack for msgSize {msgSize} > buffer {kcpMessageBuffer.Length}. Disconnecting the connection.");
                    Disconnect();
                }
            }

            header = KcpHeader.Disconnect;
            return false;
        }

        void TickIncoming_Connected(uint time)
        {
            // detect common events & ping
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            // any reliable kcp message received?
            if (ReceiveNextReliable(out KcpHeader header, out ArraySegment<byte> message))
            {
                // message type FSM. no default so we never miss a case.
                switch (header)
                {
                    case KcpHeader.Handshake:
                    {
                        // we were waiting for a handshake.
                        // it proves that the other end speaks our protocol.
                        Log.Info("KCP: received handshake");
                        state = KcpState.Authenticated;
                        OnAuthenticated?.Invoke();
                        break;
                    }
                    case KcpHeader.Ping:
                    {
                        // ping keeps kcp from timing out. do nothing.
                        break;
                    }
                    case KcpHeader.Data:
                    case KcpHeader.Disconnect:
                    {
                        // everything else is not allowed during handshake!
                        Log.Warning($"KCP: received invalid header {header} while Connected. Disconnecting the connection.");
                        Disconnect();
                        break;
                    }
                }
            }
        }

        void TickIncoming_Authenticated(uint time)
        {
            // detect common events & ping
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            // process all received messages
            //
            // Mirror scene changing requires transports to immediately stop
            // processing any more messages after a scene message was
            // received. and since we are in a while loop here, we need this
            // extra check.
            //
            // note while that this is mainly for Mirror, but might be
            // useful in other applications too.
            //
            // note that we check it BEFORE ever calling ReceiveNext. otherwise
            // we would silently eat the received message and never process it.
            while (!paused &&
                   ReceiveNextReliable(out KcpHeader header, out ArraySegment<byte> message))
            {
                // message type FSM. no default so we never miss a case.
                switch (header)
                {
                    case KcpHeader.Handshake:
                    {
                        // should never receive another handshake after auth
                        Log.Warning($"KCP: received invalid header {header} while Authenticated. Disconnecting the connection.");
                        Disconnect();
                        break;
                    }
                    case KcpHeader.Data:
                    {
                        // call OnData IF the message contained actual data
                        if (message.Count > 0)
                        {
                            //Log.Warning($"Kcp recv msg: {BitConverter.ToString(message.Array, message.Offset, message.Count)}");
                            OnData?.Invoke(message);
                        }
                        // empty data = attacker, or something went wrong
                        else
                        {
                            Log.Warning("KCP: received empty Data message while Authenticated. Disconnecting the connection.");
                            Disconnect();
                        }
                        break;
                    }
                    case KcpHeader.Ping:
                    {
                        // ping keeps kcp from timing out. do nothing.
                        break;
                    }
                    case KcpHeader.Disconnect:
                    {
                        // disconnect might happen
                        Log.Info("KCP: received disconnect message");
                        Disconnect();
                        break;
                    }
                }
            }
        }

        public void TickIncoming()
        {
            uint time = (uint)refTime.ElapsedMilliseconds;

            try
            {
                switch (state)
                {
                    case KcpState.Connected:
                    {
                        TickIncoming_Connected(time);
                        break;
                    }
                    case KcpState.Authenticated:
                    {
                        TickIncoming_Authenticated(time);
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
                Log.Info($"KCP Connection: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                // fine, socket was closed
                Log.Info($"KCP Connection: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (Exception ex)
            {
                // unexpected
                Log.Error(ex.ToString());
                Disconnect();
            }
        }

        public void TickOutgoing()
        {
            uint time = (uint)refTime.ElapsedMilliseconds;

            try
            {
                switch (state)
                {
                    case KcpState.Connected:
                    case KcpState.Authenticated:
                    {
                        // update flushes out messages
                        kcp.Update(time);
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
                Log.Info($"KCP Connection: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                // fine, socket was closed
                Log.Info($"KCP Connection: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (Exception ex)
            {
                // unexpected
                Log.Error(ex.ToString());
                Disconnect();
            }
        }

        public void RawInput(byte[] buffer, int msgLength)
        {
            // parse channel
            if (msgLength > 0)
            {
                byte channel = buffer[0];
                switch (channel)
                {
                    case (byte)KcpChannel.Reliable:
                    {
                        // input into kcp, but skip channel byte
                        int input = kcp.Input(buffer, 1, msgLength - 1);
                        if (input != 0)
                        {
                            Log.Warning($"Input failed with error={input} for buffer with length={msgLength - 1}");
                        }
                        break;
                    }
                    case (byte)KcpChannel.Unreliable:
                    {
                        // ideally we would queue all unreliable messages and
                        // then process them in ReceiveNext() together with the
                        // reliable messages, but:
                        // -> queues/allocations/pools are slow and complex.
                        // -> DOTSNET 10k is actually slower if we use pooled
                        //    unreliable messages for transform messages.
                        //
                        //      DOTSNET 10k benchmark:
                        //        reliable-only:         170 FPS
                        //        unreliable queued: 130-150 FPS
                        //        unreliable direct:     183 FPS(!)
                        //
                        //      DOTSNET 50k benchmark:
                        //        reliable-only:         FAILS (queues keep growing)
                        //        unreliable direct:     18-22 FPS(!)
                        //
                        // -> all unreliable messages are DATA messages anyway.
                        // -> let's skip the magic and call OnData directly if
                        //    the current state allows it.
                        if (state == KcpState.Authenticated)
                        {
                            // only process messages while not paused for Mirror
                            // scene switching etc.
                            // -> if an unreliable message comes in while
                            //    paused, simply drop it. it's unreliable!
                            if (!paused)
                            {
                                ArraySegment<byte> message = new ArraySegment<byte>(buffer, 1, msgLength - 1);
                                OnData?.Invoke(message);
                            }

                            // set last receive time to avoid timeout.
                            // -> we do this in ANY case even if not enabled.
                            //    a message is a message.
                            // -> we set last receive time for both reliable and
                            //    unreliable messages. both count.
                            //    otherwise a connection might time out even
                            //    though unreliable were received, but no
                            //    reliable was received.
                            lastReceiveTime = (uint)refTime.ElapsedMilliseconds;
                        }
                        else
                        {
                            // should never
                            Log.Warning($"KCP: received unreliable message in state {state}. Disconnecting the connection.");
                            Disconnect();
                        }
                        break;
                    }
                    default:
                    {
                        // not a valid channel. random data or attacks.
                        Log.Info($"Disconnecting connection because of invalid channel header: {channel}");
                        Disconnect();
                        break;
                    }
                }
            }
        }

        // raw send puts the data into the socket
        protected abstract void RawSend(byte[] data, int length);

        // raw send called by kcp
        void RawSendReliable(byte[] data, int length)
        {
            // copy channel header, data into raw send buffer, then send
            rawSendBuffer[0] = (byte)KcpChannel.Reliable;
            Buffer.BlockCopy(data, 0, rawSendBuffer, 1, length);
            RawSend(rawSendBuffer, length + 1);
        }

        void SendReliable(KcpHeader header, ArraySegment<byte> content)
        {
            // 1 byte header + content needs to fit into send buffer
            if (1 + content.Count <= kcpSendBuffer.Length) // TODO
            {
                // copy header, content (if any) into send buffer
                kcpSendBuffer[0] = (byte)header;
                if (content.Count > 0)
                    Buffer.BlockCopy(content.Array, content.Offset, kcpSendBuffer, 1, content.Count);

                // send to kcp for processing
                int sent = kcp.Send(kcpSendBuffer, 0, 1 + content.Count);
                if (sent < 0)
                {
                    Log.Warning($"Send failed with error={sent} for content with length={content.Count}");
                }
            }
            // otherwise content is larger than MaxMessageSize. let user know!
            else Log.Error($"Failed to send reliable message of size {content.Count} because it's larger than ReliableMaxMessageSize={ReliableMaxMessageSize}");
        }

        void SendUnreliable(ArraySegment<byte> message)
        {
            // message size needs to be <= unreliable max size
            if (message.Count <= UnreliableMaxMessageSize)
            {
                // copy channel header, data into raw send buffer, then send
                rawSendBuffer[0] = (byte)KcpChannel.Unreliable;
                Buffer.BlockCopy(message.Array, 0, rawSendBuffer, 1, message.Count);
                RawSend(rawSendBuffer, message.Count + 1);
            }
            // otherwise content is larger than MaxMessageSize. let user know!
            else Log.Error($"Failed to send unreliable message of size {message.Count} because it's larger than UnreliableMaxMessageSize={UnreliableMaxMessageSize}");
        }

        // server & client need to send handshake at different times, so we need
        // to expose the function.
        // * client should send it immediately.
        // * server should send it as reply to client's handshake, not before
        //   (server should not reply to random internet messages with handshake)
        // => handshake info needs to be delivered, so it goes over reliable.
        public void SendHandshake()
        {
            Log.Info("KcpConnection: sending Handshake to other end!");
            SendReliable(KcpHeader.Handshake, default);
        }

        public void SendData(ArraySegment<byte> data, KcpChannel channel)
        {
            // sending empty segments is not allowed.
            // nobody should ever try to send empty data.
            // it means that something went wrong, e.g. in Mirror/DOTSNET.
            // let's make it obvious so it's easy to debug.
            if (data.Count == 0)
            {
                Log.Warning("KcpConnection: tried sending empty message. This should never happen. Disconnecting.");
                Disconnect();
                return;
            }

            switch (channel)
            {
                case KcpChannel.Reliable:
                    SendReliable(KcpHeader.Data, data);
                    break;
                case KcpChannel.Unreliable:
                    SendUnreliable(data);
                    break;
            }
        }

        // ping goes through kcp to keep it from timing out, so it goes over the
        // reliable channel.
        void SendPing() => SendReliable(KcpHeader.Ping, default);

        // disconnect info needs to be delivered, so it goes over reliable
        void SendDisconnect() => SendReliable(KcpHeader.Disconnect, default);

        protected virtual void Dispose() {}

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
                    SendDisconnect();
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
            Log.Info("KCP Connection: Disconnected.");
            state = KcpState.Disconnected;
            OnDisconnected?.Invoke();
        }

        // get remote endpoint
        public EndPoint GetRemoteEndPoint() => remoteEndpoint;

        // pause/unpause to safely support mirror scene handling and to
        // immediately pause the receive while loop if needed.
        public void Pause() => paused = true;
        public void Unpause()
        {
            // unpause
            paused = false;

            // reset the timeout.
            // we have likely been paused for > timeout seconds, but that
            // doesn't mean we should disconnect. for example, Mirror pauses
            // kcp during scene changes which could easily take > 10s timeout:
            //   see also: https://github.com/vis2k/kcp2k/issues/8
            // => Unpause completely resets the timeout instead of restoring the
            //    time difference when we started pausing. it's more simple and
            //    it's a good idea to start counting from 0 after we unpaused!
            lastReceiveTime = (uint)refTime.ElapsedMilliseconds;
        }
    }
}
