using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.KCP
{
    //See SetNoDelay for details
    public enum KcpDelayMode { Normal, Fast, Fast2, Fast3 }

    /// <summary>
    /// A reliability algorithm over an unreliable transport such as UDP.
    /// based on https://github.com/skywind3000/kcp
    /// </summary>
    public class Kcp
    {

        public const int RTO_NDL = 30;           // no delay min rto
        public const int RTO_MIN = 100;          // normal min rto
        public const int RTO_MAX = 60000;        // maximum RTO
        public const int ASK_SEND = 1;           // need to send CMD_WASK
        public const int ASK_TELL = 2;           // need to send CMD_WINS
        public const int WND_RCV = 128;          // default receive window. must be >= max fragment size
        public const int INTERVAL = 100;
        public const int OVERHEAD = 24;
        public const int THRESH_INIT = 2;
        public const int THRESH_MIN = 2;
        public const int PROBE_INIT = 7000;      // 7 secs to probe window size
        public const int PROBE_LIMIT = 120000;   // up to 120 secs to probe window

        private struct AckItem
        {
            internal uint serialNumber;
            internal uint timestamp;
        }

        private int reserved;

        /// <summary>
        /// How many bytes to reserve at beginning of a packet
        /// the extra bytes can be used to store a CRC or other information
        /// </summary>
        public int Reserved
        {
            get
            {
                return reserved;
            }
            set
            {
                if (value >= (mtu - OVERHEAD))
                    throw new ArgumentException(nameof(Reserved) + " must be lower than MTU.");
                reserved = value;
            }

        }

        // kcp members.
        readonly uint conv;                    // conversation
        private uint mtu = 1200; // default MTU (reduced to 1200 to fit all cases: https://en.wikipedia.org/wiki/Maximum_transmission_unit ; steam uses 1200 too!)
        private uint Mss => (uint)(mtu - OVERHEAD - Reserved);           // maximum segment size
        private uint snd_una;                 // unacknowledged
        private uint snd_nxt;
        private uint rcv_nxt;
        private uint ssthresh = THRESH_INIT;  // slow start threshold
        private int rx_rttval;
        private int rx_srtt;                  // smoothed round trip time
        private int rx_rto = 200;
        private int rx_minrto = RTO_MIN;
        private uint snd_wnd = 32;       // send window
        private uint rcv_wnd = WND_RCV;       // receive window
        private uint rmt_wnd = WND_RCV;       // remote window
        private uint cwnd;                    // congestion window
        private uint probe;
        private uint interval = INTERVAL;
        private uint ts_flush = INTERVAL;
        private bool nodelay;
        private bool updated;
        private uint ts_probe;                // timestamp probe
        private uint probe_wait;
        private uint incr;
        private uint current;                 // current time (milliseconds). set by Update.

        private int fastresend;
        private readonly int fastlimit = 5; // max times to trigger fastack
        private bool nocwnd;
        private readonly Queue<Segment> snd_queue = new Queue<Segment>(16); // send queue
        private readonly Queue<Segment> rcv_queue = new Queue<Segment>(16); // receive queue
        // snd_buffer needs index removals.
        // C# LinkedList allocates for each entry, so let's keep List for now.
        private readonly List<Segment> snd_buf = new List<Segment>(16);   // send buffer
        // rcv_buffer needs index insertions and backwards iteration.
        // C# LinkedList allocates for each entry, so let's keep List for now.
        private readonly List<Segment> rcv_buf = new List<Segment>(16);   // receive buffer
        private readonly List<AckItem> acklist = new List<AckItem>(16);

        private byte[] buffer;
        readonly Action<byte[], int> output; // buffer, size

        // get how many packet is waiting to be sent
        public int WaitSnd => snd_buf.Count + snd_queue.Count;

        // ikcp_create
        /// <summary>
        ///  create a new kcp control object, 'conv' must equal in two endpoint
        ///  from the same connection.
        /// </summary>
        /// <param name="conv">a number that must match between two endpoints</param>
        /// <param name="output">a delegate to use when sending data</param>
        public Kcp(uint conv, Action<byte[], int> output)
        {
            this.conv = conv;
            this.output = output;
            buffer = new byte[(mtu + OVERHEAD) * 3];
        }

        // ikcp_recv
        /// <summary>
        /// receive data from kcp state machine
        /// </summary>
        /// <param name="buffer">buffer where the data will be stored</param>
        /// <param name="len">size of the buffer</param>
        /// <returns>number of read bytes</returns>
        public int Receive(byte[] buffer)
        {
            // kcp's ispeek feature is not supported.
            // this makes 'merge fragment' code significantly easier because
            // we can iterate while queue.Count > 0 and dequeue each time.
            // if we had to consider ispeek then count would always be > 0 and
            // we would have to remove only after the loop.
            if (rcv_queue.Count == 0)
                return -1;

            int peeksize = PeekSize();

            if (peeksize < 0)
                return -2;

            if (peeksize > buffer.Length)
                return -3;

            bool recover = rcv_queue.Count >= rcv_wnd;

            int len = DequeueMessage(buffer);

            ReceiveBufferToReceiveQueue();

            // fast recover
            if (rcv_queue.Count < rcv_wnd && recover)
            {
                // ready to send back CMD_WINS in flush
                // tell remote my window size
                probe |= ASK_TELL;
            }

            return len;
        }

        private int DequeueMessage(byte[] buffer)
        {
            // merge fragment.
            int dequeueOffset = 0;
            int len = 0;
            // original KCP iterates rcv_queue and deletes if !ispeek.
            // removing from a c# queue while iterating is not possible, but
            // we can change to 'while Count > 0' and remove every time.
            // (we can remove every time because we removed ispeek support!)
            while (rcv_queue.Count > 0)
            {
                // unlike original kcp, we dequeue instead of just getting the
                // entry. this is fine because we remove it in ANY case.
                Segment seg = rcv_queue.Dequeue();

                seg.data.Position = 0;
                seg.data.Read(buffer, dequeueOffset, (int)seg.data.Length);
                dequeueOffset += (int)seg.data.Length;

                len += (int)seg.data.Length;
                uint fragment = seg.fragment;

                // note: ispeek is not supported in order to simplify this loop

                // unlike original kcp, we don't need to remove seg from queue
                // because we already dequeued it.
                // simply delete it
                Segment.Release(seg);

                if (fragment == 0)
                    break;
            }

            return len;
        }

        // ikcp_peeksize
        // check the size of next message in the recv queue
        public int PeekSize()
        {
            int length = 0;

            if (rcv_queue.Count == 0)
                return -1;

            Segment seq = rcv_queue.Peek();
            if (seq.fragment == 0)
                return (int)seq.data.Length;

            if (rcv_queue.Count < seq.fragment + 1)
                return -1;

            foreach (Segment seg in rcv_queue)
            {
                length += (int)seg.data.Length;
                if (seg.fragment == 0)
                    break;
            }

            return length;
        }

        // ikcp_send
        // sends byte[] to the other end.
        public void Send(byte[] buffer, int offset, int length)
        {
            if (length <= 0)
                throw new ArgumentException($"You cannot send a packet with a {nameof(length)} of 0.");

            // streaming mode: removed. we never want to send 'hello' and
            // receive 'he' 'll' 'o'. we want to always receive 'hello'.

            int count;
            if (length <= Mss)
                count = 1;
            else
                count = (int)((length + Mss - 1) / Mss);

            if (count >= rcv_wnd)
                throw new ArgumentException($"Your packet is too big and doesn't fit the receive window, either reduce its {nameof(length)}, call {nameof(SetWindowSize)} to increase the window or increase the {nameof(Mtu)}.");

            if (count == 0)
                count = 1;

            // fragment
            for (int i = 0; i < count; i++)
            {
                int size = Math.Min(length, (int)Mss);
                var seg = Segment.Lease();

                seg.data.Write(buffer, offset, size);

                // seg.len = size: WriteBytes sets segment.Position!
                seg.fragment = (byte)(count - i - 1);
                snd_queue.Enqueue(seg);
                offset += size;
                length -= size;
            }
        }

        // ikcp_update_ack
        void UpdateAck(uint ts) // round trip time
        {
            if (current < ts)
                return;

            int rtt = (int)(current - ts);

            // https://tools.ietf.org/html/rfc6298
            if (rx_srtt == 0)
            {
                rx_srtt = rtt;
                rx_rttval = rtt / 2;
            }
            else
            {
                int delta = rtt - rx_srtt;
                if (delta < 0)
                    delta = -delta;
                rx_rttval = (3 * rx_rttval + delta) / 4;
                rx_srtt = (7 * rx_srtt + rtt) / 8;
                if (rx_srtt < 1)
                    rx_srtt = 1;
            }
            int rto = rx_srtt + Math.Max((int)interval, 4 * rx_rttval);
            rx_rto = Mathf.Clamp(rto, rx_minrto, RTO_MAX);
        }

        // ikcp_shrink_buf
        private void ShrinkBuf()
        {
            if (snd_buf.Count > 0)
            {
                Segment seg = snd_buf[0];
                snd_una = seg.serialNumber;
            }
            else
            {
                snd_una = snd_nxt;
            }
        }

        // ikcp_parse_ack
        // removes the segment with 'sn' from send buffer
        private void ParseAck(uint sn)
        {
            if (sn < snd_una || sn >= snd_nxt)
                return;

            // for-int so we can erase while iterating
            for (int i = 0; i < snd_buf.Count; ++i)
            {
                Segment seg = snd_buf[i];
                if (sn == seg.serialNumber)
                {
                    snd_buf.RemoveAt(i);
                    Segment.Release(seg);
                    break;
                }
                if (sn < seg.serialNumber)
                {
                    break;
                }
            }
        }

        // ikcp_parse_una
        void ParseUna(uint una)
        {
            int removed = 0;
            foreach (Segment seg in snd_buf)
            {
                if (una > seg.serialNumber)
                {
                    // can't remove while iterating. remember how many to remove
                    // and do it after the loop.
                    ++removed;
                    Segment.Release(seg);
                }
                else
                {
                    break;
                }
            }
            snd_buf.RemoveRange(0, removed);
        }

        // ikcp_parse_fastack
        void ParseFastack(uint sn)
        {
            if (sn < snd_una || sn >= snd_nxt)
                return;

            foreach (Segment seg in snd_buf)
            {
                if (sn <= seg.serialNumber)
                    break;

                seg.fastack++;
            }
        }

        // ikcp_ack_push
        // appends an ack.
        void AckPush(uint sn, uint ts)
        {
            acklist.Add(new AckItem { serialNumber = sn, timestamp = ts });
        }

        // ikcp_parse_data
        void ParseData(Segment newseg)
        {
            uint sn = newseg.serialNumber;

            if (sn >= rcv_nxt + rcv_wnd || sn < rcv_nxt)
            {
                Segment.Release(newseg);
                return;
            }

            InsertSegmentInReceiveBuffer(newseg);
            ReceiveBufferToReceiveQueue();
        }

        // inserts the segment into rcv_buf, ordered by seg.sn.
        // drops the segment if one with the same seg.sn already exists.
        // goes through receive buffer in reverse order for performance.
        //
        // note: see KcpTests.InsertSegmentInReceiveBuffer test!
        // note: 'insert or delete' can be done in different ways, but let's
        //       keep consistency with original C kcp.
        private void InsertSegmentInReceiveBuffer(Segment newseg)
        {
            // original C iterates backwards, so we need to do that as well.
            int i;
            for (i = rcv_buf.Count - 1; i >= 0; i--)
            {
                Segment seg = rcv_buf[i];
                if (seg.serialNumber == newseg.serialNumber)
                {
                    // duplicate segment found. nothing will be added.
                    Segment.Release(newseg);
                    return;
                }
                if (newseg.serialNumber > seg.serialNumber)
                {
                    // this entry's sn is < newseg.sn, so let's stop
                    break;
                }
            }

            rcv_buf.Insert(i + 1, newseg);
        }

        // move available data from rcv_buf -> rcv_queue
        void ReceiveBufferToReceiveQueue()
        {
            int removed = 0;
            foreach (Segment seg in rcv_buf)
            {
                if (seg.serialNumber == rcv_nxt && rcv_queue.Count < rcv_wnd)
                {
                    // can't remove while iterating. remember how many to remove
                    // and do it after the loop.
                    ++removed;
                    rcv_queue.Enqueue(seg);
                    rcv_nxt++;
                }
                else
                {
                    break;
                }
            }
            rcv_buf.RemoveRange(0, removed);
        }

        // ikcp_input
        /// used when you receive a low level packet (eg. UDP packet)
        public int Input(byte[] data, int size)
        {
            uint prev_una = snd_una;
            uint maxack = 0;
            bool flag = false;

            // the data is expected to have the reserved space
            size -= Reserved;
            if (size < OVERHEAD)
                return -1;

            int reservedOffset = Reserved;

            while (size >= OVERHEAD)
            {
                var decoder = new Decoder(data, reservedOffset);
                uint conv_ = decoder.Decode32U();
                var cmd = (CommandType)decoder.Decode8U();
                byte frg = decoder.Decode8U();
                ushort wnd = decoder.Decode16U();
                uint ts = decoder.Decode32U();
                uint sn = decoder.Decode32U();
                uint una = decoder.Decode32U();
                int len = (int)decoder.Decode32U();

                reservedOffset = decoder.Position;
                size -= OVERHEAD;

                if (!ValidateSegment(size, conv_, cmd, len))
                    return -1;

                rmt_wnd = wnd;
                ParseUna(una);
                ShrinkBuf();

                switch (cmd)
                {
                    case CommandType.Ack:
                        UpdateAck(ts);
                        ParseAck(sn);
                        ShrinkBuf();
                        maxack = Math.Max(maxack, sn);
                        flag = true;
                        break;
                    case CommandType.Push:
                        if (sn >= rcv_nxt + rcv_wnd)
                            break;

                        AckPush(sn, ts);
                        if (sn < rcv_nxt)
                            break;

                        var seg = Segment.Lease();
                        seg.conversation = conv_;
                        seg.cmd = cmd;
                        seg.fragment = frg;
                        seg.window = wnd;
                        seg.timeStamp = ts;
                        seg.serialNumber = sn;
                        seg.unacknowledged = una;
                        seg.data.Write(data, reservedOffset, len);
                        ParseData(seg);
                        break;
                    case CommandType.WindowAsk:

                        // ready to send back CMD_WINS in flush
                        // tell remote my window size
                        probe |= ASK_TELL;
                        break;
                }

                reservedOffset += len;
                size -= len;
            }

            if (flag)
            {
                ParseFastack(maxack);
            }

            // cwnd update when packet arrived
            UpdateCongestionWindow(prev_una);

            return 0;
        }

        private bool ValidateSegment(int size, uint conv_, CommandType cmd, int len)
        {
            if (conv_ != conv)
                return false;

            if (size < len || len < 0)
                return false;

            switch (cmd)
            {
                case CommandType.Ack:
                case CommandType.Push:
                case CommandType.WindowAsk:
                case CommandType.WindowTell:
                    break;
                default:
                    return false;
            }

            return true;
        }

        private void UpdateCongestionWindow(uint prev_una)
        {
            if (snd_una <= prev_una || cwnd >= rmt_wnd)            
                return;

            if (cwnd < ssthresh)
            {
                cwnd++;
                incr += Mss;
            }
            else
            {
                if (incr < Mss)
                    incr = Mss;
                incr += Mss * Mss / incr + (Mss / 16);
                if ((cwnd + 1) * Mss <= incr)
                {
                    cwnd = (incr + Mss - 1) / ((Mss > 0) ? Mss : 1);
                }
            }
            if (cwnd > rmt_wnd)
            {
                cwnd = rmt_wnd;
                incr = rmt_wnd * Mss;
            }
        }

        // ikcp_wnd_unused
        uint WndUnused() => Math.Max(rcv_wnd - (uint)rcv_queue.Count, 0);

        int offset;

        // helper functions
        void MakeSpace(int space)
        {
            if (offset + space > mtu)
            {
                // we can't fit that space in the buffer
                // so send the current buffer
                // and start a new one
                // leave space for the reserved bytes
                output(buffer, offset);
                offset = Reserved;
            }
        }

        // ikcp_flush
        // flush remain ack segments
        public void Flush()
        {
            // buffer ptr in original C
            // unlike the C version we leave a little room at the beginning
            // of the buffer for the reserved bytes
            offset = Reserved;

            // 'ikcp_update' haven't been called.
            if (!updated)
                return;

            // kcp only stack allocs a segment here for performance, leaving
            // its data buffer null because this segment's data buffer is never
            // used. that's fine in C, but in C# our segment is class so we need
            // to allocate and most importantly, not forget to deallocate it
            // before returning.
            var seg = Segment.Lease();
            seg.conversation = conv;
            seg.cmd = CommandType.Ack;
            seg.window = WndUnused();
            seg.unacknowledged = rcv_nxt;

            // flush acknowledges
            FlushAcknowledge(seg);

            // probe window size (if remote window size equals zero)
            FlushProbe(seg);

            uint cwnd_ = CalculateWindowSize();

            // move data from send queue to send buffer
            SendQueueToSendBuffer(seg.window, cwnd_);

            FlushDataSegments(seg.window, cwnd_);

            if (cwnd < 1)
            {
                cwnd = 1;
                incr = Mss;
            }

            // flash remain segments
            if (offset > Reserved)
            {
                output(buffer, offset);
            }

            // kcp stackallocs 'seg'. our C# segment is a class though, so we
            // need to properly delete and return it to the pool now that we are
            // done with it.
            Segment.Release(seg);
        }

        private void FlushDataSegments(uint window, uint cwnd_)
        {
            // calculate resent
            uint resent = fastresend > 0 ? (uint)fastresend : uint.MaxValue;
            uint rtomin = nodelay ? 0 : (uint)rx_rto >> 3;

            // flush data segments
            bool change = false;
            bool lost = false; // lost segments
            foreach (Segment segment in snd_buf)
            {
                bool needsend = false;
                // initial transmit
                if (segment.transmit == 0)
                {
                    needsend = true;
                    segment.transmit++;
                    segment.rto = rx_rto;
                    segment.resendTimeStamp = current + (uint)segment.rto + rtomin;
                }
                // RTO
                else if (current >= segment.resendTimeStamp)
                {
                    needsend = true;
                    segment.transmit++;
                    segment.rto = ResendRto(segment.rto);
                    segment.resendTimeStamp = current + (uint)segment.rto;
                    lost = true;
                }
                // fast retransmit
                else if (segment.fastack >= resent && (segment.transmit <= fastlimit || fastlimit <= 0))
                {
                    needsend = true;
                    segment.transmit++;
                    segment.fastack = 0;
                    segment.resendTimeStamp = current + (uint)segment.rto;
                    change = true;
                }

                if (needsend)
                {
                    segment.timeStamp = current;
                    segment.window = window;
                    segment.unacknowledged = rcv_nxt;

                    int need = (int)(OVERHEAD + segment.data.Length);
                    MakeSpace(need);

                    offset = segment.Encode(buffer, offset);

                    segment.data.Position = 0;
                    segment.data.Read(buffer, offset, (int)segment.data.Length);
                    offset += (int)segment.data.Length;
                }
            }

            // update ssthresh
            // rate halving, https://tools.ietf.org/html/rfc6937
            if (change)
            {
                uint inflight = snd_nxt - snd_una;
                ssthresh = Math.Max(inflight / 2, THRESH_MIN);
                cwnd = ssthresh + resent;
                incr = cwnd * Mss;
            }

            // congestion control, https://tools.ietf.org/html/rfc5681
            if (lost)
            {
                // original C uses 'cwnd', not kcp->cwnd!
                ssthresh = Math.Max(cwnd_ / 2, THRESH_MIN);
                cwnd = 1;
                incr = Mss;
            }
        }

        private uint CalculateWindowSize()
        {
            uint cwnd_ = Math.Min(snd_wnd, rmt_wnd);
            if (!nocwnd)
                cwnd_ = Math.Min(cwnd, cwnd_);
            return cwnd_;
        }

        private int ResendRto(int rto)
        {
            if (nodelay)
            {
                return rto + rto / 2;
            }
            else
            {
                return rto + Math.Max(rto, rx_rto);
            }
        }

        private void SendQueueToSendBuffer(uint window, uint cwnd_)
        {
            // move data from snd_queue to snd_buf
            // sliding window, controlled by snd_nxt && sna_una+cwnd
            while (snd_nxt < snd_una + cwnd_)
            {
                if (snd_queue.Count == 0)
                    break;

                Segment newseg = snd_queue.Dequeue();

                newseg.conversation = conv;
                newseg.cmd = CommandType.Push;
                newseg.window = window;
                newseg.timeStamp = current;
                newseg.serialNumber = snd_nxt++;
                newseg.unacknowledged = rcv_nxt;
                newseg.resendTimeStamp = current;
                newseg.rto = rx_rto;
                newseg.fastack = 0;
                newseg.transmit = 0;
                snd_buf.Add(newseg);
            }
        }

        private void FlushProbe(Segment seg)
        {
            if (rmt_wnd == 0)
            {
                if (probe_wait == 0)
                {
                    probe_wait = PROBE_INIT;
                    ts_probe = current + probe_wait;
                }
                else if (current >= ts_probe)
                {
                    if (probe_wait < PROBE_INIT)
                        probe_wait = PROBE_INIT;
                    probe_wait += probe_wait / 2;
                    if (probe_wait > PROBE_LIMIT)
                        probe_wait = PROBE_LIMIT;
                    ts_probe = current + probe_wait;
                    probe |= ASK_SEND;
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
                seg.cmd = CommandType.WindowAsk;
                MakeSpace(OVERHEAD);
                offset = seg.Encode(buffer, offset);
            }

            // flush window probing commands
            if ((probe & ASK_TELL) != 0)
            {
                seg.cmd = CommandType.WindowTell;
                MakeSpace(OVERHEAD);
                offset = seg.Encode(buffer, offset);
            }

            probe = 0;
        }

        private void FlushAcknowledge(Segment seg)
        {
            foreach (AckItem ack in acklist)
            {
                MakeSpace(OVERHEAD);
                // ikcp_ack_get assigns ack[i] to seg.sn, seg.ts
                seg.serialNumber = ack.serialNumber;
                seg.timeStamp = ack.timestamp;
                offset = seg.Encode(buffer, offset);
            }

            acklist.Clear();
        }

        // ikcp_update
        // update state (call it repeatedly, every 10ms-100ms), or you can ask
        // Check() when to call it again (without Input/Send calling).
        //
        // 'current' - current timestamp in millisec. pass it to Kcp so that
        // Kcp doesn't have to do any stopwatch/deltaTime/etc. code
        public void Update(uint currentTimeMilliSeconds)
        {
            current = currentTimeMilliSeconds;

            if (!updated)
            {
                updated = true;
                ts_flush = current;
            }

            int slap = (int)(current - ts_flush);

            if (slap >= 10000 || slap < -10000)
            {
                ts_flush = current;
                slap = 0;
            }

            if (slap >= 0)
            {
                ts_flush += interval;
                if (current >= ts_flush)
                {
                    ts_flush = current + interval;
                }
                Flush();
            }
        }

        // ikcp_check
        // Determine when should you invoke update
        // Returns when you should invoke update in millisec, if there is no
        // input/send calling. you can call update in that time, instead of
        // call update repeatly.
        //
        // Important to reduce unnecessary update invoking. use it to schedule
        // update (eg. implementing an epoll-like mechanism, or optimize update
        // when handling massive kcp connections).
        public uint Check(uint current_)
        {
            uint ts_flush_ = ts_flush;
            int tm_packet = 0x7fffffff;

            if (!updated)
                return current_;

            if ((current_ - ts_flush_) >= 10000)
                ts_flush_ = current_;

            if (current_ >= ts_flush_)
                return current_;

            int tm_flush = (int)(ts_flush_ - current_);

            foreach (Segment seg in snd_buf)
            {
                int diff = (int)(seg.resendTimeStamp - current_);
                if (diff <= 0)
                    return current_;
                if (diff < tm_packet)
                    tm_packet = diff;
            }

            uint minimal = (uint)Math.Min(tm_packet, tm_flush);
            minimal = Math.Min(minimal, interval);

            return current_ + minimal;
        }

        // ikcp_setmtu
        public uint Mtu
        {
            get => mtu;
            set
            {
                if (value < 50 || value < OVERHEAD + Reserved)
                    throw new ArgumentException("MTU must be higher than 50 and higher than OVERHEAD");

                if (value > ushort.MaxValue)
                    throw new ArgumentException("MTU must be lower than " + ushort.MaxValue);

                buffer = new byte[(value + OVERHEAD) * 3];
                mtu = value;
            }
        }

        // ikcp_interval
        public void SetInterval(int interval)
        {
            this.interval = (uint)Utils.Clamp(interval, 10, 5000);
        }

        // ikcp_nodelay
        //   Normal: 0, 40, 0, 0
        //   Fast:   0, 30, 2, 1
        //   Fast2:  1, 20, 2, 1
        //   Fast3:  1, 10, 2, 1
        public void SetNoDelay(bool nodelay = false, int interval = INTERVAL, int resend = 0, bool nocwnd = false)
        {
            this.nodelay = nodelay;

            rx_minrto = nodelay ? RTO_NDL : RTO_MIN;

            this.interval = (uint)Utils.Clamp(interval, 10, 5000);

            if (resend >= 0)
                fastresend = resend;

            this.nocwnd = nocwnd;
        }

        /// <summary>
        /// Convenience method to configure KCP for well known configurations
        /// </summary>
        /// <param name="mode"></param>
        public void SetNoDelay(KcpDelayMode mode)
        {
            switch(mode)
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

        // ikcp_wndsize
        public void SetWindowSize(uint sendWindow, uint receiveWindow)
        {
            if (sendWindow > 0)
            {
                snd_wnd = sendWindow;
            }

            // must >= max fragment size
            rcv_wnd = Math.Max(receiveWindow, WND_RCV);
        }
    }
}
