// common config struct, instead of passing 10 parameters manually every time.
using System;

namespace kcp2k
{
    // [Serializable] to show it in Unity inspector.
    // 'class' so we can set defaults easily.
    [Serializable]
    public class KcpConfig
    {
        // socket configuration ////////////////////////////////////////////////
        // DualMode uses both IPv6 and IPv4. not all platforms support it.
        // (Nintendo Switch, etc.)
        public bool DualMode;

        // attempt to maximize socket send/recv buffers to OS limit.
        // too small send/receive buffers might cause connection drops under
        // heavy load. using the OS max size can make a difference already.
        public bool MaximizeSocketBuffers;

        // kcp configuration ///////////////////////////////////////////////////
        // NoDelay is recommended to reduce latency. This also scales better
        // without buffers getting full.
        public bool NoDelay;

        // KCP internal update interval. 100ms is KCP default, but a lower
        // interval is recommended to minimize latency and to scale to more
        // networked entities.
        public uint Interval;

        // KCP fastresend parameter. Faster resend for the cost of higher
        // bandwidth.
        public int FastResend;

        // KCP congestion window heavily limits messages flushed per update.
        // congestion window may actually be broken in kcp:
        // - sending max sized message @ M1 mac flushes 2-3 messages per update
        // - even with super large send/recv window, it requires thousands of
        //   update calls
        // best to leave this disabled, as it may significantly increase latency.
        public bool CongestionWindow;

        // KCP window size can be modified to support higher loads.
        // for example, Mirror Benchmark requires:
        //   128, 128 for 4k monsters
        //   512, 512 for 10k monsters
        //  8192, 8192 for 20k monsters
        public uint SendWindowSize;
        public uint ReceiveWindowSize;

        // timeout in milliseconds
        public int Timeout;

        // maximum retransmission attempts until dead_link
        public uint MaxRetransmits;

        // constructor /////////////////////////////////////////////////////////
        // constructor with defaults for convenience.
        // makes it easy to define "new KcpConfig(DualMode=false)" etc.
        public KcpConfig(
            bool DualMode              = true,
            bool MaximizeSocketBuffers = false,
            bool NoDelay               = true,
            uint Interval              = 10,
            int FastResend             = 0,
            bool CongestionWindow      = false,
            uint SendWindowSize        = Kcp.WND_SND,
            uint ReceiveWindowSize     = Kcp.WND_RCV,
            int Timeout                = KcpPeer.DEFAULT_TIMEOUT,
            uint MaxRetransmits        = Kcp.DEADLINK)
        {
            this.DualMode = DualMode;
            this.MaximizeSocketBuffers = MaximizeSocketBuffers;
            this.NoDelay = NoDelay;
            this.Interval = Interval;
            this.FastResend = FastResend;
            this.CongestionWindow = CongestionWindow;
            this.SendWindowSize = SendWindowSize;
            this.ReceiveWindowSize = ReceiveWindowSize;
            this.Timeout = Timeout;
            this.MaxRetransmits = MaxRetransmits;
        }
    }
}
