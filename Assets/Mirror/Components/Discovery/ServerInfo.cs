using System.Net;
using Mirror;

namespace Mirror.Discovery
{
    public class ServerInfo : MessageBase
    {
        // I hash the games received, set age to 0 when deserialize and update it, ticking it upwards in my lobbby's update function adding delta time
        // if it exceeds x seconds I remove it from the list of avaialble games
        public float age;

        // The server that sent this
        public IPEndPoint EndPoint { get; set; }

        public int port;
        public ushort totalPlayers;

        // I use this to prevent duplicate server appearance when a connection can be made via LAN on multiple NICs
        public long serverId;

        // unique per game
        public long secretHandshake;
    }
}