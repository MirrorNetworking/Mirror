using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpServerConnection : KcpConnection
    {
        public KcpServerConnection(Socket socket, EndPoint remoteEndpoint, bool noDelay, uint interval = Kcp.INTERVAL)
        {
            this.socket = socket;
            this.remoteEndpoint = remoteEndpoint;
            SetupKcp(noDelay, interval);
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndpoint);
        }
    }
}
