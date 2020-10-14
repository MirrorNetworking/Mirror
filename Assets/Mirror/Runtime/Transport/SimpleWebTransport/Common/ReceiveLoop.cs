using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Mirror.SimpleWeb
{
    internal static class ReceiveLoop
    {
        public struct Config
        {
            public readonly Connection conn;
            public readonly int maxMessageSize;
            public readonly bool expectMask;
            public readonly ConcurrentQueue<Message> queue;
            public readonly Action<Connection> closeCallback;
            public readonly BufferPool bufferPool;

            public Config(Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, Action<Connection> closeCallback, BufferPool bufferPool)
            {
                this.conn = conn ?? throw new ArgumentNullException(nameof(conn));
                this.maxMessageSize = maxMessageSize;
                this.expectMask = expectMask;
                this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
                this.closeCallback = closeCallback ?? throw new ArgumentNullException(nameof(closeCallback));
                this.bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
            }

            public void Deconstruct(out Connection conn, out int maxMessageSize, out bool expectMask, out ConcurrentQueue<Message> queue, out Action<Connection> closeCallback)
            {
                conn = this.conn;
                maxMessageSize = this.maxMessageSize;
                expectMask = this.expectMask;
                queue = this.queue;
                closeCallback = this.closeCallback;
            }

            public void Deconstruct(out Connection conn, out ConcurrentQueue<Message> queue, out Action<Connection> closeCallback, out BufferPool bufferPool)
            {
                conn = this.conn;
                queue = this.queue;
                closeCallback = this.closeCallback;
                bufferPool = this.bufferPool;
            }
        }

        public static void Loop(Config config)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, Action<Connection> closeCallback) = config;

            byte[] readBuffer = new byte[Constants.HeaderSize + (expectMask ? Constants.MaskSize : 0) + maxMessageSize];
            try
            {
                try
                {
                    TcpClient client = conn.client;

                    while (client.Connected)
                    {
                        bool success = ReadOneMessage(config, readBuffer);
                        if (!success)
                            break;
                    }
                }
                catch (Exception)
                {
                    // if interupted we dont care about other execptions
                    Utils.CheckForInterupt();
                    throw;
                }
            }
            catch (ThreadInterruptedException) { Log.Info($"ReceiveLoop {conn} ThreadInterrupted"); }
            catch (ThreadAbortException) { Log.Info($"ReceiveLoop {conn} ThreadAbort"); }
            catch (ObjectDisposedException) { Log.Info($"ReceiveLoop {conn} Stream closed"); }
            catch (ReadHelperException e)
            {
                Log.Info($"ReceiveLoop {conn.connId} read failed: {e.Message}");
            }
            catch (SocketException e)
            {
                // this could happen if wss client closes stream
                Log.Warn($"ReceiveLoop SocketException\n{e.Message}", false);
            }
            catch (IOException e)
            {
                // this could happen if client disconnects
                Log.Warn($"ReceiveLoop IOException\n{e.Message}", false);
            }
            catch (InvalidDataException e)
            {
                Log.Error($"Invalid data from {conn}: {e.Message}");
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (Exception e)
            {
                Log.Exception(e);
                queue.Enqueue(new Message(conn.connId, e));
            }
            finally
            {
                closeCallback.Invoke(conn);
            }
        }

        static bool ReadOneMessage(Config config, byte[] buffer)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> _, Action<Connection> _) = config;
            Stream stream = conn.stream;

            int offset = 0;
            // read 2
            offset = ReadHelper.Read(stream, buffer, offset, Constants.HeaderMinSize);
            // log after first blocking call
            Log.Verbose($"Message From {conn}");

            if (MessageProcessor.NeedToReadShortLength(buffer))
            {
                offset = ReadHelper.Read(stream, buffer, offset, Constants.ShortLength);
            }


            MessageProcessor.ValidateHeader(buffer, maxMessageSize, expectMask);

            if (expectMask)
            {
                offset = ReadHelper.Read(stream, buffer, offset, Constants.MaskSize);
            }

            int opcode = MessageProcessor.GetOpcode(buffer);
            int payloadLength = MessageProcessor.GetPayloadLength(buffer);

            Log.Verbose($"Header ln:{payloadLength} op:{opcode} mask:{expectMask}");

            offset = ReadHelper.Read(stream, buffer, offset, payloadLength);

            int msgOffset = offset - payloadLength;
            if (expectMask)
            {
                int maskOffset = offset - payloadLength - Constants.MaskSize;
                MessageProcessor.ToggleMask(buffer, msgOffset, payloadLength, buffer, maskOffset);
            }

            // dump after mask off
            Log.DumpBuffer($"Raw Header", buffer, 0, msgOffset);
            Log.DumpBuffer($"Message", buffer, msgOffset, payloadLength);

            HandleMessage(config, opcode, buffer, msgOffset, payloadLength);
            return true;
        }

        static void HandleMessage(Config config, int opcode, byte[] msg, int offset, int length)
        {
            (Connection conn, ConcurrentQueue<Message> queue, Action<Connection> closeCallback, BufferPool bufferPool) = config;

            if (opcode == 2)
            {
                ArrayBuffer buffer = bufferPool.Take(length);

                buffer.CopyFrom(msg, offset, length);

                queue.Enqueue(new Message(conn.connId, buffer));
            }
            else if (opcode == 8)
            {
                Log.Info($"Close: {msg[offset + 0] << 8 | msg[offset + 1]} message:{Encoding.UTF8.GetString(msg, offset + 2, length - 2)}");
                closeCallback.Invoke(conn);
            }
        }
    }
}
