using System.Net;
using System.Net.Sockets;
using WhereAllocation;

namespace kcp2k
{
    public class KcpServerConnection : KcpConnection
    {
        IPEndPointNonAlloc reusableSendEndPoint;

        public KcpServerConnection(Socket socket, EndPoint remoteEndPoint, IPEndPointNonAlloc reusableSendEndPoint, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV, int timeout = DEFAULT_TIMEOUT)
        {
            this.socket = socket;
            this.remoteEndpoint = remoteEndPoint;
            this.reusableSendEndPoint = reusableSendEndPoint;
            SetupKcp(noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize, timeout);
        }

        protected override void RawSend(byte[] data, int length)
        {
            // where-allocation nonalloc
            socket.SendTo_NonAlloc(data, 0, length, SocketFlags.None, reusableSendEndPoint);
        }
    }
}
