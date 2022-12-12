using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.Profiling;

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
            public readonly BufferPool bufferPool;

            public Config(Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool)
            {
                this.conn = conn ?? throw new ArgumentNullException(nameof(conn));
                this.maxMessageSize = maxMessageSize;
                this.expectMask = expectMask;
                this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
                this.bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
            }

            public void Deconstruct(out Connection conn, out int maxMessageSize, out bool expectMask, out ConcurrentQueue<Message> queue, out BufferPool bufferPool)
            {
                conn = this.conn;
                maxMessageSize = this.maxMessageSize;
                expectMask = this.expectMask;
                queue = this.queue;
                bufferPool = this.bufferPool;
            }
        }

        struct Header
        {
            public int payloadLength;
            public int offset;
            public int opcode;
            public bool finished;
        }

        public static void Loop(Config config)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool _) = config;

            Profiler.BeginThreadProfiling("SimpleWeb", $"ReceiveLoop {conn.connId}");

            byte[] readBuffer = new byte[Constants.HeaderSize + (expectMask ? Constants.MaskSize : 0) + maxMessageSize];
            try
            {
                try
                {
                    TcpClient client = conn.client;

                    while (client.Connected)
                    {
                        ReadOneMessage(config, readBuffer);
                    }

                    Log.Info($"[SimpleWebTransport] {conn} Not Connected");
                }
                catch (Exception)
                {
                    // if interrupted we don't care about other exceptions
                    Utils.CheckForInterupt();
                    throw;
                }
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException e) { Log.InfoException(e); }
            catch (ObjectDisposedException e) { Log.InfoException(e); }
            catch (ReadHelperException e)
            {
                Log.InfoException(e);
            }
            catch (SocketException e)
            {
                // this could happen if wss client closes stream
                Log.Warn($"[SimpleWebTransport] ReceiveLoop SocketException\n{e.Message}", false);
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (IOException e)
            {
                // this could happen if client disconnects
                Log.Warn($"[SimpleWebTransport] ReceiveLoop IOException\n{e.Message}", false);
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (InvalidDataException e)
            {
                Log.Error($"[SimpleWebTransport] Invalid data from {conn}: {e.Message}");
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (Exception e)
            {
                Log.Exception(e);
                queue.Enqueue(new Message(conn.connId, e));
            }
            finally
            {
                Profiler.EndThreadProfiling();

                conn.Dispose();
            }
        }

        static void ReadOneMessage(Config config, byte[] buffer)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;
            Stream stream = conn.stream;

            Header header = ReadHeader(config, buffer);

            int msgOffset = header.offset;
            header.offset = ReadHelper.Read(stream, buffer, header.offset, header.payloadLength);

            if (header.finished)
            {
                switch (header.opcode)
                {
                    case 2:
                        HandleArrayMessage(config, buffer, msgOffset, header.payloadLength);
                        break;
                    case 8:
                        HandleCloseMessage(config, buffer, msgOffset, header.payloadLength);
                        break;
                }
            }
            else
            {
                // todo cache this to avoid allocations
                Queue<ArrayBuffer> fragments = new Queue<ArrayBuffer>();
                fragments.Enqueue(CopyMessageToBuffer(bufferPool, expectMask, buffer, msgOffset, header.payloadLength));
                int totalSize = header.payloadLength;

                while (!header.finished)
                {
                    header = ReadHeader(config, buffer, opCodeContinuation: true);

                    msgOffset = header.offset;
                    header.offset = ReadHelper.Read(stream, buffer, header.offset, header.payloadLength);
                    fragments.Enqueue(CopyMessageToBuffer(bufferPool, expectMask, buffer, msgOffset, header.payloadLength));

                    totalSize += header.payloadLength;
                    MessageProcessor.ThrowIfMsgLengthTooLong(totalSize, maxMessageSize);
                }


                ArrayBuffer msg = bufferPool.Take(totalSize);
                msg.count = 0;
                while (fragments.Count > 0)
                {
                    ArrayBuffer part = fragments.Dequeue();

                    part.CopyTo(msg.array, msg.count);
                    msg.count += part.count;

                    part.Release();
                }

                // dump after mask off
                Log.DumpBuffer($"[SimpleWebTransport] Message", msg);

                queue.Enqueue(new Message(conn.connId, msg));
            }
        }

        static Header ReadHeader(Config config, byte[] buffer, bool opCodeContinuation = false)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;
            Stream stream = conn.stream;
            Header header = new Header();

            // read 2
            header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.HeaderMinSize);
            // log after first blocking call
            Log.Verbose($"[SimpleWebTransport] Message From {conn}");

            if (MessageProcessor.NeedToReadShortLength(buffer))
            {
                header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.ShortLength);
            }
            if (MessageProcessor.NeedToReadLongLength(buffer))
            {
                header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.LongLength);
            }

            Log.DumpBuffer($"[SimpleWebTransport] Raw Header", buffer, 0, header.offset);

            MessageProcessor.ValidateHeader(buffer, maxMessageSize, expectMask, opCodeContinuation);

            if (expectMask)
            {
                header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.MaskSize);
            }

            header.opcode = MessageProcessor.GetOpcode(buffer);
            header.payloadLength = MessageProcessor.GetPayloadLength(buffer);
            header.finished = MessageProcessor.Finished(buffer);

            Log.Verbose($"[SimpleWebTransport] Header ln:{header.payloadLength} op:{header.opcode} mask:{expectMask}");

            return header;
        }

        static void HandleArrayMessage(Config config, byte[] buffer, int msgOffset, int payloadLength)
        {
            (Connection conn, int _, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;

            ArrayBuffer arrayBuffer = CopyMessageToBuffer(bufferPool, expectMask, buffer, msgOffset, payloadLength);

            // dump after mask off
            Log.DumpBuffer($"[SimpleWebTransport] Message", arrayBuffer);

            queue.Enqueue(new Message(conn.connId, arrayBuffer));
        }

        static ArrayBuffer CopyMessageToBuffer(BufferPool bufferPool, bool expectMask, byte[] buffer, int msgOffset, int payloadLength)
        {
            ArrayBuffer arrayBuffer = bufferPool.Take(payloadLength);

            if (expectMask)
            {
                int maskOffset = msgOffset - Constants.MaskSize;
                // write the result of toggle directly into arrayBuffer to avoid 2nd copy call
                MessageProcessor.ToggleMask(buffer, msgOffset, arrayBuffer, payloadLength, buffer, maskOffset);
            }
            else
            {
                arrayBuffer.CopyFrom(buffer, msgOffset, payloadLength);
            }

            return arrayBuffer;
        }

        static void HandleCloseMessage(Config config, byte[] buffer, int msgOffset, int payloadLength)
        {
            (Connection conn, int _, bool expectMask, ConcurrentQueue<Message> _, BufferPool _) = config;

            if (expectMask)
            {
                int maskOffset = msgOffset - Constants.MaskSize;
                MessageProcessor.ToggleMask(buffer, msgOffset, payloadLength, buffer, maskOffset);
            }

            // dump after mask off
            Log.DumpBuffer($"[SimpleWebTransport] Message", buffer, msgOffset, payloadLength);

            Log.Info($"[SimpleWebTransport] Close: {GetCloseCode(buffer, msgOffset)} message:{GetCloseMessage(buffer, msgOffset, payloadLength)}");

            conn.Dispose();
        }

        static string GetCloseMessage(byte[] buffer, int msgOffset, int payloadLength)
        {
            return Encoding.UTF8.GetString(buffer, msgOffset + 2, payloadLength - 2);
        }

        static int GetCloseCode(byte[] buffer, int msgOffset)
        {
            return buffer[msgOffset + 0] << 8 | buffer[msgOffset + 1];
        }
    }
}
