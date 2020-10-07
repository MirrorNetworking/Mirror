using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    public interface IWebSocketClient
    {
        event Action onConnect;
        event Action onDisconnect;
        event Action<ArraySegment<byte>> onData;
        event Action<Exception> onError;

        ClientState ConnectionState { get; }
        void Connect(string address);
        void Disconnect();
        void Send(ArraySegment<byte> segment);
        void ProcessMessageQueue(MonoBehaviour behaviour);
    }

    public enum ClientState
    {
        NotConnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
    }
    public abstract class WebSocketClientBase : IWebSocketClient
    {
        readonly int maxMessagesPerTick;
        protected readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();
        protected ClientState state;

        protected WebSocketClientBase(int maxMessagesPerTick)
        {
            this.maxMessagesPerTick = maxMessagesPerTick;
        }
        public ClientState ConnectionState => state;

        public event Action onConnect;
        public event Action onDisconnect;
        public event Action<ArraySegment<byte>> onData;
        public event Action<Exception> onError;

        public void ProcessMessageQueue(MonoBehaviour behaviour)
        {
            int processedCount = 0;
            // check enabled every time incase behaviour was disabled after data
            while (
                behaviour.enabled &&
                processedCount < maxMessagesPerTick &&
                // Dequeue last
                receiveQueue.TryDequeue(out Message next)
                )
            {
                processedCount++;

                switch (next.type)
                {
                    case EventType.Connected:
                        onConnect?.Invoke();
                        break;
                    case EventType.Data:
                        onData?.Invoke(next.data);
                        break;
                    case EventType.Disconnected:
                        onDisconnect?.Invoke();
                        break;
                    case EventType.Error:
                        onError?.Invoke(next.exception);
                        break;
                }
            }
        }

        public abstract void Connect(string address);
        public abstract void Disconnect();
        public abstract void Send(ArraySegment<byte> segment);
    }

    public static class SimpleWebClient
    {
        static IWebSocketClient instance;

        public static IWebSocketClient Create(int maxMessageSize, int clientMaxMessagesPerTick)
        {
            if (instance != null)
            {
                Debug.LogError("Cant create SimpleWebClient while one already exists");
                return null;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            instance = new WebSocketClientWebGl(maxMessageSize, clientMaxMessagesPerTick);
#else
            instance = new WebSocketClientStandAlone(maxMessageSize, clientMaxMessagesPerTick);
#endif
            return instance;
        }

        public static void CloseExisting()
        {
            instance?.Disconnect();
            instance = null;
        }

        /// <summary>
        /// Called by IWebSocketClient on disconnect
        /// </summary>
        internal static void RemoveInstance()
        {
            instance = null;
        }
    }
}
