// kcp client logic abstracted into a class.
// for use in Mirror, DOTSNET, testing, etc.
using System;
using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpClient : KcpPeer
    {
        // IO
        protected Socket socket;
        public EndPoint remoteEndPoint;

        // expose local endpoint for users / relays / nat traversal etc.
        public EndPoint LocalEndPoint => socket?.LocalEndPoint;

        // config
        protected readonly KcpConfig config;

        // raw receive buffer always needs to be of 'MTU' size, even if
        // MaxMessageSize is larger. kcp always sends in MTU segments and having
        // a buffer smaller than MTU would silently drop excess data.
        // => we need the MTU to fit channel + message!
        // => protected because someone may overwrite RawReceive but still wants
        //    to reuse the buffer.
        protected readonly byte[] rawReceiveBuffer;

        // callbacks
        // even for errors, to allow liraries to show popups etc.
        // instead of logging directly.
        // (string instead of Exception for ease of use and to avoid user panic)
        //
        // events are readonly, set in constructor.
        // this ensures they are always initialized when used.
        // fixes https://github.com/MirrorNetworking/Mirror/issues/3337 and more
        protected readonly Action OnConnectedCallback;
        protected readonly Action<ArraySegment<byte>, KcpChannel> OnDataCallback;
        protected readonly Action OnDisconnectedCallback;
        protected readonly Action<ErrorCode, string> OnErrorCallback;

        // state
        bool active = false; // active between when connect() and disconnect() are called
        public bool connected;

        public KcpClient(Action OnConnected,
                         Action<ArraySegment<byte>, KcpChannel> OnData,
                         Action OnDisconnected,
                         Action<ErrorCode, string> OnError,
                         KcpConfig config)
                         : base(config, 0) // client has no cookie yet
        {
            // initialize callbacks first to ensure they can be used safely.
            OnConnectedCallback = OnConnected;
            OnDataCallback = OnData;
            OnDisconnectedCallback = OnDisconnected;
            OnErrorCallback = OnError;
            this.config = config;

            // create mtu sized receive buffer
            rawReceiveBuffer = new byte[config.Mtu];
        }

        // callbacks ///////////////////////////////////////////////////////////
        // some callbacks need to wrapped with some extra logic
        protected override void OnAuthenticated()
        {
            Log.Info($"[KCP] Client: OnConnected");
            connected = true;
            OnConnectedCallback();
        }

        protected override void OnData(ArraySegment<byte> message, KcpChannel channel) =>
            OnDataCallback(message, channel);

        protected override void OnError(ErrorCode error, string message) =>
            OnErrorCallback(error, message);

        protected override void OnDisconnected()
        {
            Log.Info($"[KCP] Client: OnDisconnected");
            connected = false;
            socket?.Close();
            socket = null;
            remoteEndPoint = null;
            OnDisconnectedCallback();
            active = false;
        }

        ////////////////////////////////////////////////////////////////////////
        public void Connect(string address, ushort port)
        {
            if (connected)
            {
                Log.Warning("[KCP] Client: already connected!");
                return;
            }

            // resolve host name before creating peer.
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3361
            if (!Common.ResolveHostname(address, out IPAddress[] addresses))
            {
                // pass error to user callback. no need to log it manually.
                OnError(ErrorCode.DnsResolve, $"Failed to resolve host: {address}");
                OnDisconnectedCallback();
                return;
            }

            // create fresh peer for each new session
            // client doesn't need secure cookie.
            Reset(config);

            Log.Info($"[KCP] Client: connect to {address}:{port}");

            // create socket
            remoteEndPoint = new IPEndPoint(addresses[0], port);
            socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            active = true;

            // recv & send are called from main thread.
            // need to ensure this never blocks.
            // even a 1ms block per connection would stop us from scaling.
            socket.Blocking = false;

            // configure buffer sizes
            Common.ConfigureSocketBuffers(socket, config.RecvBufferSize, config.SendBufferSize);

            // bind to endpoint so we can use send/recv instead of sendto/recvfrom.
            socket.Connect(remoteEndPoint);

            // immediately send a hello message to the server.
            // server will call OnMessage and add the new connection.
            // note that this still has cookie=0 until we receive the server's hello.
            SendHello();
        }

        // io - input.
        // virtual so it may be modified for relays, etc.
        // call this while it returns true, to process all messages this tick.
        // returned ArraySegment is valid until next call to RawReceive.
        protected virtual bool RawReceive(out ArraySegment<byte> segment)
        {
            segment = default;
            if (socket == null) return false;

            try
            {
                return socket.ReceiveNonBlocking(rawReceiveBuffer, out segment);
            }
            // for non-blocking sockets, Receive throws WouldBlock if there is
            // no message to read. that's okay. only log for other errors.
            catch (SocketException e)
            {
                // the other end closing the connection is not an 'error'.
                // but connections should never just end silently.
                // at least log a message for easier debugging.
                // for example, his can happen when connecting without a server.
                // see test: ConnectWithoutServer().
                Log.Info($"[KCP] Client.RawReceive: looks like the other end has closed the connection. This is fine: {e}");
                base.Disconnect();
                return false;
            }
        }

        // io - output.
        // virtual so it may be modified for relays, etc.
        protected override void RawSend(ArraySegment<byte> data)
        {
            // only if socket was connected / created yet.
            // users may call send functions without having connected, causing NRE.
            if (socket == null) return;

            try
            {
                socket.SendNonBlocking(data);
            }
            catch (SocketException e)
            {
                // SendDisconnect() sometimes gets a SocketException with
                // 'Connection Refused' if the other end already closed.
                // this is not an 'error', it's expected to happen.
                // but connections should never just end silently.
                // at least log a message for easier debugging.
                Log.Info($"[KCP] Client.RawSend: looks like the other end has closed the connection. This is fine: {e}");
                // base.Disconnect(); <- don't call this, would deadlock if SendDisconnect() already throws
            }
        }

        public void Send(ArraySegment<byte> segment, KcpChannel channel)
        {
            if (!connected)
            {
                Log.Warning("[KCP] Client: can't send because not connected!");
                return;
            }

            SendData(segment, channel);
        }

        // insert raw IO. usually from socket.Receive.
        // offset is useful for relays, where we may parse a header and then
        // feed the rest to kcp.
        public void RawInput(ArraySegment<byte> segment)
        {
            // ensure valid size: at least 1 byte for channel + 4 bytes for cookie
            if (segment.Count <= 5) return;

            // parse channel
            // byte channel = segment[0]; ArraySegment[i] isn't supported in some older Unity Mono versions
            byte channel = segment.Array[segment.Offset + 0];

            // server messages always contain the security cookie.
            // parse it, assign if not assigned, warn if suddenly different.
            Utils.Decode32U(segment.Array, segment.Offset + 1, out uint messageCookie);
            if (messageCookie == 0)
            {
                Log.Error($"[KCP] Client: received message with cookie=0, this should never happen. Server should always include the security cookie.");
            }

            if (cookie == 0)
            {
                cookie = messageCookie;
                Log.Info($"[KCP] Client: received initial cookie: {cookie}");
            }
            else if (cookie != messageCookie)
            {
                Log.Warning($"[KCP] Client: dropping message with mismatching cookie: {messageCookie} expected: {cookie}.");
                return;
            }

            // parse message
            ArraySegment<byte> message = new ArraySegment<byte>(segment.Array, segment.Offset + 1+4, segment.Count - 1-4);

            switch (channel)
            {
                case (byte)KcpChannel.Reliable:
                {
                    OnRawInputReliable(message);
                    break;
                }
                case (byte)KcpChannel.Unreliable:
                {
                    OnRawInputUnreliable(message);
                    break;
                }
                default:
                {
                    // invalid channel indicates random internet noise.
                    // servers may receive random UDP data.
                    // just ignore it, but log for easier debugging.
                    Log.Warning($"[KCP] Client: invalid channel header: {channel}, likely internet noise");
                    break;
                }
            }
        }

        // process incoming messages. should be called before updating the world.
        // virtual because relay may need to inject their own ping or similar.
        public override void TickIncoming()
        {
            // recv on socket first, then process incoming
            // (even if we didn't receive anything. need to tick ping etc.)
            // (connection is null if not active)
            if (active)
            {
                while (RawReceive(out ArraySegment<byte> segment))
                    RawInput(segment);
            }

            // RawReceive may have disconnected peer. active check again.
            if (active) base.TickIncoming();
        }

        // process outgoing messages. should be called after updating the world.
        // virtual because relay may need to inject their own ping or similar.
        public override void TickOutgoing()
        {
            // process outgoing while active
            if (active) base.TickOutgoing();
        }

        // process incoming and outgoing for convenience
        // => ideally call ProcessIncoming() before updating the world and
        //    ProcessOutgoing() after updating the world for minimum latency
        public virtual void Tick()
        {
            TickIncoming();
            TickOutgoing();
        }
    }
}
