using System.Collections.Generic;
using System.IO;

namespace Mirror.KCP
{
    public enum CommandType : byte { Push = 81, Ack = 82, WindowAsk = 83, WindowTell = 84 };

    // KCP Segment Definition
    internal class Segment
    {
        internal uint conversation;
        internal CommandType cmd;
        internal uint fragment;
        internal uint window;
        internal uint timeStamp;
        internal uint serialNumber;
        internal uint unacknowledged;
        internal int rto;
        internal uint transmit;
        internal uint resendTimeStamp;
        internal uint fastack;
        internal bool acked;
        internal MemoryStream data;

        static readonly Stack<Segment> msSegmentPool = new Stack<Segment>(32);

        public static Segment Lease()
        {
            if (msSegmentPool.Count > 0)
                return msSegmentPool.Pop();
            return new Segment();
        }

        public static void Release(Segment seg)
        {
            seg.Reset();
            msSegmentPool.Push(seg);
        }

        Segment()
        {
            data = new MemoryStream();
        }

        // encode a segment into buffer
        internal int Encode(byte[] ptr, int offset)
        {
            var encoder = new Encoder(ptr, offset);
            encoder.Encode32U(conversation);
            encoder.Encode8U((byte)cmd);
            encoder.Encode8U((byte)fragment);
            encoder.Encode16U((ushort)window);
            encoder.Encode32U(timeStamp);
            encoder.Encode32U(serialNumber);
            encoder.Encode32U(unacknowledged);
            encoder.Encode32U((uint)data.Length);

            return encoder.Position;
        }

        internal void Reset()
        {
            conversation = 0;
            cmd = 0;
            fragment = 0;
            window = 0;
            timeStamp = 0;
            serialNumber = 0;
            unacknowledged = 0;
            rto = 0;
            transmit = 0;
            resendTimeStamp = 0;
            fastack = 0;
            acked = false;

            // keep buffer for next pool usage, but reset position
            data.Position = 0;
            data.SetLength(0);
        }
    }
}
