using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    internal sealed class Connection : IDisposable
    {
        public const int IdNotSet = -1;

        readonly object disposedLock = new object();

        public TcpClient client;
        // used for ToString
        readonly string endpoint;

        public int connId = IdNotSet;
        public Stream stream;
        public Thread receiveThread;
        public Thread sendThread;

        public ManualResetEventSlim sendPending = new ManualResetEventSlim(false);
        public ConcurrentQueue<ArrayBuffer> sendQueue = new ConcurrentQueue<ArrayBuffer>();

        public Action<Connection> onDispose;

        volatile bool hasDisposed;

        public Connection(TcpClient client, Action<Connection> onDispose)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            endpoint = client.Client.RemoteEndPoint.ToString();
            this.onDispose = onDispose;
        }


        /// <summary>
        /// disposes client and stops threads
        /// </summary>
        public void Dispose()
        {
            Log.Verbose($"Dispose {ToString()}");

            // check hasDisposed first to stop ThreadInterruptedException on lock
            if (hasDisposed) { return; }

            Log.Info($"Connection Close: {ToString()}");


            lock (disposedLock)
            {
                // check hasDisposed again inside lock to make sure no other object has called this
                if (hasDisposed) { return; }
                hasDisposed = true;

                // stop threads first so they dont try to use disposed objects
                receiveThread.Interrupt();
                sendThread?.Interrupt();

                try
                {
                    // stream 
                    stream?.Dispose();
                    stream = null;
                    client.Dispose();
                    client = null;
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }

                sendPending.Dispose();

                // release all buffers in send queue
                while (sendQueue.TryDequeue(out ArrayBuffer buffer))
                {
                    buffer.Release();
                }

                onDispose.Invoke(this);
            }
        }

        public override string ToString()
        {
            return $"[Conn:{connId}, endPoint:{endpoint}]";
        }
    }
}
