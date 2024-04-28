// Kcp Peer, similar to UDP Peer but wrapped with reliability, channels,
// timeouts, authentication, state, etc.
//
// still IO agnostic to work with udp, nonalloc, relays, native, etc.
using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace kcp2k
{
    public abstract class KcpPeer
    {
        // kcp reliability algorithm
        internal Kcp kcp;

        // security cookie to prevent UDP spoofing.
        // credits to IncludeSec for disclosing the issue.
        //
        // server passes the expected cookie to the client's KcpPeer.
        // KcpPeer sends cookie to the connected client.
        // KcpPeer only accepts packets which contain the cookie.
        // => cookie can be a random number, but it needs to be cryptographically
        //    secure random that can't be easily predicted.
        // => cookie can be hash(ip, port) BUT only if salted to be not predictable
        internal uint cookie;

        // state: connected as soon as we create the peer.
        // leftover from KcpConnection. remove it after refactoring later.
        protected KcpState state = KcpState.Connected;

        // If we don't receive anything these many milliseconds
        // then consider us disconnected
        public const int DEFAULT_TIMEOUT = 10000;
        public int timeout;
        uint lastReceiveTime;

        // internal time.
        // StopWatch offers ElapsedMilliSeconds and should be more precise than
        // Unity's time.deltaTime over long periods.
        readonly Stopwatch watch = new Stopwatch();

        // buffer to receive kcp's processed messages (avoids allocations).
        // IMPORTANT: this is for KCP messages. so it needs to be of size:
        //            1 byte header + MaxMessageSize content
        readonly byte[] kcpMessageBuffer;// = new byte[1 + ReliableMaxMessageSize];

        // send buffer for handing user messages to kcp for processing.
        // (avoids allocations).
        // IMPORTANT: needs to be of size:
        //            1 byte header + MaxMessageSize content
        readonly byte[] kcpSendBuffer;// = new byte[1 + ReliableMaxMessageSize];

        // raw send buffer is exactly MTU.
        readonly byte[] rawSendBuffer;

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
        public int SendQueueCount     => kcp.snd_queue.Count;
        public int ReceiveQueueCount  => kcp.rcv_queue.Count;
        public int SendBufferCount    => kcp.snd_buf.Count;
        public int ReceiveBufferCount => kcp.rcv_buf.Count;

        // we need to subtract the channel and cookie bytes from every
        // MaxMessageSize calculation.
        // we also need to tell kcp to use MTU-1 to leave space for the byte.
        public const int CHANNEL_HEADER_SIZE = 1;
        public const int COOKIE_HEADER_SIZE = 4;
        public const int METADATA_SIZE = CHANNEL_HEADER_SIZE + COOKIE_HEADER_SIZE;

        // reliable channel (= kcp) MaxMessageSize so the outside knows largest
        // allowed message to send. the calculation in Send() is not obvious at
        // all, so let's provide the helper here.
        //
        // kcp does fragmentation, so max message is way larger than MTU.
        //
        // -> runtime MTU changes are disabled: mss is always MTU_DEF-OVERHEAD
        // -> Send() checks if fragment count < rcv_wnd, so we use rcv_wnd - 1.
        //    NOTE that original kcp has a bug where WND_RCV default is used
        //    instead of configured rcv_wnd, limiting max message size to 144 KB
        //    https://github.com/skywind3000/kcp/pull/291
        //    we fixed this in kcp2k.
        // -> we add 1 byte KcpHeader enum to each message, so -1
        //
        // IMPORTANT: max message is MTU * rcv_wnd, in other words it completely
        //            fills the receive window! due to head of line blocking,
        //            all other messages have to wait while a maxed size message
        //            is being delivered.
        //            => in other words, DO NOT use max size all the time like
        //               for batching.
        //            => sending UNRELIABLE max message size most of the time is
        //               best for performance (use that one for batching!)
        static int ReliableMaxMessageSize_Unconstrained(int mtu, uint rcv_wnd) =>
            (mtu - Kcp.OVERHEAD - METADATA_SIZE) * ((int)rcv_wnd - 1) - 1;

        // kcp encodes 'frg' as 1 byte.
        // max message size can only ever allow up to 255 fragments.
        //   WND_RCV gives 127 fragments.
        //   WND_RCV * 2 gives 255 fragments.
        // so we can limit max message size by limiting rcv_wnd parameter.
        public static int ReliableMaxMessageSize(int mtu, uint rcv_wnd) =>
            ReliableMaxMessageSize_Unconstrained(mtu, Math.Min(rcv_wnd, Kcp.FRG_MAX));

        // unreliable max message size is simply MTU - channel header - kcp header
        public static int UnreliableMaxMessageSize(int mtu) =>
            mtu - METADATA_SIZE - 1;

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
        public uint MaxSendRate    => kcp.snd_wnd * kcp.mtu * 1000 / kcp.interval;
        public uint MaxReceiveRate => kcp.rcv_wnd * kcp.mtu * 1000 / kcp.interval;

        // calculate max message sizes based on mtu and wnd only once
        public readonly int unreliableMax;
        public readonly int reliableMax;

        // SetupKcp creates and configures a new KCP instance.
        // => useful to start from a fresh state every time the client connects
        // => NoDelay, interval, wnd size are the most important configurations.
        //    let's force require the parameters so we don't forget it anywhere.
        protected KcpPeer(KcpConfig config, uint cookie)
        {
            // initialize variable state in extra function so we can reuse it
            // when reconnecting to reset state
            Reset(config);

            // set the cookie after resetting state so it's not overwritten again.
            // with log message for debugging in case of cookie issues.
            this.cookie = cookie;
            Log.Info($"[KCP] {GetType()}: created with cookie={cookie}");

            // create mtu sized send buffer
            rawSendBuffer = new byte[config.Mtu];

            // calculate max message sizes once
            unreliableMax = UnreliableMaxMessageSize(config.Mtu);
            reliableMax = ReliableMaxMessageSize(config.Mtu, config.ReceiveWindowSize);

            // create message buffers AFTER window size is set
            // see comments on buffer definition for the "+1" part
            kcpMessageBuffer = new byte[1 + reliableMax];
            kcpSendBuffer    = new byte[1 + reliableMax];
        }

        // Reset all state once.
        // useful for KcpClient to reconned with a fresh kcp state.
        protected void Reset(KcpConfig config)
        {
            // reset state
            cookie = 0;
            state = KcpState.Connected;
            lastReceiveTime = 0;
            lastPingTime = 0;
            watch.Restart(); // start at 0 each time

            // set up kcp over reliable channel (that's what kcp is for)
            kcp = new Kcp(0, RawSendReliable);

            // set nodelay.
            // note that kcp uses 'nocwnd' internally so we negate the parameter
            kcp.SetNoDelay(config.NoDelay ? 1u : 0u, config.Interval, config.FastResend, !config.CongestionWindow);
            kcp.SetWindowSize(config.SendWindowSize, config.ReceiveWindowSize);

            // IMPORTANT: high level needs to add 1 channel byte to each raw
            // message. so while Kcp.MTU_DEF is perfect, we actually need to
            // tell kcp to use MTU-1 so we can still put the header into the
            // message afterwards.
            kcp.SetMtu((uint)config.Mtu - METADATA_SIZE);

            // set maximum retransmits (aka dead_link)
            kcp.dead_link = config.MaxRetransmits;
            timeout = config.Timeout;
        }

        // callbacks ///////////////////////////////////////////////////////////
        // events are abstract, guaranteed to be implemented.
        // this ensures they are always initialized when used.
        // fixes https://github.com/MirrorNetworking/Mirror/issues/3337 and more
        protected abstract void OnAuthenticated();
        protected abstract void OnData(ArraySegment<byte> message, KcpChannel channel);
        protected abstract void OnDisconnected();

        // error callback instead of logging.
        // allows libraries to show popups etc.
        // (string instead of Exception for ease of use and to avoid user panic)
        protected abstract void OnError(ErrorCode error, string message);
        protected abstract void RawSend(ArraySegment<byte> data);

        ////////////////////////////////////////////////////////////////////////

        void HandleTimeout(uint time)
        {
            // note: we are also sending a ping regularly, so timeout should
            //       only ever happen if the connection is truly gone.
            if (time >= lastReceiveTime + timeout)
            {
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.Timeout, $"{GetType()}: Connection timed out after not receiving any message for {timeout}ms. Disconnecting.");
                Disconnect();
            }
        }

        void HandleDeadLink()
        {
            // kcp has 'dead_link' detection. might as well use it.
            if (kcp.state == -1)
            {
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.Timeout, $"{GetType()}: dead_link detected: a message was retransmitted {kcp.dead_link} times without ack. Disconnecting.");
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
                //Log.Debug("[KCP] sending ping...");
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
                        kcp.rcv_buf.Count   + kcp.snd_buf.Count;
            if (total >= QueueDisconnectThreshold)
            {
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.Congestion,
                        $"{GetType()}: disconnecting connection because it can't process data fast enough.\n" +
                        $"Queue total {total}>{QueueDisconnectThreshold}. rcv_queue={kcp.rcv_queue.Count} snd_queue={kcp.snd_queue.Count} rcv_buf={kcp.rcv_buf.Count} snd_buf={kcp.snd_buf.Count}\n" +
                        $"* Try to Enable NoDelay, decrease INTERVAL, disable Congestion Window (= enable NOCWND!), increase SEND/RECV WINDOW or compress data.\n" +
                        $"* Or perhaps the network is simply too slow on our end, or on the other end.");

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
        bool ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message)
        {
            message = default;
            header = KcpHeaderReliable.Ping;

            int msgSize = kcp.PeekSize();
            if (msgSize <= 0) return false;

            // only allow receiving up to buffer sized messages.
            // otherwise we would get BlockCopy ArgumentException anyway.
            if (msgSize > kcpMessageBuffer.Length)
            {
                // we don't allow sending messages > Max, so this must be an
                // attacker. let's disconnect to avoid allocation attacks etc.
                // pass error to user callback. no need to log it manually.
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: possible allocation attack for msgSize {msgSize} > buffer {kcpMessageBuffer.Length}. Disconnecting the connection.");
                Disconnect();
                return false;
            }

            // receive from kcp
            int received = kcp.Receive(kcpMessageBuffer, msgSize);
            if (received < 0)
            {
                // if receive failed, close everything
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive failed with error={received}. closing connection.");
                Disconnect();
                return false;
            }

            // safely extract header. attackers may send values out of enum range.
            byte headerByte = kcpMessageBuffer[0];
            if (!KcpHeader.ParseReliable(headerByte, out header))
            {
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive failed to parse header: {headerByte} is not defined in {typeof(KcpHeaderReliable)}.");
                Disconnect();
                return false;
            }

            // extract content without header
            message = new ArraySegment<byte>(kcpMessageBuffer, 1, msgSize - 1);
            lastReceiveTime = (uint)watch.ElapsedMilliseconds;
            return true;
        }

        void TickIncoming_Connected(uint time)
        {
            // detect common events & ping
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            // any reliable kcp message received?
            if (ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message))
            {
                // message type FSM. no default so we never miss a case.
                switch (header)
                {
                    case KcpHeaderReliable.Hello:
                    {
                        // we were waiting for a Hello message.
                        // it proves that the other end speaks our protocol.

                        // log with previously parsed cookie
                        Log.Info($"[KCP] {GetType()}: received hello with cookie={cookie}");
                        state = KcpState.Authenticated;
                        OnAuthenticated();
                        break;
                    }
                    case KcpHeaderReliable.Ping:
                    {
                        // ping keeps kcp from timing out. do nothing.
                        break;
                    }
                    case KcpHeaderReliable.Data:
                    {
                        // everything else is not allowed during handshake!
                        // pass error to user callback. no need to log it manually.
                        // GetType() shows Server/ClientConn instead of just Connection.
                        OnError(ErrorCode.InvalidReceive, $"[KCP] {GetType()}: received invalid header {header} while Connected. Disconnecting the connection.");
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
            while (ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message))
            {
                // message type FSM. no default so we never miss a case.
                switch (header)
                {
                    case KcpHeaderReliable.Hello:
                    {
                        // should never receive another hello after auth
                        // GetType() shows Server/ClientConn instead of just Connection.
                        Log.Warning($"{GetType()}: received invalid header {header} while Authenticated. Disconnecting the connection.");
                        Disconnect();
                        break;
                    }
                    case KcpHeaderReliable.Data:
                    {
                        // call OnData IF the message contained actual data
                        if (message.Count > 0)
                        {
                            //Log.Warning($"Kcp recv msg: {BitConverter.ToString(message.Array, message.Offset, message.Count)}");
                            OnData(message, KcpChannel.Reliable);
                        }
                        // empty data = attacker, or something went wrong
                        else
                        {
                            // pass error to user callback. no need to log it manually.
                            // GetType() shows Server/ClientConn instead of just Connection.
                            OnError(ErrorCode.InvalidReceive, $"{GetType()}: received empty Data message while Authenticated. Disconnecting the connection.");
                            Disconnect();
                        }
                        break;
                    }
                    case KcpHeaderReliable.Ping:
                    {
                        // ping keeps kcp from timing out. do nothing.
                        break;
                    }
                }
            }
        }

        public virtual void TickIncoming()
        {
            uint time = (uint)watch.ElapsedMilliseconds;

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
            // TODO KcpConnection is IO agnostic. move this to outside later.
            catch (SocketException exception)
            {
                // this is ok, the connection was closed
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                // fine, socket was closed
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (Exception exception)
            {
                // unexpected
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.Unexpected, $"{GetType()}: unexpected Exception: {exception}");
                Disconnect();
            }
        }

        public virtual void TickOutgoing()
        {
            uint time = (uint)watch.ElapsedMilliseconds;

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
            // TODO KcpConnection is IO agnostic. move this to outside later.
            catch (SocketException exception)
            {
                // this is ok, the connection was closed
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                // fine, socket was closed
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: Disconnecting because {exception}. This is fine.");
                Disconnect();
            }
            catch (Exception exception)
            {
                // unexpected
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.Unexpected, $"{GetType()}: unexpected exception: {exception}");
                Disconnect();
            }
        }

        protected void OnRawInputReliable(ArraySegment<byte> message)
        {
            // input into kcp, but skip channel byte
            int input = kcp.Input(message.Array, message.Offset, message.Count);
            if (input != 0)
            {
                // GetType() shows Server/ClientConn instead of just Connection.
                Log.Warning($"[KCP] {GetType()}: Input failed with error={input} for buffer with length={message.Count - 1}");
            }
        }

        protected void OnRawInputUnreliable(ArraySegment<byte> message)
        {
            // need at least one byte for the KcpHeader enum
            if (message.Count < 1) return;

            // safely extract header. attackers may send values out of enum range.
            byte headerByte = message.Array[message.Offset + 0];
            if (!KcpHeader.ParseUnreliable(headerByte, out KcpHeaderUnreliable header))
            {
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive failed to parse header: {headerByte} is not defined in {typeof(KcpHeaderUnreliable)}.");
                Disconnect();
                return;
            }

            // subtract header from message content
            // (above we already ensure it's at least 1 byte long)
            message = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);

            switch (header)
            {
                case KcpHeaderUnreliable.Data:
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
                        OnData(message, KcpChannel.Unreliable);

                        // set last receive time to avoid timeout.
                        // -> we do this in ANY case even if not enabled.
                        //    a message is a message.
                        // -> we set last receive time for both reliable and
                        //    unreliable messages. both count.
                        //    otherwise a connection might time out even
                        //    though unreliable were received, but no
                        //    reliable was received.
                        lastReceiveTime = (uint)watch.ElapsedMilliseconds;
                    }
                    else
                    {
                        // it's common to receive unreliable messages before being
                        // authenticated, for example:
                        // - random internet noise
                        // - game server may send an unreliable message after authenticating,
                        //   and the unreliable message arrives on the client before the
                        //   'auth_ok' message. this can be avoided by sending a final
                        //   'ready' message after being authenticated, but this would
                        //   add another 'round trip time' of latency to the handshake.
                        //
                        // it's best to simply ignore invalid unreliable messages here.
                        // Log.Info($"{GetType()}: received unreliable message while not authenticated.");
                    }
                    break;
                }
                case KcpHeaderUnreliable.Disconnect:
                {
                    // GetType() shows Server/ClientConn instead of just Connection.
                    Log.Info($"[KCP] {GetType()}: received disconnect message");
                    Disconnect();
                    break;
                }
            }
        }

        // raw send called by kcp
        void RawSendReliable(byte[] data, int length)
        {
            // write channel header
            // from 0, with 1 byte
            rawSendBuffer[0] = (byte)KcpChannel.Reliable;

            // write handshake cookie to protect against UDP spoofing.
            // from 1, with 4 bytes
            Utils.Encode32U(rawSendBuffer, 1, cookie); // allocation free

            // write data
            // from 5, with N bytes
            Buffer.BlockCopy(data, 0, rawSendBuffer, 1+4, length);

            // IO send
            ArraySegment<byte> segment = new ArraySegment<byte>(rawSendBuffer, 0, length + 1+4);
            RawSend(segment);
        }

        void SendReliable(KcpHeaderReliable header, ArraySegment<byte> content)
        {
            // 1 byte header + content needs to fit into send buffer
            if (1 + content.Count > kcpSendBuffer.Length) // TODO
            {
                // otherwise content is larger than MaxMessageSize. let user know!
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.InvalidSend, $"{GetType()}: Failed to send reliable message of size {content.Count} because it's larger than ReliableMaxMessageSize={reliableMax}");
                return;
            }

            // write channel header
            kcpSendBuffer[0] = (byte)header;

            // write data (if any)
            if (content.Count > 0)
                Buffer.BlockCopy(content.Array, content.Offset, kcpSendBuffer, 1, content.Count);

            // send to kcp for processing
            int sent = kcp.Send(kcpSendBuffer, 0, 1 + content.Count);
            if (sent < 0)
            {
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.InvalidSend, $"{GetType()}: Send failed with error={sent} for content with length={content.Count}");
            }
        }

        void SendUnreliable(KcpHeaderUnreliable header, ArraySegment<byte> content)
        {
            // message size needs to be <= unreliable max size
            if (content.Count > unreliableMax)
            {
                // otherwise content is larger than MaxMessageSize. let user know!
                // GetType() shows Server/ClientConn instead of just Connection.
                Log.Error($"[KCP] {GetType()}: Failed to send unreliable message of size {content.Count} because it's larger than UnreliableMaxMessageSize={unreliableMax}");
                return;
            }

            // write channel header
            // from 0, with 1 byte
            rawSendBuffer[0] = (byte)KcpChannel.Unreliable;

            // write handshake cookie to protect against UDP spoofing.
            // from 1, with 4 bytes
            Utils.Encode32U(rawSendBuffer, 1, cookie); // allocation free

            // write kcp header
            rawSendBuffer[5] = (byte)header;

            // write data (if any)
            // from 6, with N bytes
            if (content.Count > 0)
                Buffer.BlockCopy(content.Array, content.Offset, rawSendBuffer, 1 + 4 + 1, content.Count);

            // IO send
            ArraySegment<byte> segment = new ArraySegment<byte>(rawSendBuffer, 0, content.Count + 1 + 4 + 1);
            RawSend(segment);
        }

        // server & client need to send handshake at different times, so we need
        // to expose the function.
        // * client should send it immediately.
        // * server should send it as reply to client's handshake, not before
        //   (server should not reply to random internet messages with handshake)
        // => handshake info needs to be delivered, so it goes over reliable.
        public void SendHello()
        {
            // send an empty message with 'Hello' header.
            // cookie is automatically included in all messages.

            // GetType() shows Server/ClientConn instead of just Connection.
            Log.Info($"[KCP] {GetType()}: sending handshake to other end with cookie={cookie}");
            SendReliable(KcpHeaderReliable.Hello, default);
        }

        public void SendData(ArraySegment<byte> data, KcpChannel channel)
        {
            // sending empty segments is not allowed.
            // nobody should ever try to send empty data.
            // it means that something went wrong, e.g. in Mirror/DOTSNET.
            // let's make it obvious so it's easy to debug.
            if (data.Count == 0)
            {
                // pass error to user callback. no need to log it manually.
                // GetType() shows Server/ClientConn instead of just Connection.
                OnError(ErrorCode.InvalidSend, $"{GetType()}: tried sending empty message. This should never happen. Disconnecting.");
                Disconnect();
                return;
            }

            switch (channel)
            {
                case KcpChannel.Reliable:
                    SendReliable(KcpHeaderReliable.Data, data);
                    break;
                case KcpChannel.Unreliable:
                    SendUnreliable(KcpHeaderUnreliable.Data, data);
                    break;
            }
        }

        // ping goes through kcp to keep it from timing out, so it goes over the
        // reliable channel.
        void SendPing() => SendReliable(KcpHeaderReliable.Ping, default);

        // send disconnect message
        void SendDisconnect()
        {
            // sending over reliable to ensure delivery seems like a good idea:
            // but if we close the connection immediately, it often doesn't get
            // fully delivered: https://github.com/MirrorNetworking/Mirror/issues/3591
            //   SendReliable(KcpHeader.Disconnect, default);
            //
            // instead, rapid fire a few unreliable messages.
            // they are sent immediately even if we close the connection after.
            // this way we don't need to keep the connection alive for a while.
            // (glenn fiedler method)
            for (int i = 0; i < 5; ++i)
                SendUnreliable(KcpHeaderUnreliable.Disconnect, default);
        }

        // disconnect this connection
        public virtual void Disconnect()
        {
            // only if not disconnected yet
            if (state == KcpState.Disconnected)
                return;

            // send a disconnect message
            try
            {
                SendDisconnect();
            }
            // TODO KcpConnection is IO agnostic. move this to outside later.
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

            // set as Disconnected, call event
            // GetType() shows Server/ClientConn instead of just Connection.
            Log.Info($"[KCP] {GetType()}: Disconnected.");
            state = KcpState.Disconnected;
            OnDisconnected();
        }
    }
}
