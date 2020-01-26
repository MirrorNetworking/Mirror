using System;
using System.Net;

namespace Mirror.Discovery
{
    public class ServerResponse : MessageBase
    {
        // The server that sent this
        public IPEndPoint EndPoint { get; set; }

        public Uri uri;

        // Prevent duplicate server appearance when a connection can be made via LAN on multiple NICs
        public long serverId;
    }
}