using System;

namespace Telepathy
{
    public class MagnificentSendPipe : MagnificentPipe
    {
        // constructor
        public MagnificentSendPipe(int MaxMessageSize) : base(MaxMessageSize) {}

        // send threads need to dequeue each byte[] and write it into the socket
        // -> dequeueing one byte[] after another works, but it's WAY slower
        //    than dequeueing all immediately (locks only once)
        //    lock{} & DequeueAll is WAY faster than ConcurrentQueue & dequeue
        //    one after another:
        //
        //      uMMORPG 450 CCU
        //        SafeQueue:       900-1440ms latency
        //        ConcurrentQueue:     2000ms latency
        //
        // -> the most obvious solution is to just return a list with all byte[]
        //    (which allocates) and then write each one into the socket
        // -> a faster solution is to serialize each one into one payload buffer
        //    and pass that to the socket only once. fewer socket calls always
        //    give WAY better CPU performance(!)
        // -> to avoid allocating a new list of entries each time, we simply
        //    serialize all entries into the payload here already
        // => having all this complexity built into the pipe makes testing and
        //    modifying the algorithm super easy!
        //
        // IMPORTANT: serializing in here will allow us to return the byte[]
        //            entries back to a pool later to completely avoid
        //            allocations!
        public bool DequeueAndSerializeAll(ref byte[] payload, out int packetSize)
        {
            // pool & queue usage always needs to be locked
            lock (this)
            {
                // do nothing if empty
                packetSize = 0;
                if (queue.Count == 0)
                    return false;

                // we might have multiple pending messages. merge into one
                // packet to avoid TCP overheads and improve performance.
                //
                // IMPORTANT: Mirror & DOTSNET already batch into MaxMessageSize
                //            chunks, but we STILL pack all pending messages
                //            into one large payload so we only give it to TCP
                //            ONCE. This is HUGE for performance so we keep it!
                packetSize = 0;
                foreach (ArraySegment<byte> message in queue)
                    packetSize += 4 + message.Count; // header + content

                // create payload buffer if not created yet or previous one is
                // too small
                // IMPORTANT: payload.Length might be > packetSize! don't use it!
                if (payload == null || payload.Length < packetSize)
                    payload = new byte[packetSize];

                // dequeue all byte[] messages and serialize into the packet
                int position = 0;
                while (queue.Count > 0)
                {
                    // dequeue
                    ArraySegment<byte> message = queue.Dequeue();

                    // write header (size) into buffer at position
                    Utils.IntToBytesBigEndianNonAlloc(message.Count, payload, position);
                    position += 4;

                    // copy message into payload at position
                    Buffer.BlockCopy(message.Array, message.Offset, payload, position, message.Count);
                    position += message.Count;

                    // return to pool so it can be reused (avoids allocations!)
                    pool.Push(message.Array);
                }

                // we did serialize something
                return true;
            }
        }
    }
}