using System;
using System.Collections.Generic;

namespace Mirror.KCP
{
    /// <summary>
    /// Manages unreliable channel
    /// </summary>
    public class Unreliable
    {
        private readonly Queue<Segment> messages = new Queue<Segment>();
        private readonly Action<byte[], int> output;
        public const int OVERHEAD = 4; //related to MTU

        public int Reserved { get; set; }

        // Start is called before the first frame update
        public Unreliable(Action<byte[], int> output_)
        {
            output = output_;
        }

        /// <summary>Input
        /// <para>Used when you receive a low level packet (eg. UDP packet)</para>
        /// <returns>Returns int (-3, -1, or 0)</returns></summary>
        /// <param name="data"></param>
        /// <param name="index"></param>
        /// <param name="size"></param>
        public int Input(byte[] data, int size)
        {
            if (size <= OVERHEAD)
                return -3;

            var seg = Segment.Lease();
            seg.data.Write(data, Reserved + OVERHEAD, size - OVERHEAD - Reserved);

            messages.Enqueue(seg);

            return 0;
        }

        public int PeekSize()
        {
            if (messages.Count == 0)
                return -1;

            Segment seg = messages.Peek();
            return (int)seg.data.Length;
        }

        /// <summary>Receive
        /// <para>Receive data from kcp state machine</para>
        /// <return>Return number of bytes read.
        /// Return -1 when there is no readable data.
        /// Return -2 if len(buffer) is smaller than kcp.PeekSize().</return></summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public int Receive(byte[] buffer, int length)
        {
            if (messages.Count <= 0)
                return -1;

            Segment segment = messages.Dequeue();

            if (length < segment.data.Position)
                return -2;
            segment.data.Position = 0;
            segment.data.Read(buffer, 0, (int)segment.data.Length);
            int bytes = (int)segment.data.Length;
            Segment.Release(segment);
            return bytes;
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            var segment = Segment.Lease();

            System.IO.MemoryStream sendBuffer = segment.data;

            sendBuffer.SetLength(length + Reserved + OVERHEAD);

            var encoder = new Encoder(sendBuffer.GetBuffer(), Reserved);
            encoder.Encode32U(Channel.Unreliable);

            sendBuffer.Position = encoder.Position;

            sendBuffer.Write(buffer, offset, length);
            output(sendBuffer.GetBuffer(), length + Reserved + OVERHEAD);

            Segment.Release(segment);
        }
    }
}