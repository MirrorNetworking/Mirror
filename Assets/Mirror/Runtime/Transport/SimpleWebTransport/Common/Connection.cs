using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    internal class Connection
    {
        public object lockObj = new object();

        public TcpClient client;
        // used for ToString
        private readonly string endpoint;

        public bool hasClosed;
        public int connId = -1;
        public Stream stream;
        public Thread receiveThread;
        public Thread sendThread;

        public ManualResetEventSlim sendPending = new ManualResetEventSlim(false);
        public ConcurrentQueue<ArrayBuffer> sendQueue = new ConcurrentQueue<ArrayBuffer>();

        public Connection(TcpClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            endpoint = client.Client.RemoteEndPoint.ToString();
        }

        /// <summary>
        /// disposes client and stops threads
        /// </summary>
        /// <returns>return true if closed by this call, false if was already closed</returns>
        public bool Close()
        {
            // check hasClosed first to stop ThreadInterruptedException on lock
            if (hasClosed) { return false; }

            Log.Info($"Connection Close: {ToString()}");

            lock (lockObj)
            {
                // check hasClosed again inside lock to make sure no other object has called this
                if (hasClosed) { return false; }
                hasClosed = true;

                // stop threads first so they dont try to use disposed objects
                receiveThread.Interrupt();
                sendThread?.Interrupt();

                stream?.Dispose();
                stream = null;
                client.Dispose();
                client = null;

                sendPending.Dispose();

                // Todo is this ok to run on the main thread?? will it have negative performance if queue is large
                // release all buffers in send queue
                while (sendQueue.TryDequeue(out ArrayBuffer buffer))
                {
                    buffer.Release();
                }

                return true;
            }
        }

        public override string ToString()
        {
            return $"[Conn:{connId}, endPoint:{endpoint}]";
        }
    }
}
