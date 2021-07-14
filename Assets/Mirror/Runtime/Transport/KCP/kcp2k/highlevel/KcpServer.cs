// kcp server logic abstracted into a class.
// for use in Mirror, DOTSNET, testing, etc.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using WhereAllocation;

namespace kcp2k
{
    public class KcpServer
    {
        // events
        public Action<int> OnConnected;
        public Action<int, ArraySegment<byte>> OnData;
        public Action<int> OnDisconnected;

        // configuration
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

        // state
        Socket socket;
#if UNITY_SWITCH
        // switch does not support ipv6
        //EndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
        IPEndPointNonAlloc reusableClientEP = new IPEndPointNonAlloc(IPAddress.Any, 0); // where-allocation
#else
        //EndPoint newClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
        IPEndPointNonAlloc reusableClientEP = new IPEndPointNonAlloc(IPAddress.IPv6Any, 0); // where-allocation
#endif
        // IMPORTANT: raw receive buffer always needs to be of 'MTU' size, even
        //            if MaxMessageSize is larger. kcp always sends in MTU
        //            segments and having a buffer smaller than MTU would
        //            silently drop excess data.
        //            => we need the mtu to fit channel + message!
        readonly byte[] rawReceiveBuffer = new byte[Kcp.MTU_DEF];

        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        public Dictionary<int, KcpServerConnection> connections = new Dictionary<int, KcpServerConnection>();

        public KcpServer(Action<int> OnConnected,
                         Action<int, ArraySegment<byte>> OnData,
                         Action<int> OnDisconnected,
                         bool NoDelay,
                         uint Interval,
                         int FastResend = 0,
                         bool CongestionWindow = true,
                         uint SendWindowSize = Kcp.WND_SND,
                         uint ReceiveWindowSize = Kcp.WND_RCV,
                         int Timeout = KcpConnection.DEFAULT_TIMEOUT)
        {
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
            this.NoDelay = NoDelay;
            this.Interval = Interval;
            this.FastResend = FastResend;
            this.CongestionWindow = CongestionWindow;
            this.SendWindowSize = SendWindowSize;
            this.ReceiveWindowSize = ReceiveWindowSize;
            this.Timeout = Timeout;
        }

        public bool IsActive() => socket != null;

        public void Start(ushort port)
        {
            // only start once
            if (socket != null)
            {
                Log.Warning("KCP: server already started!");
            }

            // listen
#if UNITY_SWITCH
            // Switch does not support ipv6
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
#else
            socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            socket.DualMode = true;
            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
#endif
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

        // process incoming messages. should be called before updating the world.
        HashSet<int> connectionsToRemove = new HashSet<int>();
        public void TickIncoming()
        {
            while (socket != null && socket.Poll(0, SelectMode.SelectRead))
            {
                try
                {
                    // NOTE: ReceiveFrom allocates.
                    //   we pass our IPEndPoint to ReceiveFrom.
                    //   receive from calls newClientEP.Create(socketAddr).
                    //   IPEndPoint.Create always returns a new IPEndPoint.
                    //   https://github.com/mono/mono/blob/f74eed4b09790a0929889ad7fc2cf96c9b6e3757/mcs/class/System/System.Net.Sockets/Socket.cs#L1761
                    //int msgLength = socket.ReceiveFrom(rawReceiveBuffer, 0, rawReceiveBuffer.Length, SocketFlags.None, ref newClientEP);
                    //Log.Info($"KCP: server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

                    // where-allocation nonalloc ReceiveFrom.
                    int msgLength = socket.ReceiveFrom_NonAlloc(rawReceiveBuffer, 0, rawReceiveBuffer.Length, SocketFlags.None, reusableClientEP);
                    SocketAddress remoteAddress = reusableClientEP.temp;

                    // calculate connectionId from endpoint
                    // NOTE: IPEndPoint.GetHashCode() allocates.
                    //  it calls m_Address.GetHashCode().
                    //  m_Address is an IPAddress.
                    //  GetHashCode() allocates for IPv6:
                    //  https://github.com/mono/mono/blob/bdd772531d379b4e78593587d15113c37edd4a64/mcs/class/referencesource/System/net/System/Net/IPAddress.cs#L699
                    //
                    // => using only newClientEP.Port wouldn't work, because
                    //    different connections can have the same port.
                    //int connectionId = newClientEP.GetHashCode();

                    // where-allocation nonalloc GetHashCode
                    int connectionId = remoteAddress.GetHashCode();

                    // IMPORTANT: detect if buffer was too small for the received
                    //            msgLength. otherwise the excess data would be
                    //            silently lost.
                    //            (see ReceiveFrom documentation)
                    if (msgLength <= rawReceiveBuffer.Length)
                    {
                        // is this a new connection?
                        if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
                        {
                            // IPEndPointNonAlloc is reused all the time.
                            // we can't store that as the connection's endpoint.
                            // we need a new copy!
                            IPEndPoint newClientEP = reusableClientEP.DeepCopyIPEndPoint();

                            // for allocation free sending, we also need another
                            // IPEndPointNonAlloc...
                            IPEndPointNonAlloc reusableSendEP = new IPEndPointNonAlloc(newClientEP.Address, newClientEP.Port);

                            // create a new KcpConnection
                            // -> where-allocation IPEndPointNonAlloc is reused.
                            //    need to create a new one from the temp address.
                            connection = new KcpServerConnection(socket, newClientEP, reusableSendEP, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout);

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
                                Log.Info($"KCP: server added connection({connectionId}): {newClientEP}");

                                // setup Data + Disconnected events only AFTER the
                                // handshake. we don't want to fire OnServerDisconnected
                                // every time we receive invalid random data from the
                                // internet.

                                // setup data event
                                connection.OnData = (message) =>
                                {
                                    // call mirror event
                                    //Log.Info($"KCP: OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                                    OnData.Invoke(connectionId, message);
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

        // pause/unpause to safely support mirror scene handling and to
        // immediately pause the receive while loop if needed.
        public void Pause()
        {
            foreach (KcpServerConnection connection in connections.Values)
                connection.Pause();
        }

        public void Unpause()
        {
            foreach (KcpServerConnection connection in connections.Values)
                connection.Unpause();
        }
    }
}
