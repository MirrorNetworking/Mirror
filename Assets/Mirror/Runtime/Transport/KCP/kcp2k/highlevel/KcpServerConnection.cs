using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpServerConnection : KcpConnection
    {
        public KcpServerConnection(Socket socket, EndPoint remoteEndpoint, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint sendWindowSize = Kcp.WND_SND, uint receiveWindowSize = Kcp.WND_RCV, int timeout = DEFAULT_TIMEOUT)
        {
            this.socket = socket;
            this.remoteEndpoint = remoteEndpoint;
            SetupKcp(noDelay, interval, fastResend, congestionWindow, sendWindowSize, receiveWindowSize, timeout);
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndpoint);
        }
    }
}
