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
            public readonly Action<Connection> closeCallback;

            public Config(Connection conn, int bufferSize, bool setMask, Action<Connection> closeCallback)
            {
                this.conn = conn ?? throw new ArgumentNullException(nameof(conn));
                this.bufferSize = bufferSize;
                this.setMask = setMask;
                this.closeCallback = closeCallback ?? throw new ArgumentNullException(nameof(closeCallback));
            }

            public void Deconstruct(out Connection conn, out int bufferSize, out bool setMask, out Action<Connection> closeCallback)
            {
                conn = this.conn;
                bufferSize = this.bufferSize;
                setMask = this.setMask;
                closeCallback = this.closeCallback;
            }

            internal void Deconstruct(out Connection conn, out bool setMask)
            {
                throw new NotImplementedException();
            }
        }


        public static void Loop(Config config)
        {
            (Connection conn, int bufferSize, bool setMask, Action<Connection> closeCallback) = config;

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
            }
            catch (ThreadInterruptedException) { Log.Info($"SendLoop {conn} ThreadInterrupted"); }
            catch (ThreadAbortException) { Log.Info($"SendLoop {conn} ThreadAbort"); }
            catch (Exception e)
            {
                Log.Exception(e);

                closeCallback.Invoke(conn);
            }
            finally
            {
                maskHelper?.Dispose();
            }
        }

        static void SendMessage(Stream stream, byte[] buffer, ArrayBuffer msg, bool setMask, MaskHelper maskHelper)
        {
            int msgLength = msg.Length;
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
                //todo make toggleMask write to buffer to skip Array.Copy
                int messageOffset = sendLength - msgLength;
                MessageProcessor.ToggleMask(buffer, messageOffset, msgLength, buffer, messageOffset - Constants.MaskSize);
            }

            stream.Write(buffer, 0, sendLength);
        }

        static int WriteHeader(byte[] buffer, int msgLength, bool setMask)
        {
            int sendLength = 0;
            byte finished = 128;
            byte byteOpCode = 2;

            buffer[0] = (byte)(finished | byteOpCode);
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
