using System;
using System.IO;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mirror
{

    /// <summary>
    /// A connection that is directly connected to another connection
    /// If you send data in one of them,  you receive it on the other one
    /// </summary>
    public class PipeConnection : IConnection
    {

        private PipeConnection connected;

        // should only be created by CreatePipe
        private PipeConnection()
        {

        }

        // buffer where we can queue up data
        readonly NetworkWriter writer = new NetworkWriter();
        readonly NetworkReader reader = new NetworkReader(new byte[] { });

        // counts how many messages we have pending
        private readonly SemaphoreSlim MessageCount = new SemaphoreSlim(0);

        public static (IConnection, IConnection) CreatePipe()
        {
            var c1 = new PipeConnection();
            var c2 = new PipeConnection();

            c1.connected = c2;
            c2.connected = c1;

            return (c1, c2);
        }

        public void Disconnect()
        {
            // disconnect both ends of the pipe
            connected.writer.WriteBytesAndSizeSegment(new ArraySegment<byte>(Array.Empty<byte>()));
            connected.MessageCount.Release();

            writer.WriteBytesAndSizeSegment(new ArraySegment<byte>(Array.Empty<byte>()));
            MessageCount.Release();
        }

        // technically not an IPEndpoint,  will fix later
        public EndPoint GetEndPointAddress() => new IPEndPoint(IPAddress.Loopback, 0);
        
        public async UniTask<int> ReceiveAsync(MemoryStream buffer)
        {
            // wait for a message
            await MessageCount.WaitAsync();

            buffer.SetLength(0);
            reader.buffer = writer.ToArraySegment();

            ArraySegment<byte> data = reader.ReadBytesAndSizeSegment();

            if (data.Count == 0)
                throw new EndOfStreamException();

            buffer.SetLength(0);
            buffer.Write(data.Array, data.Offset, data.Count);

            if (reader.Position == reader.Length)
            {
                // if we reached the end of the buffer, reset the buffer to recycle memory
                writer.SetLength(0);
                reader.Position = 0;
            }

            return 0;
        }

        public UniTask SendAsync(ArraySegment<byte> data, int channel = Channel.Reliable)
        {
            // add some data to the writer in the connected connection
            // and increase the message count
            connected.writer.WriteBytesAndSizeSegment(data);
            connected.MessageCount.Release();
            return UniTask.CompletedTask;
        }
    }
}
