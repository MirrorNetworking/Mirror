using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    internal class WebSocketClientWebGl : WebSocketClientBase, IWebSocketClient
    {
        static WebSocketClientWebGl instance;

        readonly int maxMessageSize;

        internal WebSocketClientWebGl(int maxMessageSize, int maxMessagesPerTick) : base(maxMessagesPerTick)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            instance = this;
            this.maxMessageSize = maxMessageSize;
#else
            throw new NotSupportedException();
#endif
        }

        public bool CheckJsConnected() => SimpleWebJSLib.IsConnected();

        public override void Connect(string address)
        {
            SimpleWebJSLib.Connect(address, OpenCallback, CloseCallBack, MessageCallback, ErrorCallback);
            state = ClientState.Connecting;
        }

        public override void Disconnect()
        {
            instance.state = ClientState.Disconnecting;
            // disconnect should cause closeCallback and OnDisconnect to be called
            SimpleWebJSLib.Disconnect();
        }

        public override void Send(ArraySegment<byte> segment)
        {
            if (segment.Count > maxMessageSize)
            {
                Debug.LogError($"Cant send message with length {segment.Count} because it is over the max size of {maxMessageSize}");
                return;
            }

            SimpleWebJSLib.Send(segment.Array, 0, segment.Count);
        }


        [MonoPInvokeCallback(typeof(Action))]
        static void OpenCallback()
        {
            instance.receiveQueue.Enqueue(new Message(EventType.Connected));
            instance.state = ClientState.Connected;
        }

        [MonoPInvokeCallback(typeof(Action))]
        static void CloseCallBack()
        {
            instance.receiveQueue.Enqueue(new Message(EventType.Disconnected));
            instance.state = ClientState.NotConnected;
            SimpleWebClient.RemoveInstance();
        }

        [MonoPInvokeCallback(typeof(Action<IntPtr, int>))]
        static void MessageCallback(IntPtr bufferPtr, int count)
        {
            try
            {
                byte[] buffer = new byte[count];
                Marshal.Copy(bufferPtr, buffer, 0, count);

                ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, count);
                instance.receiveQueue.Enqueue(new Message(segment));
            }
            catch (Exception e)
            {
                Log.Error($"onData {e.GetType()}: {e.Message}\n{e.StackTrace}");
                instance.receiveQueue.Enqueue(new Message(e));
            }
        }

        [MonoPInvokeCallback(typeof(Action))]
        static void ErrorCallback()
        {
            instance.receiveQueue.Enqueue(new Message(new Exception("Javascript Websocket error")));
            SimpleWebJSLib.Disconnect();
            instance.state = ClientState.NotConnected;
        }
    }
}
