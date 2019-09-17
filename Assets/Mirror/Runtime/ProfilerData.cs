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
        public readonly struct OutMessage
        {
            public readonly IMessageBase message;
            public readonly int channel;
            public readonly int bytes;
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

        public static OutMessageUnityEvent OutMessageEvent = new OutMessageUnityEvent();

        [Conditional("ENABLE_PROFILER")]
        internal static void Send<T>(T message, int channel, int bytes, int count) where T : IMessageBase
        {
            OutMessage outMessage = new OutMessage(message, channel, bytes, count);
            OutMessageEvent.Invoke(outMessage);
        }
        #endregion

        #region In messages

        public readonly struct InMessage
        {
            public readonly IMessageBase message;
            public readonly int channel;
            public readonly int bytes;

            internal InMessage(IMessageBase message, int channel, int bytes)
            {
                this.message = message;
                this.channel = channel;
                this.bytes = bytes;
            }
        }

        public class InMessageUnityEvent : UnityEvent<InMessage> { };

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