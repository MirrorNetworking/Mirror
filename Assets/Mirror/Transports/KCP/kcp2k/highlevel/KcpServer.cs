// kcp server logic abstracted into a class.
// for use in Mirror, DOTSNET, testing, etc.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpServer
    {
        // callbacks
        // even for errors, to allow liraries to show popups etc.
        // instead of logging directly.
        // (string instead of Exception for ease of use and to avoid user panic)
        //
        // events are readonly, set in constructor.
        // this ensures they are always initialized when used.
        // fixes https://github.com/MirrorNetworking/Mirror/issues/3337 and more
        protected readonly Action<int> OnConnected;
        protected readonly Action<int, ArraySegment<byte>, KcpChannel> OnData;
        protected readonly Action<int> OnDisconnected;
        protected readonly Action<int, ErrorCode, string> OnError;

        // configuration
        protected readonly KcpConfig config;

        // state
        protected Socket socket;
        EndPoint newClientEP;

        // raw receive buffer always needs to be of 'MTU' size, even if
        // MaxMessageSize is larger. kcp always sends in MTU segments and having
        // a buffer smaller than MTU would silently drop excess data.
        // => we need the mtu to fit channel + message!
        protected readonly byte[] rawReceiveBuffer;

        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        public Dictionary<int, KcpServerConnection> connections =
            new Dictionary<int, KcpServerConnection>();

        public KcpServer(Action<int> OnConnected,
                         Action<int, ArraySegment<byte>, KcpChannel> OnData,
                         Action<int> OnDisconnected,
                         Action<int, ErrorCode, string> OnError,
                         KcpConfig config)
        {
            // initialize callbacks first to ensure they can be used safely.
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
            this.OnError = OnError;
            this.config = config;

            // create mtu sized receive buffer
            rawReceiveBuffer = new byte[config.Mtu];

            // create newClientEP either IPv4 or IPv6
            newClientEP = config.DualMode
                          ? new IPEndPoint(IPAddress.IPv6Any, 0)
                          : new IPEndPoint(IPAddress.Any,     0);
        }

        public virtual bool IsActive() => socket != null;

        static Socket CreateServerSocket(bool DualMode, ushort port)
        {
            if (DualMode)
            {
                // IPv6 socket with DualMode @ "::" : port
                Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                // settings DualMode may throw:
                // https://learn.microsoft.com/en-us/dotnet/api/System.Net.Sockets.Socket.DualMode?view=net-7.0
                // attempt it, otherwise log but continue
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3358
                try
                {
                    socket.DualMode = true;
                }
                catch (NotSupportedException e)
                {
                    Log.Warning($"Failed to set Dual Mode, continuing with IPv6 without Dual Mode. Error: {e}");
                }
                socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                return socket;
            }
            else
            {
                // IPv4 socket @ "0.0.0.0" : port
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return socket;
            }
        }

        public virtual void Start(ushort port)
        {
            // only start once
            if (socket != null)
            {
                Log.Warning("KcpServer: already started!");
                return;
            }

            // listen
            socket = CreateServerSocket(config.DualMode, port);

            // recv & send are called from main thread.
            // need to ensure this never blocks.
            // even a 1ms block per connection would stop us from scaling.
            socket.Blocking = false;

            // configure buffer sizes
            Common.ConfigureSocketBuffers(socket, config.RecvBufferSize, config.SendBufferSize);
        }

        public void Send(int connectionId, ArraySegment<byte> segment, KcpChannel channel)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.peer.SendData(segment, channel);
            }
        }

        public void Disconnect(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.peer.Disconnect();
            }
        }

        // expose the whole IPEndPoint, not just the IP address. some need it.
        public IPEndPoint GetClientEndPoint(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                return connection.remoteEndPoint as IPEndPoint;
            }
            return null;
        }

        // io - input.
        // virtual so it may be modified for relays, nonalloc workaround, etc.
        // https://github.com/vis2k/where-allocation
        // bool return because not all receives may be valid.
        // for example, relay may expect a certain header.
        protected virtual bool RawReceiveFrom(out ArraySegment<byte> segment, out int connectionId)
        {
            segment = default;
            connectionId = 0;
            if (socket == null) return false;

            try
            {
                if (socket.ReceiveFromNonBlocking(rawReceiveBuffer, out segment, ref newClientEP))
                {
                    // set connectionId to hash from endpoint
                    connectionId = Common.ConnectionHash(newClientEP);
                    return true;
                }
            }
            catch (SocketException e)
            {
                // NOTE: SocketException is not a subclass of IOException.
                // the other end closing the connection is not an 'error'.
                // but connections should never just end silently.
                // at least log a message for easier debugging.
                Log.Info($"KcpServer: ReceiveFrom failed: {e}");
            }

            return false;
        }

        // io - out.
        // virtual so it may be modified for relays, nonalloc workaround, etc.
        // relays may need to prefix connId (and remoteEndPoint would be same for all)
        protected virtual void RawSend(int connectionId, ArraySegment<byte> data)
        {
            // get the connection's endpoint
            if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                Log.Warning($"KcpServer: RawSend invalid connectionId={connectionId}");
                return;
            }

            try
            {
                socket.SendToNonBlocking(data, connection.remoteEndPoint);
            }
            catch (SocketException e)
            {
                Log.Error($"KcpServer: SendTo failed: {e}");
            }
        }

        protected virtual KcpServerConnection CreateConnection(int connectionId)
        {
            // events need to be wrapped with connectionIds
            Action<ArraySegment<byte>> RawSendWrap =
                data => RawSend(connectionId, data);

            // create empty connection without peer first.
            // we need it to set up peer callbacks.
            // afterwards we assign the peer.
            KcpServerConnection connection = new KcpServerConnection(newClientEP);

            // generate a random cookie for this connection to avoid UDP spoofing.
            // needs to be random, but without allocations to avoid GC.
            uint cookie = Common.GenerateCookie();

            // set up peer with callbacks
            KcpPeer peer = new KcpPeer(RawSendWrap, OnAuthenticatedWrap, OnDataWrap, OnDisconnectedWrap, OnErrorWrap, config, cookie);

            // assign peer to connection
            connection.peer = peer;
            return connection;

            // setup authenticated event that also adds to connections
            void OnAuthenticatedWrap()
            {
                // only send handshake to client AFTER we received his
                // handshake in OnAuthenticated.
                // we don't want to reply to random internet messages
                // with handshakes each time.
                connection.peer.SendHandshake();

                // add to connections dict after being authenticated.
                connections.Add(connectionId, connection);
                Log.Info($"KcpServer: added connection({connectionId})");

                // setup Data + Disconnected events only AFTER the
                // handshake. we don't want to fire OnServerDisconnected
                // every time we receive invalid random data from the
                // internet.

                // setup data event


                // finally, call mirror OnConnected event
                Log.Info($"KcpServer: OnConnected({connectionId})");
                OnConnected(connectionId);
            }

            void OnDataWrap(ArraySegment<byte> message, KcpChannel channel)
            {
                // call mirror event
                //Log.Info($"KCP: OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                OnData(connectionId, message, channel);
            }

            void OnDisconnectedWrap()
            {
                // flag for removal
                // (can't remove directly because connection is updated
                //  and event is called while iterating all connections)
                connectionsToRemove.Add(connectionId);

                // call mirror event
                Log.Info($"KcpServer: OnDisconnected({connectionId})");
                OnDisconnected(connectionId);
            }

            void OnErrorWrap(ErrorCode error, string reason)
            {
                OnError(connectionId, error, reason);
            }
        }

        // receive + add + process once.
        // best to call this as long as there is more data to receive.
        void ProcessMessage(ArraySegment<byte> segment, int connectionId)
        {
            //Log.Info($"KCP: server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

            // is this a new connection?
            if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                // create a new KcpConnection based on last received
                // EndPoint. can be overwritten for where-allocation.
                connection = CreateConnection(connectionId);

                // DO NOT add to connections yet. only if the first message
                // is actually the kcp handshake. otherwise it's either:
                // * random data from the internet
                // * or from a client connection that we just disconnected
                //   but that hasn't realized it yet, still sending data
                //   from last session that we should absolutely ignore.
                //
                //
                // TODO this allocates a new KcpConnection for each new
                // internet connection. not ideal, but C# UDP Receive
                // already allocated anyway.
                //
                // expecting a MAGIC byte[] would work, but sending the raw
                // UDP message without kcp's reliability will have low
                // probability of being received.
                //
                // for now, this is fine.


                // now input the message & process received ones
                // connected event was set up.
                // tick will process the first message and adds the
                // connection if it was the handshake.
                connection.peer.RawInput(segment);
                connection.peer.TickIncoming();

                // again, do not add to connections.
                // if the first message wasn't the kcp handshake then
                // connection will simply be garbage collected.
            }
            // existing connection: simply input the message into kcp
            else
            {
                connection.peer.RawInput(segment);
            }
        }

        // process incoming messages. should be called before updating the world.
        // virtual because relay may need to inject their own ping or similar.
        readonly HashSet<int> connectionsToRemove = new HashSet<int>();
        public virtual void TickIncoming()
        {
            // input all received messages into kcp
            while (RawReceiveFrom(out ArraySegment<byte> segment, out int connectionId))
            {
                ProcessMessage(segment, connectionId);
            }

            // process inputs for all server connections
            // (even if we didn't receive anything. need to tick ping etc.)
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.peer.TickIncoming();
            }

            // remove disconnected connections
            // (can't do it in connection.OnDisconnected because Tick is called
            //  while iterating connections)
            foreach (int connectionId in connectionsToRemove)
            {
                connections.Remove(connectionId);
            }
            connectionsToRemove.Clear();
        }

        // process outgoing messages. should be called after updating the world.
        // virtual because relay may need to inject their own ping or similar.
        public virtual void TickOutgoing()
        {
            // flush all server connections
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.peer.TickOutgoing();
            }
        }

        // process incoming and outgoing for convenience.
        // => ideally call ProcessIncoming() before updating the world and
        //    ProcessOutgoing() after updating the world for minimum latency
        public virtual void Tick()
        {
            TickIncoming();
            TickOutgoing();
        }

        public virtual void Stop()
        {
            // need to clear connections, otherwise they are in next session.
            // fixes https://github.com/vis2k/kcp2k/pull/47
            connections.Clear();
            socket?.Close();
            socket = null;
        }
    }
}
