// where-allocation version of KcpServer.
// may not be wanted on all platforms, so it's an extra optional class.
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using WhereAllocation;

namespace kcp2k
{
    public class KcpServerNonAlloc : KcpServer
    {
        readonly IPEndPointNonAlloc reusableClientEP;

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
                                 int Timeout = KcpPeer.DEFAULT_TIMEOUT,
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

        protected override bool RawReceive(byte[] buffer, out int size, out int connectionId)
        {
            // where-allocation nonalloc ReceiveFrom.
            size = socket.ReceiveFrom_NonAlloc(buffer, 0, buffer.Length, SocketFlags.None, reusableClientEP);
            SocketAddress remoteAddress = reusableClientEP.temp;

            // where-allocation nonalloc GetHashCode
            connectionId = remoteAddress.GetHashCode();
            return true;
        }

        // make sure to pass IPEndPointNonAlloc as remoteEndPoint
        protected override void RawSend(int connectionId, ArraySegment<byte> data)
        {
            // get the connection's endpoint
            if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                Debug.LogWarning($"KcpServerNonAlloc.RawSend: invalid connectionId={connectionId}");
                return;
            }

            // where-allocation nonalloc send to the endpoint.
            // do not send to 'newClientEP', as that's always reused.
            // fixes https://github.com/MirrorNetworking/Mirror/issues/3296
            socket.SendTo_NonAlloc(data.Array, data.Offset, data.Count, SocketFlags.None, connection.remoteEndPoint as IPEndPointNonAlloc);
        }

        protected override KcpServerConnection CreateConnection(int connectionId)
        {
            // IPEndPointNonAlloc is reused all the time.
            // we can't store that as the connection's endpoint.
            // we need a new copy!
            IPEndPoint newClientEP = reusableClientEP.DeepCopyIPEndPoint();

            // for allocation free sending, we also need another
            // IPEndPointNonAlloc...
            IPEndPointNonAlloc endPointNonAlloc = new IPEndPointNonAlloc(newClientEP.Address, newClientEP.Port);

            // attach conectionId to RawSend.
            // kcp needs a simple RawSend(byte[]) function.
            Action<ArraySegment<byte>> RawSendWrap =
                data => RawSend(connectionId, data);

            KcpPeer peer = new KcpPeer(RawSendWrap, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize, Timeout, MaxRetransmits);
            return new KcpServerConnection(peer, endPointNonAlloc);
        }
    }
}
