using System.Net;
using System.Net.Sockets;

namespace Mirror.KCP
{
    public class KcpServerConnection : KcpConnection
    {
        public KcpServerConnection(Socket socket, EndPoint remoteEndpoint) 
        {
            this.socket = socket;
            this.remoteEndpoint = remoteEndpoint;
            SetupKcp();
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndpoint);
        }
    }
}
