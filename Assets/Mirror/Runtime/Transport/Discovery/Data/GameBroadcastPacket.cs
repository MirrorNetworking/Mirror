using System;
using System.Runtime.Serialization;

namespace Assets.Scripts.NetworkMessages
{
    [Serializable]
    public class GameBroadcastPacket : ISerializable
    {
        // I hash the games received, set age to 0 when deserialize and update it, ticking it upwards in my lobbby's update function adding delta time
        // if it exceeds x seconds I remove it from the list of avaialble games
        public float age;

        // I use this to decide if players can connect to each other or not as opposed to the signature originally produced 
        public string gameVersion;

        public string hostName;

        public string serverAddress;
        public int port;
        public ushort totalPlayers;

        // I use this to prevent duplicate server appearance when a connection can be made via LAN on multiple NICs
        public string serverGUID;

        public GameBroadcastPacket() { }

        protected GameBroadcastPacket(SerializationInfo info, StreamingContext context)
        {
            age = 0;

            gameVersion = info.GetString("gameVersion");
            hostName = info.GetString("hostName");
            serverAddress = info.GetString("serverAddress");
            port = info.GetInt32("port");
            totalPlayers = info.GetUInt16("totalPlayers");

            serverGUID = info.GetString("serverGUID");
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Simplified what I really do with versioning here just for this sample
            info.AddValue("gameVersion", "1.0.1");
            info.AddValue("hostName", hostName);
            info.AddValue("serverAddress", serverAddress);
            info.AddValue("port", port);
            info.AddValue("totalPlayers", totalPlayers);
            info.AddValue("serverGUID", serverGUID);
        }

        // I use this to create a dictionary in my lobby of game objects representing games you can join
        public string SimpleLobbyKey()
        {
            return serverAddress + ":" + port;
        }
    }
}