// where-allocation version of KcpServer.
// may not be wanted on all platforms, so it's an extra optional class.
using System;
using System.Net;
using System.Net.Sockets;
using WhereAllocation;

namespace kcp2k
{
    public class KcpServerNonAlloc : KcpServer
    {
        IPEndPointNonAlloc reusableClientEP;

        public KcpServerNonAlloc(Action<int> OnConnected,
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
                                 int Timeout = KcpConnection.DEFAULT_TIMEOUT,
                                 uint MaxRetransmits = Kcp.DEADLINK,
                                 bool MaximizeSendReceiveBuffersToOSLimit = false)
            : base(OnConnected,
                   OnData,
                   OnDisconnected,
                   OnError,
                   DualMode,
                   NoDelay,
                   Interval,
                   FastResend,
                   CongestionWindow,
                   SendWindowSize,
                   ReceiveWindowSize,
                   Timeout,
                   MaxRetransmits,
                   MaximizeSendReceiveBuffersToOSLimit)
        {
            // create reusableClientEP either IPv4 or IPv6
            reusableClientEP = DualMode
                ? new IPEndPointNonAlloc(IPAddress.IPv6Any, 0)
                : new IPEndPointNonAlloc(IPAddress.Any, 0);
        }

        protected override int ReceiveFrom(byte[] buffer, out int connectionHash)
        {
            // where-allocation nonalloc ReceiveFrom.
            int read = socket.ReceiveFrom_NonAlloc(buffer, 0, buffer.Length, SocketFlags.None, reusableClientEP);
            SocketAddress remoteAddress = reusableClientEP.temp;

            // where-allocation nonalloc GetHashCode
            connectionHash = remoteAddress.GetHashCode();
            return read;
        }

        protected override KcpServerConnection CreateConnection()
        {
            // IPEndPointNonAlloc is reused all the time.
            // we can't store that as the connection's endpoint.
            // we need a new copy!
            IPEndPoint newClientEP = reusableClientEP.DeepCopyIPEndPoint();

            // for allocation free sending, we also need another
            // IPEndPointNonAlloc...
            IPEndPointNonAlloc reusableSendEP = new IPEndPointNonAlloc(newClientEP.Address, newClientEP.Port);

            // create a new KcpConnection NonAlloc version
            // -> where-allocation IPEndPointNonAlloc is reused.
            //    need to create a new one from the temp address.
            return new KcpServerConnectionNonAlloc(socket, newClientEP, reusableSendEP, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout, MaxRetransmits);
        }
    }
}