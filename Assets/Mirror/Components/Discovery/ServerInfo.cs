using System.Net;

namespace Mirror.Discovery
{
    public class ServerInfo : MessageBase
    {
        // Hash the games received, set age to 0 when deserialize and update it, ticking it upwards in the
        // update function adding delta time. If it exceeds x seconds remove it from the list of avaialble games.
        public float age;

        // The server that sent this
        public IPEndPoint EndPoint { get; set; }

        public int port;
        public ushort totalPlayers;

        // Prevent duplicate server appearance when a connection can be made via LAN on multiple NICs
        public long serverId;

        // unique per game
        public long secretHandshake;
    }
}