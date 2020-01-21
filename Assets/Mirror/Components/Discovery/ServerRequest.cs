using System.Net;

namespace Mirror.Discovery
{
    public class ServerRequest : MessageBase
    {
        public long secretHandshake;

        // The server that sent this
        public IPEndPoint EndPoint { get; set; }
    }
}
