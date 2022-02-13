using System;
using System.Collections.Generic;
using System.Linq;
using AOT;

namespace Mirror.SimpleWeb
{
    public class WebSocketClientWebGl : SimpleWebClient
    {
        static readonly Dictionary<int, WebSocketClientWebGl> instances = new Dictionary<int, WebSocketClientWebGl>();

        /// <summary>
        /// key for instances sent between c# and js
        /// </summary>
        int index;

        /// <summary>
        /// Queue for messages sent by high level while still connecting, they will be sent after onOpen is called.
        /// <para>
        ///     This is a workaround for anything that calls Send immediately after Connect.
        ///     Without this the JS websocket will give errors.
        /// </para>
        /// </summary>
        Queue<byte[]> ConnectingSendQueue;

        internal WebSocketClientWebGl(int maxMessageSize, int maxMessagesPerTick) : base(maxMessageSize, maxMessagesPerTick)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            throw new NotSupportedException();
#endif
        }

        public bool CheckJsConnected() => SimpleWebJSLib.IsConnected(index);

        public override void Connect(Uri serverAddress)
        {
            index = SimpleWebJSLib.Connect(serverAddress.ToString(), OpenCallback, CloseCallBack, MessageCallback, ErrorCallback);
            instances.Add(index, this);
            state = ClientState.Connecting;
        }

        public override void Disconnect()
        {
            state = ClientState.Disconnecting;
            // disconnect should cause closeCallback and OnDisconnect to be called
            SimpleWebJSLib.Disconnect(index);
        }

        public override void Send(ArraySegment<byte> segment)
        {
            if (segment.Count > maxMessageSize)
            {
                Log.Error($"Cant send message with length {segment.Count} because it is over the max size of {maxMessageSize}");
                return;
            }

            if (state == ClientState.Connected)
            {
                SimpleWebJSLib.Send(index, segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                if (ConnectingSendQueue == null)
                    ConnectingSendQueue = new Queue<byte[]>();
                ConnectingSendQueue.Enqueue(segment.ToArray());
            }
        }

        void onOpen()
        {
            receiveQueue.Enqueue(new Message(EventType.Connected));
            state = ClientState.Connected;

            if (ConnectingSendQueue != null)
            {
                while (ConnectingSendQueue.Count > 0)
                {
                    byte[] next = ConnectingSendQueue.Dequeue();
                    SimpleWebJSLib.Send(index, next, 0, next.Length);
                }
                ConnectingSendQueue = null;
            }
        }

        void onClose()
        {
            // this code should be last in this class

            receiveQueue.Enqueue(new Message(EventType.Disconnected));
            state = ClientState.NotConnected;
            instances.Remove(index);
        }

        void onMessage(IntPtr bufferPtr, int count)
        {
            try
            {
                ArrayBuffer buffer = bufferPool.Take(count);
                buffer.CopyFrom(bufferPtr, count);

                receiveQueue.Enqueue(new Message(buffer));
            }
            catch (Exception e)
            {
                Log.Error($"onData {e.GetType()}: {e.Message}\n{e.StackTrace}");
                receiveQueue.Enqueue(new Message(e));
            }
        }

        void onErr()
        {
            receiveQueue.Enqueue(new Message(new Exception("Javascript Websocket error")));
            Disconnect();
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        static void OpenCallback(int index) => instances[index].onOpen();

        [MonoPInvokeCallback(typeof(Action<int>))]
        static void CloseCallBack(int index) => instances[index].onClose();

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        static void MessageCallback(int index, IntPtr bufferPtr, int count) => instances[index].onMessage(bufferPtr, count);

        [MonoPInvokeCallback(typeof(Action<int>))]
        static void ErrorCallback(int index) => instances[index].onErr();
    }
}
