using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Mirror.SimpleWeb
{
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

            // create write buffer for this thread
            byte[] writeBuffer = new byte[bufferSize];
            MaskHelper maskHelper = setMask ? new MaskHelper() : null;
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
                    conn.sendPending.Wait();
                    conn.sendPending.Reset();

                    while (conn.sendQueue.TryDequeue(out ArrayBuffer msg))
                    {
                        // check if connected before sending message
                        if (!client.Connected) { Log.Info($"SendLoop {conn} not connected"); return; }

                        SendMessage(stream, writeBuffer, msg, setMask, maskHelper);
                        msg.Release();
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
                conn.Dispose();
                maskHelper?.Dispose();
            }
        }

        static void SendMessage(Stream stream, byte[] buffer, ArrayBuffer msg, bool setMask, MaskHelper maskHelper)
        {
            int msgLength = msg.count;
            int sendLength = WriteHeader(buffer, msgLength, setMask);

            if (setMask)
            {
                sendLength = maskHelper.WriteMask(buffer, sendLength);
            }

            msg.CopyTo(buffer, sendLength);
            sendLength += msgLength;

            // dump before mask on
            Log.DumpBuffer("Send", buffer, 0, sendLength);

            if (setMask)
            {
                int messageOffset = sendLength - msgLength;
                MessageProcessor.ToggleMask(buffer, messageOffset, msgLength, buffer, messageOffset - Constants.MaskSize);
            }

            stream.Write(buffer, 0, sendLength);
        }

        static int WriteHeader(byte[] buffer, int msgLength, bool setMask)
        {
            int sendLength = 0;
            const byte finished = 128;
            const byte byteOpCode = 2;

            buffer[0] = finished | byteOpCode;
            sendLength++;

            if (msgLength <= Constants.BytePayloadLength)
            {
                buffer[1] = (byte)msgLength;
                sendLength++;
            }
            else if (msgLength <= ushort.MaxValue)
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

            if (setMask)
            {
                buffer[1] |= 0b1000_0000;
            }

            return sendLength;
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
