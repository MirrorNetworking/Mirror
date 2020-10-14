using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public class WebSocketServer
    {
        public readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

        readonly TcpConfig tcpConfig;
        readonly int maxMessageSize;

        TcpListener listener;
        Thread acceptThread;
        readonly ServerHandshake handShake = new ServerHandshake();
        readonly ServerSslHelper sslHelper;
        readonly BufferPool bufferPool;
        readonly ConcurrentDictionary<int, Connection> connections = new ConcurrentDictionary<int, Connection>();

        int _previousId = 0;

        int GetNextId()
        {
            _previousId++;
            return _previousId;
        }

        public WebSocketServer(TcpConfig tcpConfig, int maxMessageSize, SslConfig sslConfig, BufferPool bufferPool)
        {
            this.tcpConfig = tcpConfig;
            this.maxMessageSize = maxMessageSize;
            sslHelper = new ServerSslHelper(sslConfig);
            this.bufferPool = bufferPool;
        }

        public void Listen(int port)
        {
            listener = TcpListener.Create(port);
            listener.Start();

            Log.Info($"Server has started on port {port}");

            acceptThread = new Thread(acceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        public void Stop()
        {
            // Interrupt then stop so that Exception is handled correctly
            acceptThread?.Interrupt();
            listener?.Stop();
            acceptThread = null;

            // make copy so that foreach doesn't break if values are removed
            Connection[] connectionsCopy = connections.Values.ToArray();
            foreach (Connection conn in connectionsCopy)
            {
                conn.Close();
            }

            connections.Clear();
        }

        void acceptLoop()
        {
            try
            {
                try
                {
                    while (true)
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        tcpConfig.ApplyTo(client);

                        // TODO keep track of connections before they are in connections dictionary
                        Connection conn = new Connection(client);
                        Log.Info($"A client connected {conn}");

                        // handshake needs its own thread as it needs to wait for message from client
                        Thread receiveThread = new Thread(() => HandshakeAndReceiveLoop(conn));

                        conn.receiveThread = receiveThread;

                        receiveThread.IsBackground = true;
                        receiveThread.Start();
                    }
                }
                catch (SocketException)
                {
                    // check for Interrupted/Abort
                    Utils.CheckForInterupt();
                    throw;
                }
            }
            catch (ThreadInterruptedException) { Log.Info("acceptLoop ThreadInterrupted"); }
            catch (ThreadAbortException) { Log.Info("acceptLoop ThreadAbort"); }
            catch (Exception e) { Log.Exception(e); }
        }

        void HandshakeAndReceiveLoop(Connection conn)
        {
            bool success = sslHelper.TryCreateStream(conn);
            if (!success)
            {
                Log.Error($"Failed to create SSL Stream {conn}");
                conn.client.Dispose();
                return;
            }

            success = handShake.TryHandshake(conn);

            if (!success)
            {
                Log.Error($"Handshake Failed {conn}");
                conn.client.Dispose();
                return;
            }

            conn.connId = GetNextId();
            connections.TryAdd(conn.connId, conn);

            receiveQueue.Enqueue(new Message(conn.connId, EventType.Connected));

            Thread sendThread = new Thread(() =>
            {
                SendLoop.Config sendConfig = new SendLoop.Config(
                    conn,
                    bufferSize: Constants.HeaderSize + maxMessageSize,
                    setMask: false,
                    CloseConnection);

                SendLoop.Loop(sendConfig);
            });

            conn.sendThread = sendThread;
            sendThread.IsBackground = true;
            sendThread.Name = $"SendLoop {conn.connId}";
            sendThread.Start();

            ReceiveLoop.Config receiveConfig = new ReceiveLoop.Config(
                conn,
                maxMessageSize,
                expectMask: true,
                receiveQueue,
                CloseConnection,
                bufferPool);

            ReceiveLoop.Loop(receiveConfig);
        }

        void CloseConnection(Connection conn)
        {
            bool closed = conn.Close();
            // only send disconnect message if closed by the call
            if (closed)
            {
                receiveQueue.Enqueue(new Message(conn.connId, EventType.Disconnected));
                connections.TryRemove(conn.connId, out Connection _);
            }
        }

        public void Send(int id, ArrayBuffer buffer)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                conn.sendQueue.Enqueue(buffer);
                conn.sendPending.Set();
            }
            else
            {
                Log.Warn($"Cant send message to {id} because connection was not found in dictionary. Maybe it disconnected.");
            }
        }

        public bool CloseConnection(int id)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                CloseConnection(conn);
                return true;
            }
            else
            {
                return false;
            }
        }

        public string GetClientAddress(int id)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                return conn.client.Client.RemoteEndPoint.ToString();
            }
            else
            {
                Log.Error($"Cant close connection to {id} because connection was not found in dictionary");
                return null;
            }
        }
    }
}
