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
        public class SendMessageEvent : UnityEvent<IMessageBase, int, int, int> { };

        public static SendMessageEvent SendMessage = new SendMessageEvent();


        [Conditional("ENABLE_PROFILER")]
        public static void Send<T>(T message, int channel, int bytes, int count) where T : IMessageBase
        {
            SendMessage.Invoke(message, channel, bytes, count);
        }

        public class ReceiveMessageEvent : UnityEvent<IMessageBase, int, int> { };

        public static ReceiveMessageEvent ReceiveMessage = new ReceiveMessageEvent();
        
        [Conditional("ENABLE_PROFILER")]
        public static void Receive<T>(T message, int channel, int bytes) where T : IMessageBase
        {
            ReceiveMessage.Invoke(message, channel, bytes);
        }
    }
}