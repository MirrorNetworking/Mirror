using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    public enum ClientState
    {
        NotConnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
    }

    /// <summary>
    /// Client used to control websockets
    /// <para>Base class used by WebSocketClientWebGl and WebSocketClientStandAlone</para>
    /// </summary>
    public abstract class SimpleWebClient
    {
        readonly int maxMessagesPerTick;

        protected ClientState state;
        protected readonly int maxMessageSize;
        protected readonly BufferPool bufferPool;

        public readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

        public ClientState ConnectionState => state;

        public event Action onConnect;
        public event Action onDisconnect;
        public event Action<ArraySegment<byte>> onData;
        public event Action<Exception> onError;

        public abstract void Connect(Uri serverAddress);
        public abstract void Disconnect();
        public abstract void Send(ArraySegment<byte> segment);

        public static SimpleWebClient Create(int maxMessageSize, int maxMessagesPerTick, TcpConfig tcpConfig)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebSocketClientWebGl(maxMessageSize, maxMessagesPerTick);
#else
            return new WebSocketClientStandAlone(maxMessageSize, maxMessagesPerTick, tcpConfig);
#endif
        }

        protected SimpleWebClient(int maxMessageSize, int maxMessagesPerTick)
        {
            this.maxMessageSize = maxMessageSize;
            this.maxMessagesPerTick = maxMessagesPerTick;
            bufferPool = new BufferPool(5, 20, maxMessageSize);
        }

        /// <summary>
        /// Processes all new messages
        /// </summary>
        public void ProcessMessageQueue()
        {
            ProcessMessageQueue(null);
        }

        /// <summary>
        /// Processes all messages while <paramref name="behaviour"/> is enabled
        /// </summary>
        /// <param name="behaviour"></param>
        public void ProcessMessageQueue(MonoBehaviour behaviour)
        {
            int processedCount = 0;
            bool skipEnabled = behaviour == null;
            // check enabled every time in case behaviour was disabled after data
            while (
                (skipEnabled || behaviour.enabled) &&
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
                        onData?.Invoke(next.data.ToSegment());
                        next.data.Release();
                        break;
                    case EventType.Disconnected:
                        onDisconnect?.Invoke();
                        break;
                    case EventType.Error:
                        onError?.Invoke(next.exception);
                        break;
                }
            }
            if (receiveQueue.Count > 0)
                Log.Warn("[SWT-SimpleWebClient]: ProcessMessageQueue has {0} remaining.", receiveQueue.Count);
        }
    }
}
