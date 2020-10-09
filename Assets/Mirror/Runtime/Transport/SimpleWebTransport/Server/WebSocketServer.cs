using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Mirror.SimpleWeb
{
    public class WebSocketServer
    {
        public readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

        readonly bool noDelay;
        readonly int sendTimeout;
        readonly int receiveTimeout;
        readonly int maxMessageSize;
        readonly SslConfig sslConfig;

        TcpListener listener;
        Thread acceptThread;
        readonly ServerHandshake handShake = new ServerHandshake();
        readonly ServerSslHelper sslHelper;
        readonly ConcurrentDictionary<int, Connection> connections = new ConcurrentDictionary<int, Connection>();

        int _previousId = 0;

        int GetNextId()
        {
            _previousId++;
            return _previousId;
        }

        public WebSocketServer(bool noDelay, int sendTimeout, int receiveTimeout, int maxMessageSize, SslConfig sslConfig)
        {
            this.noDelay = noDelay;
            this.sendTimeout = sendTimeout;
            this.receiveTimeout = receiveTimeout;
            this.maxMessageSize = maxMessageSize;
            this.sslConfig = sslConfig;
            sslHelper = new ServerSslHelper(this.sslConfig);
        }

        public void Listen(int port)
        {
            listener = TcpListener.Create(port);
            listener.Server.NoDelay = noDelay;
            listener.Server.SendTimeout = sendTimeout;
            listener.Start();

            Debug.Log($"Server has started on port {port}.\nWaiting for a connection...");

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

            Connection[] connections = this.connections.Values.ToArray();
            foreach (Connection conn in connections)
            {
                conn.Close();
            }

            this.connections.Clear();
        }

        void acceptLoop()
        {
            try
            {
                try
                {
                    while (true)
                    {
                        // TODO check this is blocking?
                        TcpClient client = listener.AcceptTcpClient();
                        client.SendTimeout = sendTimeout;
                        client.ReceiveTimeout = receiveTimeout;
                        client.NoDelay = noDelay;

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
                    CheckForInterupt();
                    throw;
                }
            }
            catch (ThreadInterruptedException) { Log.Info("acceptLoop ThreadInterrupted"); return; }
            catch (ThreadAbortException) { Log.Info("acceptLoop ThreadAbort"); return; }
            catch (Exception e) { Debug.LogException(e); }
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
                int bufferSize = Constants.HeaderSize + maxMessageSize;
                SendLoop.Loop(conn, bufferSize, false, CloseConnection);
            });

            conn.sendThread = sendThread;
            sendThread.IsBackground = true;
            sendThread.Start();

            ReceiveLoop(conn);
        }

        void ReceiveLoop(Connection conn)
        {
            try
            {
                TcpClient client = conn.client;
                Stream stream = conn.stream;
                //byte[] buffer = conn.receiveBuffer;
                byte[] headerBuffer = new byte[Constants.HeaderSize];

                while (client.Connected)
                {
                    bool success = ReadOneMessage(conn, stream, headerBuffer);
                    if (!success)
                        break;
                }
            }
            catch (ObjectDisposedException) { Log.Info($"ReceiveLoop {conn} Stream closed"); return; }
            catch (ThreadInterruptedException) { Log.Info($"ReceiveLoop {conn} ThreadInterrupted"); return; }
            catch (ThreadAbortException) { Log.Info($"ReceiveLoop {conn} ThreadAbort"); return; }
            catch (InvalidDataException e)
            {
                Log.Error($"Invalid data from {conn}: {e.Message}");
                receiveQueue.Enqueue(new Message(conn.connId, e));
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                CloseConnection(conn);
            }
        }

        private bool ReadOneMessage(Connection conn, Stream stream, byte[] headerBuffer)
        {
            // header is at most 4 bytes + mask
            // 1 for bit fields
            // 1+ for length (length can be be 1, 3, or 9 and we refuse 9)
            // 4 for mask (we can read this later
            ReadHelper.ReadResult readResult = ReadHelper.SafeRead(stream, headerBuffer, 0, Constants.HeaderSize, checkLength: true);
            if ((readResult & ReadHelper.ReadResult.Fail) > 0)
            {
                Log.Info($"ReceiveLoop {conn.connId} read failed: {readResult}");
                CheckForInterupt();
                // will go to finally block below
                return false;
            }

            MessageProcessor.Result header = MessageProcessor.ProcessHeader(headerBuffer, maxMessageSize, true);

            // todo remove allocation
            // mask + msg
            byte[] buffer = new byte[Constants.HeaderSize + header.readLength];
            for (int i = 0; i < Constants.HeaderSize; i++)
            {
                // copy header as it might contain mask
                buffer[i] = headerBuffer[i];
            }

            ReadHelper.SafeRead(stream, buffer, Constants.HeaderSize, header.readLength);

            MessageProcessor.ToggleMask(buffer, header.offset + Constants.MaskSize, header.msgLength, buffer, header.offset);

            // dump after mask off
            Log.DumpBuffer($"Message From Client {conn}", buffer, 0, buffer.Length);

            HandleMessage(header.opcode, conn, buffer, header.msgOffset, header.msgLength);
            return true;
        }

        static void CheckForInterupt()
        {
            // sleep in order to check for ThreadInterruptedException
            Thread.Sleep(1);
        }

        void HandleMessage(int opcode, Connection conn, byte[] buffer, int offset, int length)
        {
            if (opcode == 2)
            {
                ArraySegment<byte> data = new ArraySegment<byte>(buffer, offset, length);

                receiveQueue.Enqueue(new Message(conn.connId, data));
            }
            else if (opcode == 8)
            {
                Log.Info($"Close: {buffer[offset + 0] << 8 | buffer[offset + 1]} message:{Encoding.UTF8.GetString(buffer, offset + 2, length - 2)}");
                CloseConnection(conn);
            }
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

        public void Send(int id, ArraySegment<byte> segment)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                conn.sendQueue.Enqueue(segment);
                conn.sendPending.Set();
            }
            else
            {
                Debug.LogError($"Cant send message to {id} because connection was not found in dictionary");
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
                Debug.LogError($"Cant close connection to {id} because connection was not found in dictionary");
                return null;
            }
        }
    }
}
