using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mirror.KCP
{
    public class Kcp
    {
        public const int RTO_NDL = 30;  // no delay min rto
        public const int RTO_MIN = 100; // normal min rto
        public const int RTO_DEF = 200; //Default RTO
        public const int RTO_MAX = 60000; //Maximum RTO
        public const int CMD_PUSH = 81; // cmd: push data
        public const int CMD_ACK = 82; // cmd: ack
        public const int CMD_WASK = 83; // cmd: window probe (ask)
        public const int CMD_WINS = 84; // cmd: window size (tell)
        public const int ASK_SEND = 1;  // need to send CMD_WASK
        public const int ASK_TELL = 2;  // need to send CMD_WINS
        public const int WND_SND = 32; // defualt Send Window
        public const int WND_RCV = 32; //default Receive Window
        public const int MTU_DEF = 1200; //MTU Default.
        public const int ACK_FAST = 3;
        public const int INTERVAL = 100;
        public const int OVERHEAD = 24;
        public const int DEADLINK = 20;
        public const int THRESH_INIT = 2;
        public const int THRESH_MIN = 2;
        public const int PROBE_INIT = 7000;   // 7 secs to probe window size
        public const int PROBE_LIMIT = 120000; // up to 120 secs to probe window
        public const int SN_OFFSET = 12;

        readonly Stopwatch refTime = new Stopwatch();

        internal struct AckItem
        {
            internal uint serialNumber;
            internal uint timestamp;
        }

        // kcp members.
        readonly uint conv;
        uint mtu;
        uint snd_una;
        uint snd_nxt;
        uint rcv_nxt;
        uint ssthresh;
        uint rx_rttval;
        uint rx_srtt;
        uint rx_rto;
        uint rx_minrto;
        uint cwnd;
        uint probe;
        uint interval;
        uint ts_flush;
        bool noDelay;
        bool updated;
        uint ts_probe;
        uint probe_wait;
        uint incr;

        int fastresend;
        bool nocwnd;
        internal readonly List<Segment> sendQueue = new List<Segment>(16);
        internal readonly List<Segment> receiveQueue = new List<Segment>(16);
        internal readonly List<Segment> sendBuffer = new List<Segment>(16);
        internal readonly List<Segment> receiveBuffer = new List<Segment>(16);
        internal readonly List<AckItem> ackList = new List<AckItem>(16);

        byte[] buffer;
        uint reserved = 0;
        readonly Action<byte[], int> output; // buffer, size

        public uint SendWindowMax { get; private set; }
        public uint ReceiveWindowMax { get; private set; }
        public uint RmtWnd { get; private set; }
        public uint Mss => mtu - OVERHEAD - reserved;

        // get how many packet is waiting to be sent
        public int WaitSnd => sendBuffer.Count + sendQueue.Count;

        // internal time.
        public uint CurrentMS => (uint)refTime.ElapsedMilliseconds;

        /// <summary>create a new kcp control object</summary>
        /// <param name="conv_">must equal in two endpoint from the same connection.</param>
        /// <param name="output_"></param>
        public Kcp(uint conv_, Action<byte[], int> output_)
        {
            conv = conv_;
            SendWindowMax = WND_SND;
            ReceiveWindowMax = WND_RCV;
            RmtWnd = WND_RCV;
            mtu = MTU_DEF;
            rx_rto = RTO_DEF;
            rx_minrto = RTO_MIN;
            interval = INTERVAL;
            ts_flush = INTERVAL;
            ssthresh = THRESH_INIT;
            buffer = new byte[mtu];
            output = output_;
            refTime.Start();
        }

        /// <summary>PeekSize
        /// check the size of next message in the recv queue</summary>
        public int PeekSize()
        {
            if (receiveQueue.Count == 0)
                return -1;

            Segment seq = receiveQueue[0];

            if (seq.frg == 0)
                return seq.data.ReadableBytes;

            if (receiveQueue.Count < seq.frg + 1)
                return -1;

            int length = 0;

            foreach (Segment item in receiveQueue)
            {
                length += item.data.ReadableBytes;
                if (item.frg == 0)
                    break;
            }

            return length;
        }

        /// <summary>Receive
        /// Receive data from kcp state machine
        /// <para>Return number of bytes read.</para>
        /// <para>Return -1 when there is no readable data.</para>
        /// <para>Return -2 if len(buffer) is smaller than kcp.PeekSize().</para></summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="length"></param>
        public int Receive(byte[] buffer, int index, int length)
        {
            int peekSize = PeekSize();
            if (peekSize < 0)
                return -1;

            if (peekSize > length)
                return -2;

            bool fastRecover = receiveQueue.Count >= ReceiveWindowMax;

            // merge fragment.
            int count = 0;
            int n = index;

            foreach (Segment seg in receiveQueue)
            {
                // copy fragment data into buffer.
                Buffer.BlockCopy(seg.data.RawBuffer, seg.data.ReaderIndex, buffer, n, seg.data.ReadableBytes);
                n += seg.data.ReadableBytes;

                count++;
                uint fragment = seg.frg;
                Segment.Put(seg);
                if (fragment == 0)
                    break;
            }

            receiveQueue.RemoveRange(0, count);

            // move available data from rcv_buf -> rcv_queue
            count = 0;
            foreach (Segment seg in receiveBuffer)
            {
                if (seg.sn == rcv_nxt && receiveQueue.Count + count < ReceiveWindowMax)
                {
                    receiveQueue.Add(seg);
                    rcv_nxt++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            receiveBuffer.RemoveRange(0, count);

            // fast recover
            if (receiveQueue.Count < ReceiveWindowMax && fastRecover)
            {
                // ready to send back CMD_WINS in flush
                // tell remote my window size
                probe |= ASK_TELL;
            }

            return n - index;
        }

        /// <summary>Send
        /// <para>user/upper level send</para></summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="length"></param>
        public void Send(byte[] buffer, int index, int length)
        {
            if (length == 0)
                throw new ArgumentException("You cannot send a packet with a length of 0.");

            int count;
            if (length <= Mss)
                count = 1;
            else
                count = (int)((length + Mss - 1) / Mss);

            if (count > 255)
                throw new ArgumentException("Your packet is too big, please reduce its length or increase the MTU with SetMtu().");

            if (count == 0)
                count = 1;

            // fragment
            for (int i = 0; i < count; i++)
            {
                int size = Math.Min(length, (int)Mss);

                var seg = Segment.Get(size);
                seg.data.WriteBytes(buffer, index, size);
                index += size;
                length -= size;

                seg.frg = (byte)(count - i - 1);
                sendQueue.Add(seg);
            }
        }

        // update ack.
        void UpdateAck(int rtt)
        {
            // https://tools.ietf.org/html/rfc6298
            if (rx_srtt == 0)
            {
                rx_srtt = (uint)rtt;
                rx_rttval = (uint)rtt >> 1;
            }
            else
            {
                uint delta = (uint)Math.Abs(rtt - rx_srtt);
                rx_srtt += delta >> 3;

                if (rtt < rx_srtt - rx_rttval)
                {
                    // if the new RTT sample is below the bottom of the range of
                    // what an RTT measurement is expected to be.
                    // give an 8x reduced weight versus its normal weighting
                    rx_rttval += (delta - rx_rttval) >> 5;
                }
                else
                {
                    rx_rttval += (delta - rx_rttval) >> 2;
                }
            }

            uint rto = rx_srtt + Math.Max(interval, rx_rttval << 2);
            rx_rto = Utils.Clamp(rto, rx_minrto, RTO_MAX);
        }

        void ShrinkBuf()
        {
            snd_una = sendBuffer.Count > 0 ? sendBuffer[0].sn : snd_nxt;
        }

        void ParseAck(uint sn)
        {
            if (sn < snd_una || sn >= snd_nxt) return;

            foreach (Segment seg in sendBuffer)
            {
                if (sn == seg.sn)
                {
                    // mark and free space, but leave the segment here,
                    // and wait until `una` to delete this, then we don't
                    // have to shift the segments behind forward,
                    // which is an expensive operation for large window
                    seg.acked = true;
                    break;
                }
                if (sn < seg.sn)
                    break;
            }
        }

        void ParseFastrack(uint sn, uint ts)
        {
            if (sn < snd_una || sn >= snd_nxt)
                return;

            foreach (Segment seg in sendBuffer)
            {
                if (sn < seg.sn)
                    break;
                else if (sn != seg.sn && seg.ts <= ts)
                    seg.fastack++;
            }
        }

        void ParseUna(uint una)
        {
            int count = 0;
            foreach (Segment seg in sendBuffer)
            {
                if (una >seg.sn)
                {
                    count++;
                    Segment.Put(seg);
                }
                else
                    break;
            }

            sendBuffer.RemoveRange(0, count);
        }

        void AckPush(uint sn, uint ts)
        {
            ackList.Add(new AckItem { serialNumber = sn, timestamp = ts });
        }

        void ParseData(Segment newseg)
        {
            uint sn = newseg.sn;
            if (sn >= rcv_nxt + ReceiveWindowMax || sn < rcv_nxt)
                return;

            InsertSegmentInReceiveBuffer(newseg);
            MoveToReceiveQueue();
        }

        private void InsertSegmentInReceiveBuffer(Segment newseg)
        {
            uint sn = newseg.sn;
            int n = receiveBuffer.Count - 1;
            int insert_idx = 0;
            bool repeat = false;
            for (int i = n; i >= 0; i--)
            {
                Segment seg = receiveBuffer[i];
                if (seg.sn == sn)
                {
                    repeat = true;
                    break;
                }

                if (sn > seg.sn)
                {
                    insert_idx = i + 1;
                    break;
                }
            }

            if (!repeat)
            {
                if (insert_idx == n + 1)
                    receiveBuffer.Add(newseg);
                else
                    receiveBuffer.Insert(insert_idx, newseg);
            }
        }

        // move available data from rcv_buf -> rcv_queue
        private void MoveToReceiveQueue()
        {
            int count = 0;
            foreach (Segment seg in receiveBuffer)
            {
                if (seg.sn == rcv_nxt && receiveQueue.Count + count < ReceiveWindowMax)
                {
                    rcv_nxt++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            for (int i = 0; i < count; i++)
                receiveQueue.Add(receiveBuffer[i]);
            receiveBuffer.RemoveRange(0, count);
        }

        /// <summary>Input
        /// <para>Used when you receive a low level packet (eg. UDP packet)</para></summary>
        /// <param name="data"></param>
        /// <param name="index"></param>
        /// <param name="size"></param>
        /// <param name="regular">regular indicates a regular packet has received(not from FEC)</param>
        /// <param name="ackNoDelay">will trigger immediate ACK, but surely it will not be efficient in bandwidth</param>
        public int Input(byte[] data, int index, int size, bool regular, bool ackNoDelay)
        {
            uint s_una = snd_una;
            if (size < OVERHEAD) return -1;

            int offset = index;
            uint latest = 0;
            int flag = 0;

            while (true)
            {
                uint ts = 0;
                uint sn = 0;
                uint length = 0;
                uint una = 0;
                uint conv_ = 0;

                ushort wnd = 0;
                byte cmd = 0;
                byte frg = 0;

                if (size - (offset - index) < OVERHEAD) break;

                offset += Utils.Decode32U(data, offset, ref conv_);

                if (conv != conv_)
                    return -1;

                offset += Utils.Decode8u(data, offset, ref cmd);
                offset += Utils.Decode8u(data, offset, ref frg);
                offset += Utils.Decode16U(data, offset, ref wnd);
                offset += Utils.Decode32U(data, offset, ref ts);
                offset += Utils.Decode32U(data, offset, ref sn);
                offset += Utils.Decode32U(data, offset, ref una);
                offset += Utils.Decode32U(data, offset, ref length);

                if (size - (offset - index) < length)
                    return -2;

                switch (cmd)
                {
                    case CMD_PUSH:
                    case CMD_ACK:
                    case CMD_WASK:
                    case CMD_WINS:
                        break;
                    default:
                        return -3;
                }

                // only trust window updates from regular packets. i.e: latest update
                if (regular)
                {
                    RmtWnd = wnd;
                }

                ParseUna(una);
                ShrinkBuf();

                if (CMD_ACK == cmd)
                {
                    ParseAck(sn);
                    ParseFastrack(sn, ts);
                    flag |= 1;
                    latest = ts;
                }
                else if (CMD_PUSH == cmd)
                {
                    if (sn < rcv_nxt + ReceiveWindowMax)
                    {
                        AckPush(sn, ts);
                        if (sn >= rcv_nxt)
                        {
                            var seg = Segment.Get((int)length);
                            seg.conv = conv_;
                            seg.cmd = cmd;
                            seg.frg = frg;
                            seg.wnd = wnd;
                            seg.ts = ts;
                            seg.sn = sn;
                            seg.una = una;
                            seg.data.WriteBytes(data, offset, (int)length);
                            ParseData(seg);
                        }
                    }
                }
                else if (CMD_WASK == cmd)
                {
                    // ready to send back CMD_WINS in flush
                    // tell remote my window size
                    probe |= ASK_TELL;
                }
                else if (CMD_WINS == cmd)
                {
                    // do nothing
                }
                else
                {
                    return -3;
                }

                offset += (int)length;
            }

            // update rtt with the latest ts
            // ignore the FEC packet
            if (flag != 0 && regular)
            {
                uint current = CurrentMS;
                if (current >= latest)
                {
                    UpdateAck(Utils.TimeDiff(current, latest));
                }
            }

            // cwnd update when packet arrived
            UpdateCwnd(s_una);

            // ack immediately
            if (ackNoDelay && ackList.Count > 0)
            {
                Flush(true);
            }

            return 0;
        }

        void UpdateCwnd(uint s_una)
        {
            if (!nocwnd && snd_una > s_una && cwnd < RmtWnd)
            {
                uint _mss = Mss;
                if (cwnd < ssthresh)
                {
                    cwnd++;
                    incr += _mss;
                }
                else
                {
                    incr = Math.Max(incr, _mss);

                    incr += _mss * _mss / incr + _mss / 16;

                    if ((cwnd + 1) * _mss <= incr)
                    {
                        cwnd = incr + _mss - 1;

                        if (_mss > 0)
                            cwnd /= _mss;
                    }
                }
                if (cwnd > RmtWnd)
                {
                    cwnd = RmtWnd;
                    incr = RmtWnd * _mss;
                }
            }
        }

        ushort WndUnused()
        {
            if (receiveQueue.Count < ReceiveWindowMax)
                return (ushort)(ReceiveWindowMax - receiveQueue.Count);
            return 0;
        }

        /// <summary>Flush</summary>
        /// <param name="ackOnly">flush remain ack segments</param>
        public uint Flush(bool ackOnly)
        {
            var seg = Segment.Get(32);
            seg.conv = conv;
            seg.cmd = CMD_ACK;
            seg.wnd = WndUnused();
            seg.una = rcv_nxt;

            int writeIndex = (int)reserved;

            void makeSpace(int space)
            {
                if (writeIndex + space > mtu)
                {
                    output(buffer, writeIndex);
                    writeIndex = (int)reserved;
                }
            }

            void flushBuffer()
            {
                if (writeIndex > reserved)
                {
                    output(buffer, writeIndex);
                }
            }

            // flush acknowledges
            for (int i = 0; i < ackList.Count; i++)
            {
                makeSpace(OVERHEAD);
                AckItem ack = ackList[i];
                if (ack.serialNumber >= rcv_nxt || ackList.Count - 1 == i)
                {
                    seg.sn = ack.serialNumber;
                    seg.ts = ack.timestamp;
                    writeIndex += seg.Encode(buffer, writeIndex);
                }
            }
            ackList.Clear();

            // flush remain ack segments
            if (ackOnly)
            {
                flushBuffer();
                return interval;
            }

            uint current = 0;
            // probe window size (if remote window size equals zero)
            if (RmtWnd == 0)
            {
                current = CurrentMS;
                if (probe_wait == 0)
                {
                    probe_wait = PROBE_INIT;
                    ts_probe = current + probe_wait;
                }
                else
                {
                    if (current >= ts_probe)
                    {
                        probe_wait = Math.Max(probe_wait, PROBE_INIT);
                        probe_wait += probe_wait / 2;
                        probe_wait = Math.Min(probe_wait, PROBE_LIMIT);
                        ts_probe = current + probe_wait;
                        probe |= ASK_SEND;
                    }
                }
            }
            else
            {
                ts_probe = 0;
                probe_wait = 0;
            }

            // flush window probing commands
            if ((probe & ASK_SEND) != 0)
            {
                seg.cmd = CMD_WASK;
                makeSpace(OVERHEAD);
                writeIndex += seg.Encode(buffer, writeIndex);
            }

            if ((probe & ASK_TELL) != 0)
            {
                seg.cmd = CMD_WINS;
                makeSpace(OVERHEAD);
                writeIndex += seg.Encode(buffer, writeIndex);
            }

            probe = 0;

            // calculate window size
            uint cwnd_ = Math.Min(SendWindowMax, RmtWnd);
            if (!nocwnd)
                cwnd_ = Math.Min(cwnd, cwnd_);

            // sliding window, controlled by snd_nxt && sna_una+cwnd
            int newSegsCount = 0;
            for (int k = 0; k < sendQueue.Count; k++)
            {
                if (snd_nxt >= snd_una + cwnd_)
                    break;

                Segment newseg = sendQueue[k];
                newseg.conv = conv;
                newseg.cmd = CMD_PUSH;
                newseg.sn = snd_nxt;
                sendBuffer.Add(newseg);
                snd_nxt++;
                newSegsCount++;
            }

            sendQueue.RemoveRange(0, newSegsCount);

            // calculate resent
            uint resent = (uint)fastresend;
            if (fastresend <= 0) resent = 0xffffffff;

            // check for retransmissions
            current = CurrentMS;
            ulong change = 0; ulong lostSegs = 0;
            int minrto = (int)interval;

            for (int k = 0; k < sendBuffer.Count; k++)
            {
                Segment segment = sendBuffer[k];
                bool needSend = false;
                if (segment.acked)
                    continue;
                if (segment.xmit == 0)  // initial transmit
                {
                    needSend = true;
                    segment.rto = rx_rto;
                    segment.resendts = current + segment.rto;
                }
                else if (segment.fastack >= resent || segment.fastack > 0 && newSegsCount == 0 ) // fast retransmit
                {
                    needSend = true;
                    segment.fastack = 0;
                    segment.rto = rx_rto;
                    segment.resendts = current + segment.rto;
                    change++;
                }
                else if (current >= segment.resendts) // RTO
                {
                    needSend = true;
                    if (!noDelay)
                        segment.rto += rx_rto;
                    else
                        segment.rto += rx_rto / 2;
                    segment.fastack = 0;
                    segment.resendts = current + segment.rto;
                    lostSegs++;
                }

                if (needSend)
                {
                    current = CurrentMS;
                    segment.xmit++;
                    segment.ts = current;
                    segment.wnd = seg.wnd;
                    segment.una = seg.una;

                    int need = OVERHEAD + segment.data.ReadableBytes;
                    makeSpace(need);
                    writeIndex += segment.Encode(buffer, writeIndex);
                    Buffer.BlockCopy(segment.data.RawBuffer, segment.data.ReaderIndex, buffer, writeIndex, segment.data.ReadableBytes);
                    writeIndex += segment.data.ReadableBytes;
                }

                // get the nearest rto
                int _rto = Utils.TimeDiff(segment.resendts, current);
                if (_rto > 0 && _rto < minrto)
                {
                    minrto = _rto;
                }
            }

            // flash remain segments
            flushBuffer();

            // cwnd update
            if (!nocwnd)
            {
                CwndUpdate(resent, change, lostSegs);
            }

            return (uint)minrto;
        }

        private void CwndUpdate(uint resent, ulong change, ulong lostSegs)
        {
            // update ssthresh
            // rate halving, https://tools.ietf.org/html/rfc6937
            if (change > 0)
            {
                uint inflght = snd_nxt - snd_una;
                ssthresh = inflght / 2;
                if (ssthresh < THRESH_MIN)
                    ssthresh = THRESH_MIN;
                cwnd = ssthresh + resent;
                incr = cwnd * Mss;
            }

            // congestion control, https://tools.ietf.org/html/rfc5681
            if (lostSegs > 0)
            {
                ssthresh = cwnd / 2;
                if (ssthresh < THRESH_MIN)
                    ssthresh = THRESH_MIN;
                cwnd = 1;
                incr = Mss;
            }

            if (cwnd < 1)
            {
                cwnd = 1;
                incr = Mss;
            }
        }

        /// <summary>Update
        /// Determine when should you invoke update
        /// <para>update state (call it repeatedly, every 10ms-100ms)</para>
        /// </summary>
        public void Update()
        {
            uint current = CurrentMS;

            if (!updated)
            {
                updated = true;
                ts_flush = current;
            }

            int slap = Utils.TimeDiff(current, ts_flush);

            if (slap >= 10000 || slap < -10000)
            {
                ts_flush = current;
                slap = 0;
            }

            if (slap >= 0)
            {
                ts_flush += interval;
                if (current >= ts_flush)
                    ts_flush = current + interval;
                Flush(false);
            }
        }

        /// <summary>Check
        /// Determine when should you invoke update
        /// <para>Returns when you should invoke update in millisec, if there
        /// is no input/_send calling. you can call update in that
        /// time, instead of call update repeatly.</para>
        /// <para>Important to reduce unnacessary update invoking. use it to
        /// schedule update (eg. implementing an epoll-like mechanism, or
        /// optimize update when handling massive kcp connections)</para>
        /// <remark> Standard KCP returns time as current + delta.  This version returns delta</remark>
        /// </summary>
        public int Check()
        {
            uint current = CurrentMS;

            uint ts_flush_ = ts_flush;
            int tm_packet = 0x7fffffff;

            if (!updated)
                return 0;

            if (current >= ts_flush_ + 10000 || current < ts_flush_ - 10000)
                ts_flush_ = current;

            if (current >= ts_flush_)
                return 0;

            int tm_flush_ = Utils.TimeDiff(ts_flush_, current);

            foreach (Segment seg in sendBuffer)
            {
                int diff = Utils.TimeDiff(seg.resendts, current);
                if (diff <= 0)
                    return 0;
                if (diff < tm_packet)
                    tm_packet = diff;
            }

            int minimal = tm_packet;
            if (tm_packet >= tm_flush_)
                minimal = tm_flush_;
            if (minimal >= interval)
                minimal = (int)interval;

            // NOTE: Original KCP returns current time + delta
            // I changed it to only return delta

            return  minimal;
        }

        /// <summary>Change MTU (Maximum Transmission Unit) size. Default is 1200.</summary>
        /// <param name="mtu">Maximum Transmission Unit size. Can't be lower than 50 and must be higher than reserved bytes.</param>
        public void SetMtu(uint mtu)
        {
            if (mtu < 50)
                throw new ArgumentException("MTU must be higher than 50.");
            if (reserved >= (int)(mtu - OVERHEAD))
                throw new ArgumentException("Please increase the MTU value so it is higher than reserved bytes.");

            buffer = new byte[mtu];

            this.mtu = mtu;
        }

        /// <summary>SetNoDelay
        /// <para>Normal: false, 40, 0, 0</para>
        /// <para>Fast:    false, 30, 2, 1</para>
        /// <para>Fast2:   true, 20, 2, 1</para>
        /// <para>Fast3:   true, 10, 2, 1</para>
        /// </summary>
        /// <param name="nodelay">Whether to enable nodelay mode.</param>
        /// <param name="interval">Interval of the internal working of the protocol, in milliseconds, such as 10ms or 20ms.</param>
        /// <param name="resend">fast retransmission mode, default 0 is off, 1 fast resend, 2 can be set (2 ACK crossings will directly retransmit).</param>
        /// <param name="nc">Whether to close the flow control (congestion), the default is 0 means not to close, 1 means to close.</param>
        public void SetNoDelay(bool nodelay = false, uint interval = 40, int resend = 0, bool nc = false)
        {
            if (nodelay)
            {
                noDelay = nodelay;
                rx_minrto = RTO_NDL;
            }

            if (interval >= 0)
            {
                if (interval > 5000)
                    interval = 5000;
                else if (interval < 10)
                    interval = 10;
                this.interval = interval;
            }

            if (resend >= 0)
                fastresend = resend;

            nocwnd = nc;
        }

        /// <summary>SetWindowSize
        /// sets maximum window size</summary>
        /// <param name="sendWindow">32 by default</param>
        /// <param name="receiveWindow">32 by default</param>
        public void SetWindowSize(uint sendWindow = 32, uint receiveWindow = 32)
        {
            if (sendWindow > 0)
                SendWindowMax = sendWindow;

            if (receiveWindow > 0)
                ReceiveWindowMax = Math.Max(receiveWindow, WND_RCV);
        }

        /// <summary>ReserveBytes</summary>
        /// <param name="reservedSize"></param>
        public void ReserveBytes(uint reservedSize)
        {
            if (reservedSize >= (mtu - OVERHEAD))
                throw new ArgumentException("reservedSize must be lower than MTU.");

            reserved = reservedSize;
        }
    }
}
