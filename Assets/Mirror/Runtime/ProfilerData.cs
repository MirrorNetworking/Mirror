using System;
using System.Diagnostics;
using UnityEngine.Events;

namespace Mirror
{
    /// <summary>
    /// Provides profiling information from mirror
    /// A profiler can subscribe to these events and
    /// present the data in a friendly way to the user
    /// </summary>
    public static class ProfilerData
    {
        #region Out messages

        /// <summary>
        /// Describes an outgoing message
        /// </summary>
        public readonly struct OutMessage
        {
            /// <summary>
            /// The message being sent
            /// </summary>
            public readonly IMessageBase message;
            /// <summary>
            /// channel through which the message was sent
            /// </summary>
            public readonly int channel;
            /// <summary>
            /// how big was the message (does not include transport headers)
            /// </summary>
            public readonly int bytes;
            /// <summary>
            /// How many connections was the message sent to
            /// If an object has a lot of observers this count could be high
            /// </summary>
            public readonly int count;

            internal OutMessage(IMessageBase message, int channel, int bytes, int count)
            {
                this.message = message;
                this.channel = channel;
                this.bytes = bytes;
                this.count = count;
            }
        }

        public class OutMessageUnityEvent : UnityEvent<OutMessage> { };

        /// <summary>
        /// Event that gets raised when Mirror sends a message
        /// Subscribe to this if you want to profile the network
        /// </summary>
        public static OutMessageUnityEvent OutMessageEvent = new OutMessageUnityEvent();

        [Conditional("ENABLE_PROFILER")]
        internal static void Send<T>(T message, int channel, int bytes, int count) where T : IMessageBase
        {
            OutMessage outMessage = new OutMessage(message, channel, bytes, count);
            OutMessageEvent.Invoke(outMessage);
        }
        #endregion

        #region In messages

        /// <summary>
        /// Describe a message received by mirror
        /// </summary>
        public readonly struct InMessage
        {
            /// <summary>
            /// The message received
            /// </summary>
            public readonly IMessageBase message;
            /// <summary>
            /// The channel through which the message was received
            /// </summary>
            public readonly int channel;
            /// <summary>
            /// How big was the message (not including transport headers)
            /// </summary>
            public readonly int bytes;

            internal InMessage(IMessageBase message, int channel, int bytes)
            {
                this.message = message;
                this.channel = channel;
                this.bytes = bytes;
            }
        }

        public class InMessageUnityEvent : UnityEvent<InMessage> { };

        /// <summary>
        /// Event that gets raised when Mirror receives a message
        /// Subscribe to this if you want to profile the network
        /// </summary>
        public static InMessageUnityEvent InMessageEvent = new InMessageUnityEvent();
        
        [Conditional("ENABLE_PROFILER")]
        internal static void Receive<T>(T message, int channel, int bytes) where T : IMessageBase
        {
            InMessage inMessage = new InMessage(message, channel, bytes);
            InMessageEvent.Invoke(inMessage);
        }

        #endregion
    }
}