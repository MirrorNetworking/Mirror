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
        // events
        public Action<int> OnConnected;
        public Action<int, ArraySegment<byte>, KcpChannel> OnData;
        public Action<int> OnDisconnected;

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

        // IMPORTANT: raw receive buffer always needs to be of 'MTU' size, even
        //            if MaxMessageSize is larger. kcp always sends in MTU
        //            segments and having a buffer smaller than MTU would
        //            silently drop excess data.
        //            => we need the mtu to fit channel + message!
        readonly byte[] rawReceiveBuffer = new byte[Kcp.MTU_DEF];

        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        public Dictionary<int, KcpServerConnection> connections = new Dictionary<int, KcpServerConnection>();

        public KcpServer(Action<int> OnConnected,
                         Action<int, ArraySegment<byte>, KcpChannel> OnData,
                         Action<int> OnDisconnected,
                         bool DualMode,
                         bool NoDelay,
                         uint Interval,
                         int FastResend = 0,
                         bool CongestionWindow = true,
                         uint SendWindowSize = Kcp.WND_SND,
                         uint ReceiveWindowSize = Kcp.WND_RCV,
                         int Timeout = KcpConnection.DEFAULT_TIMEOUT,
                         uint MaxRetransmits = Kcp.DEADLINK,
                         bool MaximizeSendReceiveBuffersToOSLimit = false)
        {
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
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

        public bool IsActive() => socket != null;

        // if connections drop under heavy load, increase to OS limit.
        // if still not enough, increase the OS limit.
        void ConfigureSocketBufferSizes()
        {
            if (MaximizeSendReceiveBuffersToOSLimit)
            {
                // log initial size for comparison.
                // remember initial size for log comparison
                int initialReceive = socket.ReceiveBufferSize;
                int initialSend = socket.SendBufferSize;

                socket.SetReceiveBufferToOSLimit();
                socket.SetSendBufferToOSLimit();
                Log.Info($"KcpServer: RecvBuf = {initialReceive}=>{socket.ReceiveBufferSize} ({socket.ReceiveBufferSize/initialReceive}x) SendBuf = {initialSend}=>{socket.SendBufferSize} ({socket.SendBufferSize/initialSend}x) increased to OS limits!");
            }
            // otherwise still log the defaults for info.
            else Log.Info($"KcpServer: RecvBuf = {socket.ReceiveBufferSize} SendBuf = {socket.SendBufferSize}. If connections drop under heavy load, enable {nameof(MaximizeSendReceiveBuffersToOSLimit)} to increase it to OS limit. If they still drop, increase the OS limit.");
        }

        public void Start(ushort port)
        {
            // only start once
            if (socket != null)
            {
                Log.Warning("KCP: server already started!");
            }

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

            // configure socket buffer size.
            ConfigureSocketBufferSizes();
        }

        public void Send(int connectionId, ArraySegment<byte> segment, KcpChannel channel)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.SendData(segment, channel);
            }
        }

        public void Disconnect(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.Disconnect();
            }
        }

        public string GetClientAddress(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                return (connection.GetRemoteEndPoint() as IPEndPoint).Address.ToString();
            }
            return "";
        }

        // EndPoint & Receive functions can be overwritten for where-allocation:
        // https://github.com/vis2k/where-allocation
        protected virtual int ReceiveFrom(byte[] buffer, out int connectionHash)
        {
            // NOTE: ReceiveFrom allocates.
            //   we pass our IPEndPoint to ReceiveFrom.
            //   receive from calls newClientEP.Create(socketAddr).
            //   IPEndPoint.Create always returns a new IPEndPoint.
            //   https://github.com/mono/mono/blob/f74eed4b09790a0929889ad7fc2cf96c9b6e3757/mcs/class/System/System.Net.Sockets/Socket.cs#L1761
            int read = socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref newClientEP);

            // calculate connectionHash from endpoint
            // NOTE: IPEndPoint.GetHashCode() allocates.
            //  it calls m_Address.GetHashCode().
            //  m_Address is an IPAddress.
            //  GetHashCode() allocates for IPv6:
            //  https://github.com/mono/mono/blob/bdd772531d379b4e78593587d15113c37edd4a64/mcs/class/referencesource/System/net/System/Net/IPAddress.cs#L699
            //
            // => using only newClientEP.Port wouldn't work, because
            //    different connections can have the same port.
            connectionHash = newClientEP.GetHashCode();
            return read;
        }

        protected virtual KcpServerConnection CreateConnection() =>
            new KcpServerConnection(socket, newClientEP, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout, MaxRetransmits);

        // process incoming messages. should be called before updating the world.
        HashSet<int> connectionsToRemove = new HashSet<int>();
        public void TickIncoming()
        {
            while (socket != null && socket.Poll(0, SelectMode.SelectRead))
            {
                try
                {
                    // receive
                    int msgLength = ReceiveFrom(rawReceiveBuffer, out int connectionId);
                    //Log.Info($"KCP: server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

                    // IMPORTANT: detect if buffer was too small for the received
                    //            msgLength. otherwise the excess data would be
                    //            silently lost.
                    //            (see ReceiveFrom documentation)
                    if (msgLength <= rawReceiveBuffer.Length)
                    {
                        // is this a new connection?
                        if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
                        {
                            // create a new KcpConnection based on last received
                            // EndPoint. can be overwritten for where-allocation.
                            connection = CreateConnection();

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
                            connection.OnAuthenticated = () =>
                            {
                                // only send handshake to client AFTER we received his
                                // handshake in OnAuthenticated.
                                // we don't want to reply to random internet messages
                                // with handshakes each time.
                                connection.SendHandshake();

                                // add to connections dict after being authenticated.
                                connections.Add(connectionId, connection);
                                Log.Info($"KCP: server added connection({connectionId})");

                                // setup Data + Disconnected events only AFTER the
                                // handshake. we don't want to fire OnServerDisconnected
                                // every time we receive invalid random data from the
                                // internet.

                                // setup data event
                                connection.OnData = (message, channel) =>
                                {
                                    // call mirror event
                                    //Log.Info($"KCP: OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                                    OnData.Invoke(connectionId, message, channel);
                                };

                                // setup disconnected event
                                connection.OnDisconnected = () =>
                                {
                                    // flag for removal
                                    // (can't remove directly because connection is updated
                                    //  and event is called while iterating all connections)
                                    connectionsToRemove.Add(connectionId);

                                    // call mirror event
                                    Log.Info($"KCP: OnServerDisconnected({connectionId})");
                                    OnDisconnected.Invoke(connectionId);
                                };

                                // finally, call mirror OnConnected event
                                Log.Info($"KCP: OnServerConnected({connectionId})");
                                OnConnected.Invoke(connectionId);
                            };

                            // now input the message & process received ones
                            // connected event was set up.
                            // tick will process the first message and adds the
                            // connection if it was the handshake.
                            connection.RawInput(rawReceiveBuffer, msgLength);
                            connection.TickIncoming();

                            // again, do not add to connections.
                            // if the first message wasn't the kcp handshake then
                            // connection will simply be garbage collected.
                        }
                        // existing connection: simply input the message into kcp
                        else
                        {
                            connection.RawInput(rawReceiveBuffer, msgLength);
                        }
                    }
                    else
                    {
                        Log.Error($"KCP Server: message of size {msgLength} does not fit into buffer of size {rawReceiveBuffer.Length}. The excess was silently dropped. Disconnecting connectionId={connectionId}.");
                        Disconnect(connectionId);
                    }
                }
                // this is fine, the socket might have been closed in the other end
                catch (SocketException) {}
            }

            // process inputs for all server connections
            // (even if we didn't receive anything. need to tick ping etc.)
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.TickIncoming();
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
        public void TickOutgoing()
        {
            // flush all server connections
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.TickOutgoing();
            }
        }

        // process incoming and outgoing for convenience.
        // => ideally call ProcessIncoming() before updating the world and
        //    ProcessOutgoing() after updating the world for minimum latency
        public void Tick()
        {
            TickIncoming();
            TickOutgoing();
        }

        public void Stop()
        {
            socket?.Close();
            socket = null;
        }
    }
}
