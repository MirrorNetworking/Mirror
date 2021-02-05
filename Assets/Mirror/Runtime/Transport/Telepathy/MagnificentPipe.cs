// a magnificent  pipe to shield us from all of life's complexities.
// common class to safely send/recv messages between threads.
// -> thread safety built in
// -> byte[] pooling coming in the future
//
// => hides all the complexity from telepathy
// => easy to switch between stack/queue/concurrentqueue/etc.
// => easy to test
using System;
using System.Collections.Concurrent;

namespace Telepathy
{
    public abstract class MagnificentPipe
    {
        // message queue
        // => ConcurrentQueue allocates.
        // => lock{} doesn't, but locking with 150 CCU/threads is way slower!
        protected readonly ConcurrentQueue<ArraySegment<byte>> queue =
            new ConcurrentQueue<ArraySegment<byte>>();

        // max message size for pooling
        readonly int MaxMessageSize;

        // byte[] pool to avoid allocations
        // Take & Return is beautifully encapsulated in the pipe.
        // the outside does not need to worry about anything.
        // and it can be tested easily.
        //
        // => ConcurrentStack allocates.
        // => lock{} doesn't, but locking with 150 CCU/threads is way slower!
        protected readonly ConcurrentStack<byte[]> pool = new ConcurrentStack<byte[]>();

        // constructor
        protected MagnificentPipe(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }

        // for statistics. don't call Count and assume that it's the same after
        // the call.
        public int Count => queue.Count;

        // pool count for testing
        public int PoolCount => pool.Count;

        // enqueue a message
        // arraysegment for allocation free sends later.
        // -> the segment's array is only used until Enqueue() returns!
        public void Enqueue(ArraySegment<byte> message)
        {
            // ArraySegment array is only valid until returning, so copy
            // it into a byte[] that we can queue safely.

            // to avoid allocations, try to get a byte[] from the pool first
            byte[] bytes;
            if (!pool.TryPop(out bytes))
                bytes = new byte[MaxMessageSize];

            // copy into it
            Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

            // indicate which part is the message
            ArraySegment<byte> segment = new ArraySegment<byte>(bytes, 0, message.Count);

            // now enqueue it
            queue.Enqueue(segment);
        }

        // peek the next message
        // -> allows the caller to process it while pipe still holds on to the
        //    byte[]
        // -> TryDequeue should be called after processing, so that the message
        //    is actually dequeued and the byte[] is returned to pool!
        // => see TryDequeue comments!
        //
        // IMPORTANT: TryPeek & Dequeue need to be called from the SAME THREAD!
        public bool TryPeek(out ArraySegment<byte> data) => queue.TryPeek(out data);

        // dequeue the next message
        // -> simply dequeues and returns the byte[] to pool (if any)
        // -> use Peek to actually process the first element while the pipe
        //    still holds on to the byte[]
        // -> doesn't return the element because the byte[] needs to be returned
        //    to the pool in dequeue. caller can't be allowed to work with a
        //    byte[] that is already returned to pool.
        // => Peek & Dequeue is the most simple, clean solution for receive
        //    pipe pooling to avoid allocations!
        //
        // IMPORTANT: TryPeek & Dequeue need to be called from the SAME THREAD!
        public bool TryDequeue()
        {
            // dequeue and return byte[] to pool
            if (queue.TryDequeue(out ArraySegment<byte> segment))
            {
                pool.Push(segment.Array);
                return true;
            }
            return false;
        }

        public virtual void Clear()
        {
            while (queue.TryDequeue(out ArraySegment<byte> segment))
                pool.Push(segment.Array);
        }
    }
}