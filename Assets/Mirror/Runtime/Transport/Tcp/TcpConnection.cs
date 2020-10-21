using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;

namespace Mirror.Tcp
{
    internal class TcpConnection : IConnection
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;

        public TcpConnection(TcpClient client)
        {
            this.client = client;
            stream = client.GetStream();
        }

        #region Receiving
        public async UniTask<(bool next, int channel)> ReceiveAsync(MemoryStream buffer)
        {
            buffer.SetLength(0);
            long position = buffer.Position;
            try
            { 
                // read message size
                if (!await ReadExactlyAsync(stream, buffer, 4))
                    return (false, 0);

                // rewind so that we read it
                buffer.Position = position;

                int length = ReadInt(buffer);

                // now read the message
                buffer.Position = position;

                bool next = await ReadExactlyAsync(stream, buffer, length);
                return (next, 0);
            }
            catch (ObjectDisposedException)
            {
                return (false, 0);
            }
        }

        private static async UniTask<bool> ReadExactlyAsync(NetworkStream stream, MemoryStream buffer, int count)
        {
            int offset = 0;

            buffer.SetLength(buffer.Position + count);

            // keep reading until we fill up the buffer
            while (offset < count)
            {
                int received;
                byte[] bytes = buffer.GetBuffer();

                if (stream.DataAvailable)
                {
                    // read available data immediatelly
                    // this is an important optimization because unity seems
                    // to wait until the next frame every time we call ReadAsync
                    // so if we have a bunch of data waiting in the buffer it takes a long
                    // time to receive it.
                    received = stream.Read(bytes, (int)buffer.Position + offset, count - offset);
                }
                else
                {
                    // wait for more data
                    received = await stream.ReadAsync(bytes, (int)buffer.Position + offset, count - offset);
                }

                // we just got disconnected
                if (received == 0)
                {
                    return false;
                }

                offset += received;
            }

            buffer.Position += count;
            return true;
        }

        private static int ReadInt(Stream buffer)
        {
            return
                (buffer.ReadByte() << 24) +
                (buffer.ReadByte() << 16) +
                (buffer.ReadByte() << 8) +
                buffer.ReadByte();
        }

        #endregion

        #region Sending
        public async UniTask SendAsync(ArraySegment<byte> data, int channel = Channel.Reliable)
        {
            try
            {
                var prefixed = new MemoryStream(data.Count + 4);
                WritePrefixedData(prefixed, data);

                prefixed.TryGetBuffer(out ArraySegment<byte> prefixedData);
                await stream.WriteAsync(prefixedData.Array, prefixedData.Offset, prefixedData.Count);
            }
            catch (ObjectDisposedException)
            {
                // this is fine, connection was closed
            }
        }

        private static void WriteInt(Stream stream, int length)
        {
            stream.WriteByte((byte)(length >> 24));
            stream.WriteByte((byte)(length >> 16));
            stream.WriteByte((byte)(length >> 8));
            stream.WriteByte((byte)length);
        }

        private static void WritePrefixedData(Stream stream, ArraySegment<byte> data)
        {
            WriteInt(stream, data.Count);
            stream.Write(data.Array, data.Offset, data.Count);
        }
        #endregion

        #region Disconnect
        public void Disconnect()
        {
            stream.Close();
            client.Close();
        }

        #endregion

        public EndPoint GetEndPointAddress()
        {
            return client.Client.RemoteEndPoint;
        }
    }
}
