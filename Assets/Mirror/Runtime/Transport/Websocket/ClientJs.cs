#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AOT;

namespace Mirror.Websocket
{
    // this is the client implementation used by browsers
    public class Client
    {
        static int idGenerator = 0;
        static readonly Dictionary<int, Client> clients = new Dictionary<int, Client>();

        public bool NoDelay = true;

        public event Action Connected;
        public event Action<ArraySegment<byte>> ReceivedData;
        public event Action Disconnected;
        public event Action<Exception> ReceivedError;

        public bool Connecting { get; set; }
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

        public Task ConnectAsync(Uri uri)
        {
            clients[id] = this;

            Connecting = true;

            nativeRef = SocketCreate(uri.ToString(), id, OnOpen, OnData, OnClose);
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            SocketClose(nativeRef);
        }

        // send the data or throw exception
        public Task SendAsync(ArraySegment<byte> segment)
        {
            SocketSend(nativeRef, segment.Array, segment.Count);
            return Task.CompletedTask;
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
            clients[id].Connecting = false;
            clients[id].Connected?.Invoke();
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnClose(int id)
        {
            clients[id].Connecting = false;
            clients[id].Disconnected?.Invoke();
            clients.Remove(id);
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnData(int id, IntPtr ptr, int length)
        {
            byte[] data = new byte[length];
            Marshal.Copy(ptr, data, 0, length);

            clients[id].ReceivedData(new ArraySegment<byte>(data));
        }
#endregion
    }
}

#endif
