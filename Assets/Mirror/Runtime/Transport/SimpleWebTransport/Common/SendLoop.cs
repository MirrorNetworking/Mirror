using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine.Profiling;

namespace Mirror.SimpleWeb
{
    public static class SendLoopConfig
    {
        public static volatile bool batchSend = false;
        public static volatile bool sleepBeforeSend = false;
    }
    internal static class SendLoop
    {
        public struct Config
        {
            public readonly Connection conn;
            public readonly int bufferSize;
            public readonly bool setMask;

            public Config(Connection conn, int bufferSize, bool setMask)
            {
                this.conn = conn ?? throw new ArgumentNullException(nameof(conn));
                this.bufferSize = bufferSize;
                this.setMask = setMask;
            }

            public void Deconstruct(out Connection conn, out int bufferSize, out bool setMask)
            {
                conn = this.conn;
                bufferSize = this.bufferSize;
                setMask = this.setMask;
            }
        }

        public static void Loop(Config config)
        {
            (Connection conn, int bufferSize, bool setMask) = config;

            Profiler.BeginThreadProfiling("SimpleWeb", $"SendLoop {conn.connId}");

            // create write buffer for this thread
            byte[] writeBuffer = new byte[bufferSize];
            MaskHelper maskHelper = setMask ? new MaskHelper() : null;
            try
            {
                TcpClient client = conn.client;
                Stream stream = conn.stream;

                // null check in case disconnect while send thread is starting
                if (client == null)
                    return;

                while (client.Connected)
                {
                    // wait for message
                    conn.sendPending.Wait();
                    // wait for 1ms for mirror to send other messages
                    if (SendLoopConfig.sleepBeforeSend)
                    {
                        Thread.Sleep(1);
                    }
                    conn.sendPending.Reset();

                    if (SendLoopConfig.batchSend)
                    {
                        int offset = 0;
                        while (conn.sendQueue.TryDequeue(out ArrayBuffer msg))
                        {
                            // check if connected before sending message
                            if (!client.Connected) { Log.Info($"SendLoop {conn} not connected"); return; }

                            int maxLength = msg.count + Constants.HeaderSize + Constants.MaskSize;

                            // if next writer could overflow, write to stream and clear buffer
                            if (offset + maxLength > bufferSize)
                            {
                                stream.Write(writeBuffer, 0, offset);
                                offset = 0;
                            }

                            offset = SendMessage(writeBuffer, offset, msg, setMask, maskHelper);
                            msg.Release();
                        }

                        // after no message in queue, send remaining messages
                        // don't need to check offset > 0 because last message in queue will always be sent here

                        stream.Write(writeBuffer, 0, offset);
                    }
                    else
                    {
                        while (conn.sendQueue.TryDequeue(out ArrayBuffer msg))
                        {
                            // check if connected before sending message
                            if (!client.Connected) { Log.Info($"SendLoop {conn} not connected"); return; }

                            int length = SendMessage(writeBuffer, 0, msg, setMask, maskHelper);
                            stream.Write(writeBuffer, 0, length);
                            msg.Release();
                        }
                    }
                }

                Log.Info($"{conn} Not Connected");
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException e) { Log.InfoException(e); }
            catch (Exception e)
            {
                Log.Exception(e);
            }
            finally
            {
                Profiler.EndThreadProfiling();
                conn.Dispose();
                maskHelper?.Dispose();
            }
        }

        /// <returns>new offset in buffer</returns>
        static int SendMessage(byte[] buffer, int startOffset, ArrayBuffer msg, bool setMask, MaskHelper maskHelper)
        {
            int msgLength = msg.count;
            int offset = WriteHeader(buffer, startOffset, msgLength, setMask);

            if (setMask)
            {
                offset = maskHelper.WriteMask(buffer, offset);
            }

            msg.CopyTo(buffer, offset);
            offset += msgLength;

            // dump before mask on
            Log.DumpBuffer("Send", buffer, startOffset, offset);

            if (setMask)
            {
                int messageOffset = offset - msgLength;
                MessageProcessor.ToggleMask(buffer, messageOffset, msgLength, buffer, messageOffset - Constants.MaskSize);
            }

            return offset;
        }

        static int WriteHeader(byte[] buffer, int startOffset, int msgLength, bool setMask)
        {
            int sendLength = 0;
            const byte finished = 128;
            const byte byteOpCode = 2;

            buffer[startOffset + 0] = finished | byteOpCode;
            sendLength++;

            if (msgLength <= Constants.BytePayloadLength)
            {
                buffer[startOffset + 1] = (byte)msgLength;
                sendLength++;
            }
            else if (msgLength <= ushort.MaxValue)
            {
                buffer[startOffset + 1] = 126;
                buffer[startOffset + 2] = (byte)(msgLength >> 8);
                buffer[startOffset + 3] = (byte)msgLength;
                sendLength += 3;
            }
            else
            {
                throw new InvalidDataException($"Trying to send a message larger than {ushort.MaxValue} bytes");
            }

            if (setMask)
            {
                buffer[startOffset + 1] |= 0b1000_0000;
            }

            return sendLength + startOffset;
        }

        sealed class MaskHelper : IDisposable
        {
            readonly byte[] maskBuffer;
            readonly RNGCryptoServiceProvider random;

            public MaskHelper()
            {
                maskBuffer = new byte[4];
                random = new RNGCryptoServiceProvider();
            }
            public void Dispose()
            {
                random.Dispose();
            }

            public int WriteMask(byte[] buffer, int offset)
            {
                random.GetBytes(maskBuffer);
                Buffer.BlockCopy(maskBuffer, 0, buffer, offset, 4);

                return offset + 4;
            }
        }
    }
}
