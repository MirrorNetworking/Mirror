using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public struct NetworkProfileChannel
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
    public struct NetworkProfileTick
    {
        /// <summary>
        /// The Time.time at the moment tick collection ended
        /// </summary>
        public int frameCount;

        /// <summary>
        /// The total number of messages captured during this tick
        /// </summary>
        public int TotalMessages;

        private List<NetworkProfileMessage> messages;

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

        private List<NetworkProfileChannel> channels;
        /// <summary>
        /// The channels that sent or received data during the tick
        /// </summary>
        public List<NetworkProfileChannel> Channels
        {
            get
            {
                channels = channels ?? new List<NetworkProfileChannel>();
                return channels;
            }
        }


        /// <summary>
        /// Records the message to the current tick
        /// </summary>
        /// <param name="direction">The direction of the message</param>
        /// <param name="messageType">The message type</param>
        /// <param name="entryName">The name of the entry, generally represents the target of the message</param>
        /// <param name="count">The number of these messages sent</param>
        public void RecordMessage(NetworkDirection direction, Type messageType, string entryName, int count)
        {
            TotalMessages += count;

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
    public static class NetworkProfiler
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
            if (IsRecording)
            {
                CurrentTick.RecordMessage(direction, messageType, entryName, count);
            }
        }

        /// <summary>
        /// Completes the collection of this frame and marks the frame tick
        /// </summary>
        [Conditional("MIRROR_PROFILING")]
        public static void Tick()
        {
            if (IsRecording)
            {
                if (CurrentTick.TotalMessages > 0)
                {
                    CurrentTick.frameCount = Time.frameCount;
                    TickRecorded?.Invoke(CurrentTick);
                    CurrentTick = new NetworkProfileTick();
                }
                CurrentTick.frameCount = Time.frameCount;
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
            if (IsRecording)
            {
                for (int i = 0; i< CurrentTick.Channels.Count; i++)
                {
                    NetworkProfileChannel channel = CurrentTick.Channels[i];

                    if (CurrentTick.Channels[i].Id == channelId)
                    {
                        if (direction == NetworkDirection.Incoming)
                        {
                            channel.BytesIncoming += numBytes;
                        }
                        else
                        {
                            channel.BytesOutgoing += numBytes;
                        }

                        CurrentTick.Channels[i] = channel;
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
