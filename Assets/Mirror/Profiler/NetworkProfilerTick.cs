using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Profiler
{
    /// <summary>
    /// The direction of network traffic
    /// </summary>
    [Serializable]
    public enum NetworkDirection
    {
        /// <summary>
        /// Data/Message is coming from a remote host
        /// </summary>
        Incoming = 0,

        /// <summary>
        /// Data/Message going to a remote host
        /// </summary>
        Outgoing = 1
    }

    /// <summary>
    /// Stores a network tick
    /// </summary>
    [Serializable]
    public struct NetworkProfileTick
    {
        /// <summary>
        /// The Time.time at the moment tick collection ended
        /// </summary>
        public int frameCount;

        private List<NetworkProfileMessage> messages;
        internal float time;

        /// <summary>
        /// The summary of messages captured during the tick
        /// </summary>
        public List<NetworkProfileMessage> Messages
        {
            get
            {
                messages = messages ?? new List<NetworkProfileMessage>();
                return messages;
            }
        }

        /// <summary>
        /// Records the message to the current tick
        /// </summary>
        public void RecordMessage(NetworkProfileMessage networkProfileMessage) 
        {
            Messages.Add(networkProfileMessage);
        }

        public int Count(NetworkDirection direction)
        {
            int total = 0;
            foreach (var message in Messages)
                if (message.Direction == direction)
                    total += message.Count;
            return total;
        }

        public int Bytes(NetworkDirection direction)
        {
            int total = 0;
            foreach (var message in Messages)
                if (message.Direction == direction)
                    total += message.Count * message.Size;
            return total;
        }

        public int TotalMessages()
        {
            int total = 0;
            foreach (var message in Messages)
                total +=message.Count;
            return total;
        }
    }

    /// <summary>
    /// Stores a network profile message
    /// </summary>
    [Serializable]
    public struct NetworkProfileMessage
    {
        public NetworkDirection Direction;
        public string Type;
        public string Name;
        public int Channel;
        public int Size;
        public int Count;

        [NonSerialized]
        private GameObject gameObject;

        public GameObject GameObject
        {
            get => gameObject;
            set
            {
                gameObject = value;
                Object = value == null ? "" : value.name;
            }
        }

        public string Object { get; private set; }
    }

}
