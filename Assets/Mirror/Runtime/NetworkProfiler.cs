using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// The direction of network traffic
    /// </summary>
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
    /// Stores data related to a channel
    /// </summary>
    public class NetworkProfileChannel
    {
        /// <summary>
        /// The channel id
        /// </summary>
        public int Id;

        /// <summary>
        /// The number of bytes outgoing
        /// </summary>
        public int BytesOutgoing;

        /// <summary>
        /// The number of bytes incoming
        /// </summary>
        public int BytesIncoming;
    }

    /// <summary>
    /// Stores a network tick
    /// </summary>
    public class NetworkProfileTick
    {
        /// <summary>
        /// The Time.time at the moment tick collection ended
        /// </summary>
        public float Time;

        /// <summary>
        /// The total number of messages captured during this tick
        /// </summary>
        public int TotalMessages;

        /// <summary>
        /// The summary of messages captured during the tick
        /// </summary>
        public List<NetworkProfileMessage> Messages = new List<NetworkProfileMessage>();

        /// <summary>
        /// The channels that sent or received data during the tick
        /// </summary>
        public List<NetworkProfileChannel> Channels = new List<NetworkProfileChannel>();

        /// <summary>
        /// Records the message to the current tick
        /// </summary>
        /// <param name="direction">The direction of the message</param>
        /// <param name="messageType">The message type</param>
        /// <param name="entryName">The name of the entry, generally represents the target of the message</param>
        /// <param name="count">The number of these messages sent</param>
        public void RecordMessage(NetworkDirection direction, Type messageType, string entryName, int count)
        {
            this.TotalMessages += count;

            foreach (NetworkProfileMessage m in this.Messages)
            {
                if (m.Direction == direction && messageType == m.Type && entryName == m.Name)
                {
                    m.Count += count;
                    return;
                }
            }

            this.Messages.Add(new NetworkProfileMessage()
            {
                Type = messageType,
                Direction = direction,
                Name = entryName,
                Count = count
            });
        }
    }

    /// <summary>
    /// Stores a network profile message
    /// </summary>
    public class NetworkProfileMessage
    {
        public NetworkDirection Direction;
        public Type Type;
        public string Name;
        public int Count;
    }

    /// <summary>
    /// Profiler used to profiling Mirror Low Level and High Level Activities
    /// </summary>
    public class NetworkProfiler
    {
        /// <summary>
        /// Set to true if profiler should record and fire an event on each server tick
        /// Defaults to false
        /// </summary>
        public static bool IsRecording { get; set; } = false;

        /// <summary>
        /// Event that is fired at the end of every server tick
        /// </summary>
        public static event Action<NetworkProfileTick> TickRecorded;

        private static NetworkProfileTick CurrentTick = new NetworkProfileTick();

        /// <summary>
        /// Records a message
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="messageType"></param>
        /// <param name="entryName"></param>
        /// <param name="count"></param>
        public static void RecordMessage(NetworkDirection direction, Type messageType, string entryName, int count)
        {
            if (NetworkProfiler.IsRecording)
            {
                NetworkProfiler.CurrentTick.RecordMessage(direction, messageType, entryName, count);
            }
        }

        /// <summary>
        /// Completes the collection of this frame and marks the frame tick
        /// </summary>
        /// <param name="newTime"></param>
        public static void Tick(float newTime)
        {
#if !MIRROR_PROFILING
            throw new InvalidOperationException("Network Profiling Ticks will return nothing without setting the MIRROR_PROFILING define");
#endif

            if (NetworkProfiler.IsRecording)
            {
                NetworkProfiler.CurrentTick.Time = newTime;
                NetworkProfiler.TickRecorded?.Invoke(NetworkProfiler.CurrentTick);
                NetworkProfiler.CurrentTick = new NetworkProfileTick();
            }
        }

        /// <summary>
        /// Records transport traffic
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="channelId"></param>
        /// <param name="numBytes"></param>
        public static void RecordTraffic(NetworkDirection direction, int channelId, int numBytes)
        {
            if (NetworkProfiler.IsRecording)
            {
                foreach (NetworkProfileChannel c in NetworkProfiler.CurrentTick.Channels)
                {
                    if (c.Id == channelId)
                    {
                        if (direction == NetworkDirection.Incoming)
                        {
                            c.BytesIncoming += numBytes;
                        }
                        else
                        {
                            c.BytesOutgoing += numBytes;
                        }
                        return;
                    }
                }

                NetworkProfiler.CurrentTick.Channels.Add(new NetworkProfileChannel
                {
                    Id = channelId,
                    BytesIncoming = direction == NetworkDirection.Incoming ? numBytes : 0,
                    BytesOutgoing = direction == NetworkDirection.Outgoing ? numBytes : 0,
                });
            }
        }
    }
}
