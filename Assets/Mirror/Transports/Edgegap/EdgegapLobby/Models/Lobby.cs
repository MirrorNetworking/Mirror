using System;
using UnityEngine;
namespace Edgegap
{
    // https://docs.edgegap.com/docs/lobby/functions#getting-a-specific-lobbys-information
    [Serializable]
    public struct Lobby
    {
        [Serializable]
        public struct Player
        {
            public uint authorization_token;
            public string id;
            public bool is_host;
        }

        [Serializable]
        public struct Port
        {
            public string name;
            public int port;
            public string protocol;
        }

        [Serializable]
        public struct Assignment
        {
            public uint authorization_token;
            public string host;
            public string ip;
            public Port[] ports;
        }

        public Assignment assignment;
        public string name;
        public string lobby_id;
        public bool is_joinable;
        public bool is_started;
        public int player_count;
        public int capacity;
        public int available_slots => capacity - player_count;
        public string[] tags;
        public Player[] players;
    }
}
