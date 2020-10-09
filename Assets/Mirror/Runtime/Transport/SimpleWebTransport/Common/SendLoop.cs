using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    internal class SendLoop
    {
        public static void Loop(Connection conn, int bufferSize, bool setMask, Action<Connection> closeCallback)
        {
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
                    conn.sendPending.WaitOne();
                    conn.sendPending.Reset();

                    while (conn.sendQueue.TryDequeue(out ArraySegment<byte> msg))
                    {
                        // check if connected before sending message
                        if (!client.Connected) { Log.Info($"SendLoop {conn} not connected"); return; }

                        SendMessage(stream, writeBuffer, msg, setMask, maskHelper);
                    }
                }
            }
            catch (ThreadInterruptedException) { Log.Info($"SendLoop {conn} ThreadInterrupted"); return; }
            catch (ThreadAbortException) { Log.Info($"SendLoop {conn} ThreadAbort"); return; }
            catch (Exception e)
            {
                Debug.LogException(e);

                closeCallback.Invoke(conn);
            }
        }

        static void SendMessage(Stream stream, byte[] buffer, ArraySegment<byte> msg, bool setMask, MaskHelper maskHelper)
        {
            int msgLength = msg.Count;
            int sendLength = WriteHeader(buffer, msgLength, setMask);

            if (setMask)
            {
                sendLength = maskHelper.WriteMask(buffer, sendLength);
            }

            //todo check if Buffer.BlockCopy is faster
            Array.Copy(msg.Array, msg.Offset, buffer, sendLength, msgLength);
            sendLength += msgLength;

            // dump before mask on
            Log.DumpBuffer("Send", buffer, 0, sendLength);

            if (setMask)
            {
                //todo make toggleMask write to buffer to skip Array.Copy
                int messageOffset = sendLength - msgLength;
                MessageProcessor.ToggleMask(buffer, messageOffset, msgLength, buffer, messageOffset - 4);
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

        class MaskHelper
        {
            byte[] maskBuffer;
            RNGCryptoServiceProvider random;

            public MaskHelper()
            {
                maskBuffer = new byte[4];
                random = new RNGCryptoServiceProvider();
            }
            ~MaskHelper()
            {
                random?.Dispose();
            }

            internal int WriteMask(byte[] buffer, int offset)
            {
                random.GetBytes(maskBuffer);
                Buffer.BlockCopy(maskBuffer, 0, buffer, offset, 4);

                return offset + 4;
            }
        }
    }
}
