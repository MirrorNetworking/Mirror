#if UNITY_WEBGL && !UNITY_EDITOR

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;

namespace Mirror.Websocket
{
    // this is the client implementation used by browsers
    public class Client : Common
    {
        static int idGenerator = 0;
        static Client client;

        public event Action Connected;
        public event Action<ArraySegment<byte>> ReceivedData;
        public event Action Disconnected;
#pragma warning disable CS0067 // The event is never used
        public event Action<Exception> ReceivedError;
#pragma warning restore CS0067 // The event is never used

        public bool IsConnected
        {
            get
            {
                return SocketState(nativeRef) != 0;
            }
        }

        int nativeRef = 0;
        readonly int id;

        public Client()
        {
            id = Interlocked.Increment(ref idGenerator);
        }

        public void Connect(Uri uri)
        {
            client = this;

            Connecting = true;

            nativeRef = SocketCreate(uri.ToString(), id, OnOpen, OnData, OnClose);
        }

        public void Disconnect()
        {
            SocketClose(nativeRef);
        }

        // send the data or throw exception
        public void Send(ArraySegment<byte> segment)
        {
            SocketSend(nativeRef, segment.Array, segment.Count);
        }

        public void ProcessClientMessage()
        {
            if (client.GetNextMessage(out byte[] data))
                client.ReceivedData(new ArraySegment<byte>(data));
        }

        #region Javascript native functions

        [DllImport("__Internal")]
        static extern int SocketCreate(
            string url,
            int id,
            Action<int> onpen,
            Action<int, IntPtr, int> ondata,
            Action<int> onclose);

        [DllImport("__Internal")]
        static extern int SocketState(int socketInstance);

        [DllImport("__Internal")]
        static extern void SocketSend(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        static extern void SocketClose(int socketInstance);

        #endregion

        #region Javascript callbacks

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnOpen(int id)
        {
            client.Connecting = false;
            client.Connected?.Invoke();
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnClose(int id)
        {
            client.Connecting = false;
            client.Disconnected?.Invoke();
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnData(int id, IntPtr ptr, int length)
        {
            byte[] data = new byte[length];
            Marshal.Copy(ptr, data, 0, length);

            UnityEngine.Debug.LogError(client.enabled);

            //client.ReceivedData(new ArraySegment<byte>(data));
            client.receiveQueue.Enqueue(data);
        }

        #endregion
    }
}

#endif
