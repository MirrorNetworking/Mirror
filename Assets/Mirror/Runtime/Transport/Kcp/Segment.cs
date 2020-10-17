using System.Collections.Generic;

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
        internal uint rto;
        internal uint transmit;
        internal uint resendTimeStamp;
        internal uint fastack;
        internal bool acked;
        internal ByteBuffer data;

        static readonly Stack<Segment> msSegmentPool = new Stack<Segment>(32);

        public static Segment Get(int size)
        {
            if (msSegmentPool.Count > 0)
            {
                Segment seg = msSegmentPool.Pop();
                seg.data = ByteBuffer.Allocate(size);
                return seg;
            }
            return new Segment(size);
        }

        public static void Put(Segment seg)
        {
            seg.Reset();
            msSegmentPool.Push(seg);
        }

        Segment(int size)
        {
            data = ByteBuffer.Allocate(size);
        }

        // encode a segment into buffer
        internal int Encode(byte[] ptr, int offset)
        {
            int offset_ = offset;
            offset += Utils.Encode32U(ptr, offset, conversation);
            offset += Utils.Encode8u(ptr, offset, (byte)cmd);
            offset += Utils.Encode8u(ptr, offset, (byte)fragment);
            offset += Utils.Encode16U(ptr, offset, (ushort)window);
            offset += Utils.Encode32U(ptr, offset, timeStamp);
            offset += Utils.Encode32U(ptr, offset, serialNumber);
            offset += Utils.Encode32U(ptr, offset, unacknowledged);
            offset += Utils.Encode32U(ptr, offset, (uint)data.ReadableBytes);

            return offset - offset_;
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

            data.Clear();
            data.Dispose();
            data = null;
        }
    }
}
