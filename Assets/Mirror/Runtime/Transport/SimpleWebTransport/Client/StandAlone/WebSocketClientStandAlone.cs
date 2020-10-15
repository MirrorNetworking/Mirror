using System;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public class WebSocketClientStandAlone : SimpleWebClient
    {
        readonly ClientSslHelper sslHelper;
        readonly ClientHandshake handshake;
        readonly TcpConfig tcpConfig;
        Connection conn;


        internal WebSocketClientStandAlone(int maxMessageSize, int maxMessagesPerTick, TcpConfig tcpConfig) : base(maxMessageSize, maxMessagesPerTick)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException();
#else
            sslHelper = new ClientSslHelper();
            handshake = new ClientHandshake();
            this.tcpConfig = tcpConfig;
#endif
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
                tcpConfig.ApplyTo(client);

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

                conn = new Connection(client, AfterConnectionDisposed);
                conn.receiveThread = Thread.CurrentThread;

                bool success = sslHelper.TryCreateStream(conn, uri);
                if (!success)
                {
                    Log.Warn("Failed to create Stream");
                    conn.Dispose();
                    return;
                }

                success = handshake.TryHandshake(conn, uri);
                if (!success)
                {
                    Log.Warn("Failed Handshake");
                    conn.Dispose();
                    return;
                }

                Log.Info("HandShake Successful");

                state = ClientState.Connected;

                receiveQueue.Enqueue(new Message(EventType.Connected));

                Thread sendThread = new Thread(() =>
                {
                    SendLoop.Config sendConfig = new SendLoop.Config(
                        conn,
                        bufferSize: Constants.HeaderSize + Constants.MaskSize + maxMessageSize,
                        setMask: true);

                    SendLoop.Loop(sendConfig);
                });

                conn.sendThread = sendThread;
                sendThread.IsBackground = true;
                sendThread.Start();

                ReceiveLoop.Config config = new ReceiveLoop.Config(conn,
                    maxMessageSize,
                    false,
                    receiveQueue,
                    bufferPool);
                ReceiveLoop.Loop(config);
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException e) { Log.InfoException(e); }
            catch (Exception e) { Log.Exception(e); }
            finally
            {
                // close here incase connect fails
                conn.Dispose();
            }
        }

        void AfterConnectionDisposed(Connection conn)
        {
            state = ClientState.NotConnected;
            // make sure Disconnected event is only called once
            receiveQueue.Enqueue(new Message(EventType.Disconnected));
        }

        public override void Disconnect()
        {
            state = ClientState.Disconnecting;
            Log.Info("Disconnect Called");
            conn.Dispose();
        }

        public override void Send(ArraySegment<byte> segment)
        {
            ArrayBuffer buffer = bufferPool.Take(segment.Count);
            buffer.CopyFrom(segment);

            conn.sendQueue.Enqueue(buffer);
            conn.sendPending.Set();
        }
    }
}
