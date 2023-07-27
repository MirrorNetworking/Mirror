using System.IO;

namespace kcp2k
{
    // KCP Segment Definition
    internal class Segment
    {
        internal uint conv;     // conversation
        internal uint cmd;      // command, e.g. Kcp.CMD_ACK etc.
        // fragment (sent as 1 byte).
        // 0 if unfragmented, otherwise fragment numbers in reverse: N,..,32,1,0
        // this way the first received segment tells us how many fragments there are.
        internal uint frg;
        internal uint wnd;      // window size that the receive can currently receive
        internal uint ts;       // timestamp
        internal uint sn;       // sequence number
        internal uint una;
        internal uint resendts; // resend timestamp
        internal int  rto;
        internal uint fastack;
        internal uint xmit;     // retransmit count

        // we need an auto scaling byte[] with a WriteBytes function.
        // MemoryStream does that perfectly, no need to reinvent the wheel.
        // note: no need to pool it, because Segment is already pooled.
        // -> default MTU as initial capacity to avoid most runtime resizing/allocations
        //
        // .data is only used for Encode(), which always fits it into a buffer.
        // the buffer is always Kcp.buffer. Kcp ctor creates the buffer of size:
        // (mtu + OVERHEAD) * 3 bytes.
        // in other words, Encode only ever writes up to the above amount of bytes.
        internal MemoryStream data = new MemoryStream(Kcp.MTU_DEF);

        // ikcp_encode_seg
        // encode a segment into buffer.
        // buffer is always Kcp.buffer. Kcp ctor creates the buffer of size:
        // (mtu + OVERHEAD) * 3 bytes.
        // in other words, Encode only ever writes up to the above amount of bytes.
        internal int Encode(byte[] ptr, int offset)
        {
            int previousPosition = offset;

            offset += Utils.Encode32U(ptr, offset, conv);
            offset += Utils.Encode8u(ptr, offset, (byte)cmd);
            // IMPORTANT kcp encodes 'frg' as 1 byte.
            // so we can only support up to 255 fragments.
            // (which limits max message size to around 288 KB)
            offset += Utils.Encode8u(ptr, offset, (byte)frg);
            offset += Utils.Encode16U(ptr, offset, (ushort)wnd);
            offset += Utils.Encode32U(ptr, offset, ts);
            offset += Utils.Encode32U(ptr, offset, sn);
            offset += Utils.Encode32U(ptr, offset, una);
            offset += Utils.Encode32U(ptr, offset, (uint)data.Position);

            int written = offset - previousPosition;
            return written;
        }

        // reset to return a fresh segment to the pool
        internal void Reset()
        {
            conv = 0;
            cmd = 0;
            frg = 0;
            wnd = 0;
            ts  = 0;
            sn  = 0;
            una = 0;
            rto = 0;
            xmit = 0;
            resendts = 0;
            fastack  = 0;

            // keep buffer for next pool usage, but reset length (= bytes written)
            data.SetLength(0);
        }
    }
}
