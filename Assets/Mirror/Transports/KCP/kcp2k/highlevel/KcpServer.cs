// kcp server logic abstracted into a class.
// for use in Mirror, DOTSNET, testing, etc.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace kcp2k
{
    public class KcpServer
    {
        // events
        public Action<int> OnConnected;
        public Action<int, ArraySegment<byte>, KcpChannel> OnData;
        public Action<int> OnDisconnected;
        // error callback instead of logging.
        // allows libraries to show popups etc.
        // (string instead of Exception for ease of use and to avoid user panic)
        public Action<int, ErrorCode, string> OnError;

        // socket configuration
        // DualMode uses both IPv6 and IPv4. not all platforms support it.
        // (Nintendo Switch, etc.)
        public bool DualMode;
        // too small send/receive buffers might cause connection drops under
        // heavy load. using the OS max size can make a difference already.
        public bool MaximizeSendReceiveBuffersToOSLimit;

        // kcp configuration
        // NoDelay is recommended to reduce latency. This also scales better
        // without buffers getting full.
        public bool NoDelay;
        // KCP internal update interval. 100ms is KCP default, but a lower
        // interval is recommended to minimize latency and to scale to more
        // networked entities.
        public uint Interval;
        // KCP fastresend parameter. Faster resend for the cost of higher
        // bandwidth.
        public int FastResend;
        // KCP 'NoCongestionWindow' is false by default. here we negate it for
        // ease of use. This can be disabled for high scale games if connections
        // choke regularly.
        public bool CongestionWindow;
        // KCP window size can be modified to support higher loads.
        // for example, Mirror Benchmark requires:
        //   128, 128 for 4k monsters
        //   512, 512 for 10k monsters
        //  8192, 8192 for 20k monsters
        public uint SendWindowSize;
        public uint ReceiveWindowSize;
        // timeout in milliseconds
        public int Timeout;
        // maximum retransmission attempts until dead_link
        public uint MaxRetransmits;

        // state
        protected Socket socket;
        EndPoint newClientEP;

        // raw receive buffer always needs to be of 'MTU' size, even if
        // MaxMessageSize is larger. kcp always sends in MTU segments and having
        // a buffer smaller than MTU would silently drop excess data.
        // => we need the mtu to fit channel + message!
        readonly byte[] rawReceiveBuffer = new byte[Kcp.MTU_DEF];

        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        public Dictionary<int, KcpServerConnection> connections =
            new Dictionary<int, KcpServerConnection>();

        public KcpServer(Action<int> OnConnected,
                         Action<int, ArraySegment<byte>, KcpChannel> OnData,
                         Action<int> OnDisconnected,
                         Action<int, ErrorCode, string> OnError,
                         bool DualMode,
                         bool NoDelay,
                         uint Interval,
                         int FastResend = 0,
                         bool CongestionWindow = true,
                         uint SendWindowSize = Kcp.WND_SND,
                         uint ReceiveWindowSize = Kcp.WND_RCV,
                         int Timeout = KcpPeer.DEFAULT_TIMEOUT,
                         uint MaxRetransmits = Kcp.DEADLINK,
                         bool MaximizeSendReceiveBuffersToOSLimit = false)
        {
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
            this.OnError = OnError;
            this.DualMode = DualMode;
            this.NoDelay = NoDelay;
            this.Interval = Interval;
            this.FastResend = FastResend;
            this.CongestionWindow = CongestionWindow;
            this.SendWindowSize = SendWindowSize;
            this.ReceiveWindowSize = ReceiveWindowSize;
            this.Timeout = Timeout;
            this.MaxRetransmits = MaxRetransmits;
            this.MaximizeSendReceiveBuffersToOSLimit = MaximizeSendReceiveBuffersToOSLimit;

            // create newClientEP either IPv4 or IPv6
            newClientEP = DualMode
                          ? new IPEndPoint(IPAddress.IPv6Any, 0)
                          : new IPEndPoint(IPAddress.Any, 0);
        }

        public virtual bool IsActive() => socket != null;

        public virtual void Start(ushort port)
        {
            // only start once
            if (socket != null)
            {
                Log.Warning("[KCP] server already started!");
                return;
            }

            Log.Info($"[KCP] Starting server on port {port}");

            // listen
            if (DualMode)
            {
                // IPv6 socket with DualMode
                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                socket.DualMode = true;
                socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
            }
            else
            {
                // IPv4 socket
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
            }

            // configure buffer sizes:
            // if connections drop under heavy load, increase to OS limit.
            // if still not enough, increase the OS limit.
            if (MaximizeSendReceiveBuffersToOSLimit)
            {
                Common.MaximizeSocketBuffers(socket);
            }
            // otherwise still log the defaults for info.
            else Log.Info($"[KCP] Server: RecvBuf = {socket.ReceiveBufferSize} SendBuf = {socket.SendBufferSize}. If connections drop under heavy load, enable {nameof(MaximizeSendReceiveBuffersToOSLimit)} to increase it to OS limit. If they still drop, increase the OS limit.");
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

        // io - poll.
        // return true if there is data to read.
        // after which RawReceive will be called.
        // virtual because for relays,
        protected virtual bool RawPoll() =>
            socket != null && socket.Poll(0, SelectMode.SelectRead);

        // io - input.
        // virtual so it may be modified for relays, nonalloc workaround, etc.
        // https://github.com/vis2k/where-allocation
        // bool return because not all receives may be valid.
        // for example, relay may expect a certain header.
        protected virtual bool RawReceive(byte[] buffer, out int size, out int connectionId)
        {
            // NOTE: ReceiveFrom allocates.
            //   we pass our IPEndPoint to ReceiveFrom.
            //   receive from calls newClientEP.Create(socketAddr).
            //   IPEndPoint.Create always returns a new IPEndPoint.
            //   https://github.com/mono/mono/blob/f74eed4b09790a0929889ad7fc2cf96c9b6e3757/mcs/class/System/System.Net.Sockets/Socket.cs#L1761
            size = socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref newClientEP);

            // set connectionId to hash from endpoint
            // NOTE: IPEndPoint.GetHashCode() allocates.
            //  it calls m_Address.GetHashCode().
            //  m_Address is an IPAddress.
            //  GetHashCode() allocates for IPv6:
            //  https://github.com/mono/mono/blob/bdd772531d379b4e78593587d15113c37edd4a64/mcs/class/referencesource/System/net/System/Net/IPAddress.cs#L699
            //
            // => using only newClientEP.Port wouldn't work, because
            //    different connections can have the same port.
            connectionId = newClientEP.GetHashCode();
            return true;
        }

        // io - out.
        // virtual so it may be modified for relays, nonalloc workaround, etc.
        // relays may need to prefix connId (and remoteEndPoint would be same for all)
        protected virtual void RawSend(int connectionId, ArraySegment<byte> data)
        {
            // get the connection's endpoint
            if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                Debug.LogWarning($"[KCP] Server.RawSend: invalid connectionId={connectionId}");
                return;
            }

            // send to the the endpoint.
            // do not send to 'newClientEP', as that's always reused.
            // fixes https://github.com/MirrorNetworking/Mirror/issues/3296
            socket.SendTo(data.Array, data.Offset, data.Count, SocketFlags.None, connection.remoteEndPoint);
        }

        protected virtual KcpServerConnection CreateConnection(int connectionId)
        {
            // attach connectionId to RawSend.
            // kcp needs a simple RawSend(byte[]) function.
            Action<ArraySegment<byte>> RawSendWrap =
                data => RawSend(connectionId, data);

            KcpPeer peer = new KcpPeer(RawSendWrap, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout, MaxRetransmits);
            return new KcpServerConnection(peer, newClientEP);
        }

        // receive + add + process once.
        // best to call this as long as there is more data to receive.
        void ProcessNext()
        {
            try
            {
                // receive from socket.
                // returns amount of bytes written into buffer.
                // throws SocketException if datagram was larger than buffer.
                // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receive?view=net-6.0
                if (!RawReceive(rawReceiveBuffer, out int size, out int connectionId))
                    return;

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

                    // setup authenticated event that also adds to connections
                    connection.peer.OnAuthenticated = () =>
                    {
                        // only send handshake to client AFTER we received his
                        // handshake in OnAuthenticated.
                        // we don't want to reply to random internet messages
                        // with handshakes each time.
                        connection.peer.SendHandshake();

                        // add to connections dict after being authenticated.
                        connections.Add(connectionId, connection);
                        Log.Info($"[KCP] server added connection({connectionId})");

                        // setup Data + Disconnected events only AFTER the
                        // handshake. we don't want to fire OnServerDisconnected
                        // every time we receive invalid random data from the
                        // internet.

                        // setup data event
                        connection.peer.OnData = (message, channel) =>
                        {
                            // call mirror event
                            //Log.Info($"KCP: OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                            OnData.Invoke(connectionId, message, channel);
                        };

                        // setup disconnected event
                        connection.peer.OnDisconnected = () =>
                        {
                            // flag for removal
                            // (can't remove directly because connection is updated
                            //  and event is called while iterating all connections)
                            connectionsToRemove.Add(connectionId);

                            // call mirror event
                            Log.Info($"[KCP] OnServerDisconnected({connectionId})");
                            OnDisconnected(connectionId);
                        };

                        // setup error event
                        connection.peer.OnError = (error, reason) =>
                        {
                            OnError(connectionId, error, reason);
                        };

                        // finally, call mirror OnConnected event
                        Log.Info($"[KCP] OnServerConnected({connectionId})");
                        OnConnected(connectionId);
                    };

                    // now input the message & process received ones
                    // connected event was set up.
                    // tick will process the first message and adds the
                    // connection if it was the handshake.
                    connection.peer.RawInput(rawReceiveBuffer, 0, size);
                    connection.peer.TickIncoming();

                    // again, do not add to connections.
                    // if the first message wasn't the kcp handshake then
                    // connection will simply be garbage collected.
                }
                // existing connection: simply input the message into kcp
                else
                {
                    connection.peer.RawInput(rawReceiveBuffer, 0, size);
                }
            }
            // this is fine, the socket might have been closed in the other end
            catch (SocketException ex)
            {
                // the other end closing the connection is not an 'error'.
                // but connections should never just end silently.
                // at least log a message for easier debugging.
                Log.Info($"[KCP] ClientConnection: looks like the other end has closed the connection. This is fine: {ex}");
            }
        }

        // process incoming messages. should be called before updating the world.
        // virtual because relay may need to inject their own ping or similar.
        HashSet<int> connectionsToRemove = new HashSet<int>();
        public virtual void TickIncoming()
        {
            while (RawPoll())
            {
                ProcessNext();
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
            socket?.Close();
            socket = null;
        }
    }
}
