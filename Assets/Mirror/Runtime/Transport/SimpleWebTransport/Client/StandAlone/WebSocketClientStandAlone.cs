using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    internal class WebSocketClientStandAlone : SimpleWebClient
    {
        object lockObject = new object();
        bool hasClosed;

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
                client.NoDelay = true;
                client.ReceiveTimeout = 20000;
                client.SendTimeout = 5000;
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

                Thread sendThread = new Thread(() =>
                {
                    int bufferSize = Constants.HeaderSize + Constants.MaskSize + maxMessageSize;
                    SendLoop.Loop(conn, bufferSize, true, _ => CloseConnection());
                });

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

            // client header could only be 2 bytes, and message might be 1 byte, so min is 3 bytes not the default 4 for the server
            // todo: clean up read header logic
            ReadHelper.ReadResult readResult = ReadHelper.SafeRead(stream, headerBuffer, 0, Constants.HeaderSize - 2, checkLength: true);
            if ((readResult & ReadHelper.ReadResult.Fail) > 0)
            {
                Log.Info($"ReceiveLoop read failed: {readResult}");
                CheckForInterupt();
                // will go to finally block below
                return false;
            }
            byte payloadLength = MessageProcessor.GetBytePayloadLength(headerBuffer);

            // payloadLength 1 or 2 is a specail case because it mean message is less than 4 bytes
            // todo clean up this logic
            if (payloadLength > 2)
            {
                // read rest of header
                readResult = ReadHelper.SafeRead(stream, headerBuffer, 2, 2, checkLength: true);
                if ((readResult & ReadHelper.ReadResult.Fail) > 0)
                {
                    Log.Info($"ReceiveLoop read failed: {readResult}");
                    CheckForInterupt();
                    // will go to finally block below
                    return false;
                }
            }

            MessageProcessor.Result header = MessageProcessor.ProcessHeader(headerBuffer, maxMessageSize, false);

            // todo remove allocation
            // msg
            byte[] buffer = new byte[Constants.HeaderSize + header.readLength];


            if (payloadLength == 0)
            {
                Log.Info($"ReceiveLoop Receive a message with no length");
                return false;
            }
            else if (payloadLength <= 2)
            {
                // payloadLength 1 or 2 is a specail case because it mean message is less than 4 bytes
                // todo clean up this logic

                // read message
                readResult = ReadHelper.SafeRead(stream, headerBuffer, 2, payloadLength, checkLength: true);
                if ((readResult & ReadHelper.ReadResult.Fail) > 0)
                {
                    Log.Info($"ReceiveLoop read failed: {readResult}");
                    CheckForInterupt();
                    // will go to finally block below
                    return false;
                }

                // copy header as it might contain mask
                Buffer.BlockCopy(headerBuffer, 0, buffer, 0, 2 + payloadLength);
            }
            else
            {
                // copy header as it might contain mask
                Buffer.BlockCopy(headerBuffer, 0, buffer, 0, Constants.HeaderSize);

                ReadHelper.SafeRead(stream, buffer, Constants.HeaderSize, header.readLength);
            }

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
