using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mirror.KCP
{

    public enum KcpDelayMode { Normal, Fast, Fast2, Fast3 } //See SetNoDelay for details

    public class Kcp
    {
        public const int RTO_MAX = 60000; // Maximum RTO
        public const int RTO_NDL = 30; // no delay min rto
        public const int RTO_MIN = 100;
        public const int ASK_SEND = 1;  // need to send CMD_WASK
        public const int ASK_TELL = 2;  // need to send CMD_WINS
        public const int WND_SND = 32; // defualt Send Window
        public const int WND_RCV = 128; // default Receive Window, must be >= max fragment size
        public const int OVERHEAD = 24; //related to MTU
        public const int PROBE_INIT = 7000;   // 7 secs to probe window size
        public const int PROBE_LIMIT = 120000; // up to 120 secs to probe window

        readonly Stopwatch refTime = new Stopwatch();

        internal struct AckItem
        {
            internal uint serialNumber;
            internal uint timestamp;
        }

        // kcp members.
        readonly uint conv;
        uint mtu = 1200; // MTU Default.
        uint snd_unacknowledged;
        uint snd_nxt;
        uint rcv_nxt;
        uint slowStartThreshhold = 2;
        int rx_rttval;
        int rx_SmoothedRoundTripTime; // Used by UpdateAck
        int rx_rto = 200; // Default RTO
        int rx_MinimumRto = 100; // normal min rto
        uint CongestionWindow;
        uint probe;
        int interval = 100;
        uint ts_flush = 100;
        bool noDelay;
        bool updated;
        uint timeStamp_probe;
        uint probe_wait;
        uint incr;

        int fastresend;
        bool nocwnd;
        readonly List<Segment> sendQueue = new List<Segment>(16);
        readonly List<Segment> receiveQueue = new List<Segment>(16);
        readonly List<Segment> sendBuffer = new List<Segment>(16);
        readonly List<Segment> receiveBuffer = new List<Segment>(16);
        readonly List<AckItem> ackList = new List<AckItem>(16);

        byte[] buffer;
        uint reserved;
        readonly Action<byte[], int> output; // buffer, size

        public uint SendWindowMax { get; private set; }
        public uint ReceiveWindowMax { get; private set; }
        public uint RmtWnd { get; private set; }
        public uint MaximumSegmentSize => mtu - OVERHEAD - reserved;

        /// <summary>
        /// Returns int count of current packets waiting to be sent
        /// </summary>
        public int WaitSnd => sendBuffer.Count + sendQueue.Count;

        /// <summary>
        /// Returns uint current internal time in Milliseconds
        /// </summary>
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
            buffer = new byte[(mtu + OVERHEAD) * 3];
            output = output_;
            refTime.Start();
        }

        /// <summary>PeekSize
        /// <para>check the size of next message in the recv queue</para>
        /// <return>Returns int (-1, length or readablebytes)</return></summary>
        public int PeekSize()
        {
            if (receiveQueue.Count == 0)
                return -1;

            Segment seq = receiveQueue[0];

            if (seq.fragment == 0)
                return (int)seq.data.Length;

            if (receiveQueue.Count < seq.fragment + 1)
                return -1;

            int length = 0;

            foreach (Segment item in receiveQueue)
            {
                length += (int)item.data.Length;
                if (item.fragment == 0)
                    break;
            }

            return length;
        }

        /// <summary>Receive
        /// <para>Receive data from kcp state machine</para>
        /// <return>Return number of bytes read.
        /// Return -1 when there is no readable data.
        /// Return -2 if len(buffer) is smaller than kcp.PeekSize().</return></summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public int Receive(byte[] buffer, int offset, int length)
        {
            int peekSize = PeekSize();
            if (peekSize < 0)
                return -1;

            if (peekSize > length)
                return -2;

            bool fastRecover = receiveQueue.Count >= ReceiveWindowMax;

            // merge fragment.
            int size = DequeueMessage(buffer, offset);

            // move available data from rcv_buf -> rcv_queue
            FillReceiveQueue();

            // fast recover
            if (receiveQueue.Count < ReceiveWindowMax && fastRecover)
            {
                // ready to send back CMD_WINS in flush
                // tell remote my window size
                probe |= ASK_TELL;
            }

            return size;
        }

        private int DequeueMessage(byte[] buffer, int offset)
        {
            int count = 0;
            int n = offset;

            foreach (Segment seg in receiveQueue)
            {
                seg.data.Position = 0;
                seg.data.Read(buffer, n, (int)seg.data.Length);
                // copy fragment data into buffer.
                n += (int)seg.data.Length;

                count++;
                uint fragment = seg.fragment;
                Segment.Put(seg);
                if (fragment == 0)
                    break;
            }

            receiveQueue.RemoveRange(0, count);
            return n - offset;
        }

        /// <summary>Send
        /// <para>user/upper level send</para></summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public void Send(byte[] buffer, int offset, int length)
        {
            if (length == 0)
                throw new ArgumentException("You cannot send a packet with a " + nameof(length) + " of 0.");

            int count;
            if (length <= MaximumSegmentSize)
                count = 1;
            else
                count = (int)((length + MaximumSegmentSize - 1) / MaximumSegmentSize);

            if (count > 255)
                throw new ArgumentException("Your packet is too big, please reduce its " + nameof(length) + " or increase the MTU with SetMtu().");

            if (count == 0)
                count = 1;

            // fragment
            for (int i = 0; i < count; i++)
            {
                int size = Math.Min(length, (int)MaximumSegmentSize);

                var seg = Segment.Get(size);
                seg.data.Write(buffer, offset, size);
                offset += size;
                length -= size;

                seg.fragment = (byte)(count - i - 1);
                sendQueue.Add(seg);
            }
        }

        // update ack.
        void UpdateAck(int roundTripTime)
        {
            // https://tools.ietf.org/html/rfc6298
            if (rx_SmoothedRoundTripTime == 0)
            {
                rx_SmoothedRoundTripTime = roundTripTime;
                rx_rttval = roundTripTime >> 1;
            }
            else
            {
                int delta = Math.Abs(roundTripTime - rx_SmoothedRoundTripTime);

                rx_rttval = (3 * rx_rttval + delta) >> 2;
                rx_SmoothedRoundTripTime = (7 * rx_SmoothedRoundTripTime + roundTripTime) >> 3;

                if (rx_SmoothedRoundTripTime < 1)
                    rx_SmoothedRoundTripTime = 1;
            }

            int rto = rx_SmoothedRoundTripTime + Math.Max(interval, rx_rttval << 2);
            rx_rto = Utils.Clamp(rto, rx_MinimumRto, RTO_MAX);
        }

        void ShrinkBuf()
        {
            snd_unacknowledged = sendBuffer.Count > 0 ? sendBuffer[0].serialNumber : snd_nxt;
        }

        void ParseAck(uint serialNumber)
        {
            if (serialNumber < snd_unacknowledged || serialNumber >= snd_nxt) return;

            foreach (Segment seg in sendBuffer)
            {
                if (serialNumber == seg.serialNumber)
                {
                    // mark and free space, but leave the segment here,
                    // and wait until `una` to delete this, then we don't
                    // have to shift the segments behind forward,
                    // which is an expensive operation for large window
                    seg.acked = true;
                    break;
                }
                if (serialNumber < seg.serialNumber)
                    break;
            }
        }

        void ParseFastrack(uint serialNumber, uint timeStamp)
        {
            if (serialNumber < snd_unacknowledged || serialNumber >= snd_nxt)
                return;

            foreach (Segment seg in sendBuffer)
            {
                if (serialNumber < seg.serialNumber)
                    break;
                else if (serialNumber != seg.serialNumber && seg.timeStamp <= timeStamp)
                    seg.fastack++;
            }
        }

        void ParseUna(uint unacknowledged)
        {
            int count = 0;
            foreach (Segment seg in sendBuffer)
            {
                if (unacknowledged >seg.serialNumber)
                {
                    count++;
                    Segment.Put(seg);
                }
                else
                    break;
            }

            sendBuffer.RemoveRange(0, count);
        }

        void AckPush(uint serialnumber, uint timestamp)
        {
            ackList.Add(new AckItem { serialNumber = serialnumber, timestamp = timestamp });
        }

        void ParseData(Segment newseg)
        {
            uint serialNumber = newseg.serialNumber;
            if (serialNumber >= rcv_nxt + ReceiveWindowMax || serialNumber < rcv_nxt)
            {
                Segment.Put(newseg);
                return;
            }

            InsertSegmentInReceiveBuffer(newseg);
            FillReceiveQueue();
        }

        void InsertSegmentInReceiveBuffer(Segment newseg)
        {
            uint serialNumber = newseg.serialNumber;
            int n = receiveBuffer.Count - 1;
            int insert_idx = 0;

            // note this works better in this case than binary search
            // because segments tend to go near the end of the buffer
            for (int i = n; i >= 0; i--)
            {
                Segment seg = receiveBuffer[i];
                if (seg.serialNumber == serialNumber)
                {
                    Segment.Put(newseg);
                    return;
                }

                if (serialNumber > seg.serialNumber)
                {
                    insert_idx = i + 1;
                    break;
                }
            }

            if (insert_idx == n + 1)
                receiveBuffer.Add(newseg);
            else
                receiveBuffer.Insert(insert_idx, newseg);
        }

        // move available data from rcv_buf -> rcv_queue
        void FillReceiveQueue()
        {
            int count = 0;
            foreach (Segment seg in receiveBuffer)
            {
                if (seg.serialNumber == rcv_nxt && receiveQueue.Count < ReceiveWindowMax)
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
        }

        /// <summary>Input
        /// <para>Used when you receive a low level packet (eg. UDP packet)</para>
        /// <returns>Returns int (-3, -1, or 0)</returns></summary>
        /// <param name="data"></param>
        /// <param name="index"></param>
        /// <param name="size"></param>
        public int Input(byte[] data, int index, int size)
        {
            if (size < OVERHEAD) return -1;

            int offset = index;
            uint latest = 0;
            bool flag = false;

            while (true)
            {
                if (size - (offset - index) < OVERHEAD) break;

                var decoder = new Decoder(data, offset);
                uint conv_ = decoder.Decode32U();

                if (conv != conv_)
                    return -1;

                var cmd = (CommandType)decoder.Decode8U();
                byte frg = decoder.Decode8U();
                ushort wnd = decoder.Decode16U();
                uint ts = decoder.Decode32U();
                uint sn = decoder.Decode32U();
                uint una = decoder.Decode32U();
                uint length = decoder.Decode32U();

                offset = decoder.Position;
                if (size - (offset - index) < length)
                    return -2;

                switch (cmd)
                {
                    case CommandType.Push:
                    case CommandType.Ack:
                    case CommandType.WindowAsk:
                    case CommandType.WindowTell:
                        break;
                    default:
                        return -3;
                }

                RmtWnd = wnd;

                ParseUna(una);
                ShrinkBuf();

                switch (cmd)
                {
                    case CommandType.Ack:
                        ParseAck(sn);
                        ParseFastrack(sn, ts);
                        flag = true;
                        latest = ts;
                        break;
                    case CommandType.Push:
                        if (sn < rcv_nxt + ReceiveWindowMax)
                        {
                            AckPush(sn, ts);
                            if (sn >= rcv_nxt)
                            {
                                var seg = Segment.Get((int)length);
                                seg.conversation = conv_;
                                seg.cmd = cmd;
                                seg.fragment = frg;
                                seg.window = wnd;
                                seg.timeStamp = ts;
                                seg.serialNumber = sn;
                                seg.unacknowledged = una;
                                seg.data.Write(data, offset, (int)length);
                                ParseData(seg);
                            }
                        }
                        break;
                    case CommandType.WindowAsk:
                        // ready to send back CMD_WINS in flush
                        // tell remote my window size
                        probe |= ASK_TELL;
                        break;
                    case CommandType.WindowTell:
                        // do nothing
                        break;
                    default:
                        return -3;
                }

                offset += (int)length;
            }

            // update rtt with the latest ts
            // ignore the FEC packet
            if (flag)
            {
                uint current = CurrentMS;
                if (current >= latest)
                {
                    UpdateAck(Utils.TimeDiff(current, latest));
                }
            }

            // cwnd update when packet arrived
            UpdateCwnd(snd_unacknowledged);

            return 0;
        }

        void UpdateCwnd(uint s_una)
        {
            if (!nocwnd && snd_unacknowledged > s_una && CongestionWindow < RmtWnd)
            {                
                if (CongestionWindow < slowStartThreshhold)
                {
                    CongestionWindow++;
                    incr += MaximumSegmentSize;
                }
                else
                {
                    incr = Math.Max(incr, MaximumSegmentSize);

                    incr += MaximumSegmentSize * MaximumSegmentSize / incr + MaximumSegmentSize / 16;

                    if ((CongestionWindow + 1) * MaximumSegmentSize <= incr)
                    {
                        CongestionWindow = incr + MaximumSegmentSize - 1;

                        if (MaximumSegmentSize > 0)
                            CongestionWindow /= MaximumSegmentSize;                            
                    }
                }
                if (CongestionWindow > RmtWnd)
                {
                    CongestionWindow = RmtWnd;
                    incr = RmtWnd * MaximumSegmentSize;
                }                
            }
        }

        ushort WndUnused()
        {
            if (receiveQueue.Count < ReceiveWindowMax)
                return (ushort)(ReceiveWindowMax - receiveQueue.Count);
            return 0;
        }


        int writeIndex;
        void MakeSpace(int space)
        {
            if (writeIndex + space > mtu)
            {
                output(buffer, writeIndex);
                writeIndex = (int)reserved;
            }
        }

        void FlushBuffer()
        {
            if (writeIndex > reserved)
            {
                output(buffer, writeIndex);
            }
        }

        void FlushAcknowledges(Segment seg)
        {
            foreach (AckItem ack in ackList)
            {
                MakeSpace(OVERHEAD);

                seg.serialNumber = ack.serialNumber;
                seg.timeStamp = ack.timestamp;
                writeIndex += seg.Encode(buffer, writeIndex);
            }
            ackList.Clear();
        }

        int FillSendBuffer(uint cwnd_)
        {
            // sliding window, controlled by snd_nxt && sna_una+cwnd
            int newSegsCount = 0;
            foreach (Segment newseg in sendQueue)
            {
                if (snd_nxt >= snd_unacknowledged + cwnd_)
                    break;

                newseg.conversation = conv;
                newseg.cmd = CommandType.Push;
                newseg.serialNumber = snd_nxt;
                sendBuffer.Add(newseg);
                snd_nxt++;
                newSegsCount++;
            }

            sendQueue.RemoveRange(0, newSegsCount);

            return newSegsCount;
        }

        uint CalculateWindowSize()
        {
            uint cwnd_ = Math.Min(SendWindowMax, RmtWnd);
            if (!nocwnd)
                cwnd_ = Math.Min(CongestionWindow, cwnd_);
            return cwnd_;
        }

        uint CalculateResent()
        {
            // calculate resent
            uint resent = (uint)fastresend;
            if (fastresend <= 0) resent = 0xffffffff;
            return resent;
        }

        void FlushWindowProbingCommands(Segment seg)
        {
            if ((probe & ASK_SEND) != 0)
            {
                seg.cmd = CommandType.WindowAsk;
                MakeSpace(OVERHEAD);
                writeIndex += seg.Encode(buffer, writeIndex);
            }

            if ((probe & ASK_TELL) != 0)
            {
                seg.cmd = CommandType.WindowTell;
                MakeSpace(OVERHEAD);
                writeIndex += seg.Encode(buffer, writeIndex);
            }

            probe = 0;
        }

        /// <summary><para>Flush</para>
        /// <return>Returns int (interval or mintro)</return></summary>
        /// <param name="ackOnly">flush remain ack segments</param>
        public int Flush(bool ackOnly)
        {
            var seg = Segment.Get(32);
            seg.conversation = conv;
            seg.cmd = CommandType.Ack;
            seg.window = WndUnused();
            seg.unacknowledged = rcv_nxt;

            writeIndex = (int)reserved;

            FlushAcknowledges(seg);

            // flush remain ack segments
            if (ackOnly)
            {
                FlushBuffer();
                Segment.Put(seg);
                return interval;
            }

            uint current = CurrentMS;

            ProbeWindowSize(current);

            FlushWindowProbingCommands(seg);

            return CheckForRetransmission(seg, current);
        }

        int CheckForRetransmission(Segment seg, uint current)
        {
            // sliding window, controlled by snd_nxt && sna_una+cwnd
            int newSegsCount = FillSendBuffer(CalculateWindowSize());

            // check for retransmissions
            ulong change = 0;
            ulong lostSegs = 0;
            int minrto = interval;

            for (int k = 0; k < sendBuffer.Count; k++)
            {
                Segment segment = sendBuffer[k];
                bool needSend = false;
                if (segment.acked)
                    continue;
                if (segment.transmit == 0)  // initial transmit
                {
                    needSend = true;
                    segment.rto = (uint)rx_rto;
                    segment.resendTimeStamp = current + segment.rto;
                }
                else if (segment.fastack >= CalculateResent() || segment.fastack > 0 && newSegsCount == 0) // fast retransmit
                {
                    needSend = true;
                    segment.fastack = 0;
                    segment.rto = (uint)rx_rto;
                    segment.resendTimeStamp = current + segment.rto;
                    change++;
                }
                else if (current >= segment.resendTimeStamp) // RTO
                {
                    needSend = true;

                    segment.rto += noDelay ? (uint)rx_rto >> 1 : (uint)rx_rto;
                    segment.fastack = 0;
                    segment.resendTimeStamp = current + segment.rto;
                    lostSegs++;
                }

                if (needSend)
                {
                    segment.transmit++;
                    segment.timeStamp = current;
                    segment.window = seg.window;
                    segment.unacknowledged = seg.unacknowledged;

                    int need = (int)(OVERHEAD + segment.data.Position);
                    MakeSpace(need);
                    writeIndex += segment.Encode(buffer, writeIndex);
                    segment.data.Position = 0;
                    segment.data.Read(buffer, writeIndex, (int)segment.data.Length);
                    writeIndex += (int)segment.data.Length;
                }

                // get the nearest rto
                int _rto = Utils.TimeDiff(segment.resendTimeStamp, current);
                if (_rto > 0 && _rto < minrto)
                {
                    minrto = _rto;
                }
            }

            // flash remain segments
            FlushBuffer();

            // cwnd update
            if (!nocwnd)
            {
                CwndUpdate(CalculateResent(), change, lostSegs);
            }

            Segment.Put(seg);
            return minrto;
        }

        void ProbeWindowSize(uint current)
        {
            // probe window size (if remote window size equals zero)
            if (RmtWnd == 0)
            {
                if (probe_wait == 0)
                {
                    probe_wait = PROBE_INIT;
                    timeStamp_probe = current + probe_wait;
                }
                else
                {
                    if (current >= timeStamp_probe)
                    {
                        probe_wait = Math.Max(probe_wait, PROBE_INIT);
                        probe_wait += probe_wait / 2;
                        probe_wait = Math.Min(probe_wait, PROBE_LIMIT);
                        timeStamp_probe = current + probe_wait;
                        probe |= ASK_SEND;
                    }
                }
            }
            else
            {
                timeStamp_probe = 0;
                probe_wait = 0;
            }
        }

        void SetThresh(uint value)
        {
            slowStartThreshhold = value;
            if (slowStartThreshhold < 2)
                slowStartThreshhold = 2;
        }

        void CwndUpdate(uint resent, ulong change, ulong lostSegs)
        {
            // update ssthresh
            // rate halving, https://tools.ietf.org/html/rfc6937
            if (change > 0)
            {
                SetThresh((snd_nxt - snd_unacknowledged) / 2);
                CongestionWindow = slowStartThreshhold + resent;
                incr = CongestionWindow * MaximumSegmentSize;
            }

            // congestion control, https://tools.ietf.org/html/rfc5681
            if (lostSegs > 0)
            {
                SetThresh(CongestionWindow / 2);
                CongestionWindow = 1;
                incr = MaximumSegmentSize;
            }

            if (CongestionWindow < 1)
            {
                CongestionWindow = 1;
                incr = MaximumSegmentSize;
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
                ts_flush += (uint)interval;
                if (current >= ts_flush)
                    ts_flush = current + (uint)interval;
                Flush(false);
            }
        }

        /// <summary>Check
        /// Determine when should you invoke update
        /// <para>Returns when you should invoke update in millisec, if there
        /// is no input/_send calling. you can call update in that
        /// time, instead of call update repeatly.
        /// Important to reduce unnacessary update invoking. use it to
        /// schedule update (eg. implementing an epoll-like mechanism, or
        /// optimize update when handling massive kcp connections)
        /// Standard KCP returns time as current + delta.  This version returns delta</para>
        /// <returns>Returns int (0 or minimum interval)</returns>
        /// </summary>
        public int Check()
        {
            uint current = CurrentMS;

            uint timeStamp_flush_ = ts_flush;
            int tm_packet = 0x7fffffff;

            if (!updated)
                return 0;

            if (current >= timeStamp_flush_ + 10000 || current < timeStamp_flush_ - 10000)
                timeStamp_flush_ = current;

            if (current >= timeStamp_flush_)
                return 0;

            int tm_flush_ = Utils.TimeDiff(timeStamp_flush_, current);

            foreach (Segment seg in sendBuffer)
            {
                int diff = Utils.TimeDiff(seg.resendTimeStamp, current);
                if (diff <= 0)
                    return 0;
                if (diff < tm_packet)
                    tm_packet = diff;
            }

            int minimal = Math.Min(tm_packet, tm_flush_);
            minimal = Math.Min(minimal, interval);

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

            buffer = new byte[(mtu + OVERHEAD) * 3];

            this.mtu = mtu;
        }

        /// <summary>SetNoDelay
        /// <para>Normal: false, 40, 0, false</para>
        /// <para>Fast:    false, 30, 2, true</para>
        /// <para>Fast2:   true, 20, 2, true</para>
        /// <para>Fast3:   true, 10, 2, true</para>
        /// </summary>
        /// <param name="nodelay">Whether to enable nodelay mode.</param>
        /// <param name="interval">Interval of the internal working of the protocol, in milliseconds, such as 10ms or 20ms.</param>
        /// <param name="resend">fast retransmission mode, default 0 is off, 1 fast resend, 2 can be set (2 ACK crossings will directly retransmit).</param>
        /// <param name="nc">Whether to close the flow control (congestion), the default is 0 means not to close, 1 means to close.</param>
        public void SetNoDelay(bool nodelay = false, int interval = 40, int resend = 0, bool nc = false)
        {
            noDelay = nodelay;

            rx_MinimumRto = nodelay ? RTO_NDL : RTO_MIN;

            this.interval = Utils.Clamp(interval, 10, 5000);

            if (resend >= 0)
                fastresend = resend;

            nocwnd = nc;
        }

        public void SetNoDelay(KcpDelayMode kcpDelayMode)
        {
            switch (kcpDelayMode)
            {
                case KcpDelayMode.Normal:
                    SetNoDelay(false, 40, 0, false);
                    break;
                case KcpDelayMode.Fast:
                    SetNoDelay(false, 30, 2, true);
                    break;
                case KcpDelayMode.Fast2:
                    SetNoDelay(true, 20, 2, true);
                    break;
                case KcpDelayMode.Fast3:
                    SetNoDelay(true, 10, 2, true);
                    break;
            }
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
                throw new ArgumentException(nameof(reservedSize) + " must be lower than MTU.");

            reserved = reservedSize;
        }
    }
}
