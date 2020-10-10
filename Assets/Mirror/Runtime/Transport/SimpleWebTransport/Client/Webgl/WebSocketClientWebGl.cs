using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    internal class WebSocketClientWebGl : SimpleWebClient
    {
        static readonly Dictionary<int, WebSocketClientWebGl> instances = new Dictionary<int, WebSocketClientWebGl>();

        readonly int maxMessageSize;
        public int index;

        internal WebSocketClientWebGl(int maxMessageSize, int maxMessagesPerTick) : base(maxMessagesPerTick)
        {
            this.maxMessageSize = maxMessageSize;
#if !UNITY_WEBGL || UNITY_EDITOR
            throw new NotSupportedException();
#endif
        }

        public bool CheckJsConnected() => SimpleWebJSLib.IsConnected(index);

        public override void Connect(string address)
        {
            index = SimpleWebJSLib.Connect(address, OpenCallback, CloseCallBack, MessageCallback, ErrorCallback);
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
                Debug.LogError($"Cant send message with length {segment.Count} because it is over the max size of {maxMessageSize}");
                return;
            }

            SimpleWebJSLib.Send(index, segment.Array, 0, segment.Count);
        }

        void onOpen()
        {
            receiveQueue.Enqueue(new Message(EventType.Connected));
            state = ClientState.Connected;
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
                byte[] buffer = new byte[count];
                Marshal.Copy(bufferPtr, buffer, 0, count);

                ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, count);
                receiveQueue.Enqueue(new Message(segment));
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
