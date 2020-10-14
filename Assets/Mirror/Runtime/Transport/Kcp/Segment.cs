using System.Collections.Generic;

namespace Mirror.KCP
{

    // KCP Segment Definition
    internal class Segment
    {
        internal uint conv;
        internal uint cmd;
        internal uint frg;
        internal uint wnd;
        internal uint ts;
        internal uint sn;
        internal uint una;
        internal uint rto;
        internal uint xmit;
        internal uint resendts;
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
            offset += Utils.Encode32U(ptr, offset, conv);
            offset += Utils.Encode8u(ptr, offset, (byte)cmd);
            offset += Utils.Encode8u(ptr, offset, (byte)frg);
            offset += Utils.Encode16U(ptr, offset, (ushort)wnd);
            offset += Utils.Encode32U(ptr, offset, ts);
            offset += Utils.Encode32U(ptr, offset, sn);
            offset += Utils.Encode32U(ptr, offset, una);
            offset += Utils.Encode32U(ptr, offset, (uint)data.ReadableBytes);

            return offset - offset_;
        }

        internal void Reset()
        {
            conv = 0;
            cmd = 0;
            frg = 0;
            wnd = 0;
            ts = 0;
            sn = 0;
            una = 0;
            rto = 0;
            xmit = 0;
            resendts = 0;
            fastack = 0;
            acked = false;

            data.Clear();
            data.Dispose();
            data = null;
        }
    }
}
