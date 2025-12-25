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
        bool serverStopped;
        readonly ServerHandshake handShake;
        readonly ServerSslHelper sslHelper;
        readonly BufferPool bufferPool;
        readonly ConcurrentDictionary<int, Connection> connections = new ConcurrentDictionary<int, Connection>();

        int _idCounter = 0;

        public WebSocketServer(TcpConfig tcpConfig, int maxMessageSize, int handshakeMaxSize, SslConfig sslConfig, BufferPool bufferPool)
        {
            this.tcpConfig = tcpConfig;
            this.maxMessageSize = maxMessageSize;
            sslHelper = new ServerSslHelper(sslConfig);
            this.bufferPool = bufferPool;
            handShake = new ServerHandshake(this.bufferPool, handshakeMaxSize);
        }

        public void Listen(int port)
        {
            listener = TcpListener.Create(port);
            listener.Start();

            Log.Verbose("[SWT-WebSocketServer]: Server Started on {0}", port);

            acceptThread = new Thread(acceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        public void Stop()
        {
            serverStopped = true;

            // Interrupt then stop so that Exception is handled correctly
            acceptThread?.Interrupt();
            listener?.Stop();
            acceptThread = null;

            Log.Verbose("[SWT-WebSocketServer]: Server stopped...closing all connections.");

            // make copy so that foreach doesn't break if values are removed
            Connection[] connectionsCopy = connections.Values.ToArray();
            foreach (Connection conn in connectionsCopy)
                conn.Dispose();

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
                        //      this might not be a problem as HandshakeAndReceiveLoop checks for stop
                        //      and returns/disposes before sending message to queue
                        Connection conn = new Connection(client, AfterConnectionDisposed);
                        Log.Verbose("[SWT-WebSocketServer]: A client connected from {0}", conn);

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
            catch (ThreadInterruptedException e) { Log.InfoException("[SWT-WebSocketServer]", e); }
            catch (ThreadAbortException) { Log.Error("[SWT-WebSocketServer]: Thread Abort Exception"); }
            catch (Exception e) { Log.Exception("[SWT-WebSocketServer]", e); }
        }

        void HandshakeAndReceiveLoop(Connection conn)
        {
            try
            {
                bool success = sslHelper.TryCreateStream(conn);
                if (!success)
                {
                    Log.Warn("[SWT-WebSocketServer]: Failed to create SSL Stream {0}", conn);
                    return;
                }

                success = handShake.TryHandshake(conn);

                if (!success)
                {
                    Log.Warn("[SWT-WebSocketServer]: Handshake failed for connection {0}", conn);
                    return;
                }

                // check if Stop has been called since accepting this client
                if (serverStopped)
                {
                    Log.Warn("[SWT-WebSocketServer]: Server stopped after successful handshake");
                    return;
                }

                conn.connId = Interlocked.Increment(ref _idCounter);
                Log.Info("[SWT-WebSocketServer]: A client connected {0}", conn);

                connections.TryAdd(conn.connId, conn);

                receiveQueue.Enqueue(new Message(conn.connId, EventType.Connected));

                Thread sendThread = new Thread(() =>
                {
                    SendLoop.Config sendConfig = new SendLoop.Config(
                        conn,
                        bufferSize: Constants.HeaderSize + maxMessageSize,
                        setMask: false);

                    SendLoop.Loop(sendConfig);
                });

                conn.sendThread = sendThread;
                sendThread.IsBackground = true;
                sendThread.Name = $"SendThread {conn.connId}";
                sendThread.Start();

                ReceiveLoop.Config receiveConfig = new ReceiveLoop.Config(
                    conn,
                    maxMessageSize,
                    expectMask: true,
                    receiveQueue,
                    bufferPool);

                ReceiveLoop.Loop(receiveConfig);
            }
            catch (ThreadInterruptedException e) { Log.InfoException("[SWT-WebSocketServer]", e); }
            catch (ThreadAbortException) { Log.Error("[SWT-WebSocketServer]: Thread Abort Exception"); }
            catch (Exception e) { Log.Exception("[SWT-WebSocketServer]", e); }
            finally
            {
                // close here in case connect fails
                conn.Dispose();
            }
        }

        void AfterConnectionDisposed(Connection conn)
        {
            if (conn.connId != Connection.IdNotSet)
            {
                receiveQueue.Enqueue(new Message(conn.connId, EventType.Disconnected));
                if (connections.TryRemove(conn.connId, out Connection _))
                    Log.Verbose("[SWT-WebSocketServer]: AfterConnectionDisposed: Removed connection {0} from dictionary.", conn.connId);
                else
                    Log.Verbose("[SWT-WebSocketServer]: AfterConnectionDisposed: Failed to remove connection {0} from dictionary.", conn.connId);
            }

            // Don't invoke this again
            conn.onDispose = null;
        }

        public void Send(int id, ArrayBuffer buffer)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                conn.sendQueue.Enqueue(buffer);
                conn.sendPending.Set();
            }
            else
                Log.Warn("[SWT-WebSocketServer]: Send: cannot send message to {0} because it was not found in dictionary. Maybe it disconnected.", id);
        }

        public void CloseConnection(int id)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                Log.Info("[SWT-WebSocketServer]: CloseConnection: Disconnecting connection {0}", id);
                conn.Dispose();
            }
            else
                Log.Warn("[SWT-WebSocketServer]: Failed to kick {0} because id not found.", id);
        }

        public string GetClientAddress(int id)
        {
            if (!connections.TryGetValue(id, out Connection conn))
            {
                Log.Warn("[SWT-WebSocketServer]: GetClientAddress: Cannot get address of connection {0} because it was not found in dictionary.", id);
                return null;
            }

            return conn.remoteAddress;
        }

        public Request GetClientRequest(int id)
        {
            if (!connections.TryGetValue(id, out Connection conn))
            {
                Log.Warn("[SWT-WebSocketServer]: GetClientRequest: Cannot get request of connection {0} because it was not found in dictionary.", id);
                return null;
            }

            return conn.request;
        }
    }
}
