// server needs to store a separate KcpPeer for each connection.
// as well as remoteEndPoint so we know where to send data to.
using System.Net;

namespace kcp2k
{
    // struct to avoid memory indirection
    public struct KcpServerConnection
    {
        public readonly KcpPeer peer;
        public readonly EndPoint remoteEndPoint;

        public KcpServerConnection(KcpPeer peer, EndPoint remoteEndPoint)
        {
            this.peer = peer;
            this.remoteEndPoint = remoteEndPoint;
        }
    }
}
