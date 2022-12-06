using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpServerConnection : KcpConnection
    {
        // Constructor & Send functions can be overwritten for where-allocation:
        // https://github.com/vis2k/where-allocation
        public KcpServerConnection(Socket socket, EndPoint remoteEndPoint, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV, int timeout = DEFAULT_TIMEOUT, uint maxRetransmits = Kcp.DEADLINK)
        {
            this.socket = socket;
            this.remoteEndPoint = remoteEndPoint;
            SetupKcp(noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize, timeout, maxRetransmits);
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndPoint);
        }
    }
}
