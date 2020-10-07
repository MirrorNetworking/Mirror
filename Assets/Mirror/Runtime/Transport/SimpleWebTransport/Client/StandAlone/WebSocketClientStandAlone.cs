using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    internal class WebSocketClientStandAlone : WebSocketClientBase, IWebSocketClient
    {
        object lockObject = new object();
        bool hasClosed;

        const int HeaderLength = 4;

        readonly ClientSslHelper sslHelper;
        readonly ClientHandshake handshake;
        readonly RNGCryptoServiceProvider random;
        readonly int maxMessageSize;

        private Connection conn;

        internal WebSocketClientStandAlone(int maxMessageSize, int maxMessagesPerTick) : base(maxMessagesPerTick)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException();
#else
            this.maxMessageSize = maxMessageSize;
            sslHelper = new ClientSslHelper();
            handshake = new ClientHandshake();
            random = new RNGCryptoServiceProvider();
#endif
        }
        ~WebSocketClientStandAlone()
        {
            random?.Dispose();
        }

        public override void Connect(string address)
        {
            state = ClientState.Connecting;
            Thread receiveThread = new Thread(() => ConnectAndReceiveLoop(address));
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        void ConnectAndReceiveLoop(string address)
        {
            try
            {
                TcpClient client = new TcpClient();
                Uri uri = new Uri(address);
                try
                {
                    client.Connect(uri.Host, uri.Port);
                }
                catch (SocketException)
                {
                    client.Dispose();
                    throw;
                }

                conn = new Connection(client);
                conn.receiveThread = Thread.CurrentThread;

                bool success = sslHelper.TryCreateStream(conn, uri);
                if (!success)
                {
                    conn.Close();
                    return;
                }

                success = handshake.TryHandshake(conn, uri);
                if (!success)
                {
                    conn.Close();
                    return;
                }

                Log.Info("HandShake Successful");

                state = ClientState.Connected;

                receiveQueue.Enqueue(new Message(EventType.Connected));

                Thread sendThread = new Thread(() => SendLoop(conn));

                conn.sendThread = sendThread;
                sendThread.IsBackground = true;
                sendThread.Start();

                ReceiveLoop(conn);
            }
            catch (ThreadInterruptedException) { Log.Info("acceptLoop ThreadInterrupted"); return; }
            catch (ThreadAbortException) { Log.Info("acceptLoop ThreadAbort"); return; }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                // close here incase connect fails
                CloseConnection();
            }
        }

        void ReceiveLoop(Connection conn)
        {
            // todo remove duplicate code (this and WebSocketServer)
            try
            {
                TcpClient client = conn.client;
                Stream stream = conn.stream;
                //byte[] buffer = conn.receiveBuffer;
                byte[] headerBuffer = new byte[HeaderLength];

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
                Log.Error($"Invalid data: {e.Message}");
                receiveQueue.Enqueue(new Message(e));
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                CloseConnection();
            }
        }

        private bool ReadOneMessage(Connection conn, Stream stream, byte[] headerBuffer)
        {
            // header is at most 4 bytes + mask
            // 1 for bit fields
            // 1+ for length (length can be be 1, 3, or 9 and we refuse 9)
            // 4 for mask (we can read this later
            ReadHelper.ReadResult readResult = ReadHelper.SafeRead(stream, headerBuffer, 0, HeaderLength, checkLength: true);
            if ((readResult & ReadHelper.ReadResult.Fail) > 0)
            {
                Log.Info($"ReceiveLoop read failed: {readResult}");
                CheckForInterupt();
                // will go to finally block below
                return false;
            }

            MessageProcessor.Result header = MessageProcessor.ProcessHeader(headerBuffer, maxMessageSize, false);

            // todo remove allocation
            // msg
            byte[] buffer = new byte[HeaderLength + header.readLength];
            // copy header as it might contain mask
            Buffer.BlockCopy(headerBuffer, 0, buffer, 0, HeaderLength);

            ReadHelper.SafeRead(stream, buffer, HeaderLength, header.readLength);

            Log.DumpBuffer("Message From Server", buffer, 0, buffer.Length);
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

                receiveQueue.Enqueue(new Message(data));
            }
            else if (opcode == 8)
            {
                Log.Info($"Close: {buffer[offset + 0] << 8 | buffer[offset + 1]} message:{Encoding.UTF8.GetString(buffer, offset + 2, length - 2)}");
                CloseConnection();
            }
        }


        void SendLoop(Connection conn)
        {
            // todo remove duplicate code (this and WebSocketServer)
            try
            {
                TcpClient client = conn.client;
                Stream stream = conn.stream;

                // null check incase disconnect while send thread is starting
                if (client == null)
                    return;

                while (client.Connected)
                {
                    // wait for message
                    conn.sendPending.WaitOne();
                    conn.sendPending.Reset();

                    while (conn.sendQueue.TryDequeue(out ArraySegment<byte> msg))
                    {
                        // check if connected before sending message
                        if (!client.Connected) { Log.Info($"SendLoop {conn} not connected"); return; }

                        SendMessageToServer(stream, msg);
                    }
                }
            }
            catch (ThreadInterruptedException) { Log.Info($"SendLoop {conn} ThreadInterrupted"); return; }
            catch (ThreadAbortException) { Log.Info($"SendLoop {conn} ThreadAbort"); return; }
            catch (Exception e)
            {
                Debug.LogException(e);

                CloseConnection();
            }
        }

        void CloseConnection()
        {
            conn?.Close();

            if (hasClosed) { return; }

            // lock so that hasClosed can be safely set
            lock (lockObject)
            {
                hasClosed = true;

                state = ClientState.NotConnected;
                // make sure Disconnected event is only called once
                receiveQueue.Enqueue(new Message(EventType.Disconnected));
            }
        }

        byte[] maskBuffer = new byte[4];
        void SendMessageToServer(Stream stream, ArraySegment<byte> msg)
        {
            int msgLength = msg.Count;
            // todo remove allocation
            // header 2/4 + mask + msg
            byte[] buffer = new byte[4 + 4 + msgLength];
            int sendLength = 0;

            byte finished = 128;
            byte byteOpCode = 2;

            buffer[0] = (byte)(finished | byteOpCode);
            sendLength++;

            if (msgLength < 125)
            {
                buffer[1] = (byte)msgLength;
                sendLength++;
            }
            else if (msgLength < ushort.MaxValue)
            {
                buffer[1] = 126;
                buffer[2] = (byte)(msgLength >> 8);
                buffer[3] = (byte)msgLength;
                sendLength += 3;
            }
            else
            {
                throw new InvalidDataException($"Trying to send a message larger than {ushort.MaxValue} bytes");
            }

            // mask
            buffer[1] |= 0b1000_0000;
            random.GetBytes(maskBuffer);
            Array.Copy(maskBuffer, 0, buffer, sendLength, 4);
            sendLength += 4;

            Array.Copy(msg.Array, msg.Offset, buffer, sendLength, msgLength);

            // dump before mask on
            Log.DumpBuffer("Send To Server", buffer, 0, sendLength + msgLength);

            MessageProcessor.ToggleMask(buffer, sendLength, msgLength, buffer, sendLength - 4);
            sendLength += msgLength;

            stream.Write(buffer, 0, sendLength);
        }

        public override void Disconnect()
        {
            CloseConnection();
        }

        public override void Send(ArraySegment<byte> source)
        {
            byte[] buffer = new byte[source.Count];
            Array.Copy(source.Array, source.Offset, buffer, 0, source.Count);
            ArraySegment<byte> copy = new ArraySegment<byte>(buffer);

            conn.sendQueue.Enqueue(copy);
            conn.sendPending.Set();
        }
    }
}
