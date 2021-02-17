// a magnificent receive pipe to shield us from all of life's complexities.
// safely sends messages from receive thread to main thread.
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
    public class MagnificentReceivePipe
    {
        // queue entry message. only used in here.
        // -> byte arrays are always of 4 + MaxMessageSize
        // -> ArraySegment indicates the actual message content
        struct Entry
        {
            public int connectionId;
            public EventType eventType;
            public ArraySegment<byte> data;
            public Entry(int connectionId, EventType eventType, ArraySegment<byte> data)
            {
                this.connectionId = connectionId;
                this.eventType = eventType;
                this.data = data;
            }
        }

        // message queue
        // ConcurrentQueue allocates. lock{} instead.
        //
        // IMPORTANT: lock{} all usages!
        readonly Queue<Entry> queue = new Queue<Entry>();

        // byte[] pool to avoid allocations
        // Take & Return is beautifully encapsulated in the pipe.
        // the outside does not need to worry about anything.
        // and it can be tested easily.
        //
        // IMPORTANT: lock{} all usages!
        Pool<byte[]> pool;

        // unfortunately having one receive pipe per connetionId is way slower
        // in CCU tests. right now we have one pipe for all connections.
        // => we still need to limit queued messages per connection to avoid one
        //    spamming connection being able to slow down everyone else since
        //    the queue would be full of just this connection's messages forever
        // => let's use a simpler per-connectionId counter for now
        Dictionary<int, int> queueCounter = new Dictionary<int, int>();

        // constructor
        public MagnificentReceivePipe(int MaxMessageSize)
        {
            // initialize pool to create max message sized byte[]s each time
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }

        // return amount of queued messages for this connectionId.
        // for statistics. don't call Count and assume that it's the same after
        // the call.
        public int Count(int connectionId)
        {
            lock (this)
            {
                return queueCounter.TryGetValue(connectionId, out int count)
                       ? count
                       : 0;
            }
        }

        // total count
        public int TotalCount
        {
            get { lock (this) { return queue.Count; } }
        }

        // pool count for testing
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }

        // enqueue a message
        // -> ArraySegment to avoid allocations later
        // -> parameters passed directly so it's more obvious that we don't just
        //    queue a passed 'Message', instead we copy the ArraySegment into
        //    a byte[] and store it internally, etc.)
        public void Enqueue(int connectionId, EventType eventType, ArraySegment<byte> message)
        {
            // pool & queue usage always needs to be locked
            lock (this)
            {
                // does this message have a data array content?
                ArraySegment<byte> segment = default;
                if (message != default)
                {
                    // ArraySegment is only valid until returning.
                    // copy it into a byte[] that we can store.
                    // ArraySegment array is only valid until returning, so copy
                    // it into a byte[] that we can queue safely.

                    // get one from the pool first to avoid allocations
                    byte[] bytes = pool.Take();

                    // copy into it
                    Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                    // indicate which part is the message
                    segment = new ArraySegment<byte>(bytes, 0, message.Count);
                }

                // enqueue it
                // IMPORTANT: pass the segment around pool byte[],
                //            NOT the 'message' that is only valid until returning!
                Entry entry = new Entry(connectionId, eventType, segment);
                queue.Enqueue(entry);

                // increase counter for this connectionId
                int oldCount = Count(connectionId);
                queueCounter[connectionId] = oldCount + 1;
            }
        }

        // peek the next message
        // -> allows the caller to process it while pipe still holds on to the
        //    byte[]
        // -> TryDequeue should be called after processing, so that the message
        //    is actually dequeued and the byte[] is returned to pool!
        // => see TryDequeue comments!
        //
        // IMPORTANT: TryPeek & Dequeue need to be called from the SAME THREAD!
        public bool TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> data)
        {
            connectionId = 0;
            eventType = EventType.Disconnected;
            data = default;

            // pool & queue usage always needs to be locked
            lock (this)
            {
                if (queue.Count > 0)
                {
                    Entry entry = queue.Peek();
                    connectionId = entry.connectionId;
                    eventType = entry.eventType;
                    data = entry.data;
                    return true;
                }
                return false;
            }
        }

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
            // pool & queue usage always needs to be locked
            lock (this)
            {
                if (queue.Count > 0)
                {
                    // dequeue from queue
                    Entry entry = queue.Dequeue();

                    // return byte[] to pool (if any).
                    // not all message types have byte[] contents.
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }

                    // decrease counter for this connectionId
                    queueCounter[entry.connectionId]--;

                    // remove if zero. don't want to keep old connectionIds in
                    // there forever, it would cause slowly growing memory.
                    if (queueCounter[entry.connectionId] == 0)
                        queueCounter.Remove(entry.connectionId);

                    return true;
                }
                return false;
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
                    // dequeue
                    Entry entry = queue.Dequeue();

                    // return byte[] to pool (if any).
                    // not all message types have byte[] contents.
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }
                }

                // clear counter too
                queueCounter.Clear();
            }
        }
    }
}