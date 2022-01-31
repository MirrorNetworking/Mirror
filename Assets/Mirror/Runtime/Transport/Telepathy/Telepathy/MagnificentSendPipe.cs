// a magnificent send pipe to shield us from all of life's complexities.
// safely sends messages from main thread to send thread.
// -> thread safety built in
// -> byte[] pooling coming in the future
//
// => hides all the complexity from telepathy
// => easy to switch between stack/queue/concurrentqueue/etc.
// => easy to test

using System;
using System.Collections.Generic;

namespace Telepathy
{
    public class MagnificentSendPipe
    {
        // message queue
        // ConcurrentQueue allocates. lock{} instead.
        // -> byte arrays are always of MaxMessageSize
        // -> ArraySegment indicates the actual message content
        //
        // IMPORTANT: lock{} all usages!
        readonly Queue<ArraySegment<byte>> queue = new Queue<ArraySegment<byte>>();

        // byte[] pool to avoid allocations
        // Take & Return is beautifully encapsulated in the pipe.
        // the outside does not need to worry about anything.
        // and it can be tested easily.
        //
        // IMPORTANT: lock{} all usages!
        Pool<byte[]> pool;

        // constructor
        public MagnificentSendPipe(int MaxMessageSize)
        {
            // initialize pool to create max message sized byte[]s each time
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }

        // for statistics. don't call Count and assume that it's the same after
        // the call.
        public int Count
        {
            get { lock (this) { return queue.Count; } }
        }

        // pool count for testing
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }

        // enqueue a message
        // arraysegment for allocation free sends later.
        // -> the segment's array is only used until Enqueue() returns!
        public void Enqueue(ArraySegment<byte> message)
        {
            // pool & queue usage always needs to be locked
            lock (this)
            {
                // ArraySegment array is only valid until returning, so copy
                // it into a byte[] that we can queue safely.

                // get one from the pool first to avoid allocations
                byte[] bytes = pool.Take();

                // copy into it
                Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                // indicate which part is the message
                ArraySegment<byte> segment = new ArraySegment<byte>(bytes, 0, message.Count);

                // now enqueue it
                queue.Enqueue(segment);
            }
        }

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
                    pool.Return(message.Array);
                }

                // we did serialize something
                return true;
            }
        }

        public void Clear()
        {
            // pool & queue usage always needs to be locked
            lock (this)
            {
                // clear queue, but via dequeue to return each byte[] to pool
                while (queue.Count > 0)
                {
                    pool.Return(queue.Dequeue().Array);
                }
            }
        }
    }
}