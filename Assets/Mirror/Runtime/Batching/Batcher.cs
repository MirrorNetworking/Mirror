// batching functionality encapsulated into one class.
// -> less complexity
// -> easy to test
using System;
using System.Collections.Generic;

namespace Mirror
{
    public class Batcher
    {
        // max batch size
        readonly int MaxBatchSize;

        // batched messages
        // IMPORTANT: we queue the serialized messages!
        //            queueing NetworkMessage would box and allocate!
        Queue<PooledNetworkWriter> messages = new Queue<PooledNetworkWriter>();

        public Batcher(int MaxBatchSize)
        {
            this.MaxBatchSize = MaxBatchSize;
        }

        // add a message for batching
        // -> true if it worked.
        // -> false if too big for max.
        // => true/false instead of exception because the user might try to send
        //    a gigantic message once. which is fine. but we won't batch it.
        public bool AddMessage(ArraySegment<byte> message)
        {
            // make sure the message can fit into max batch size
            if (message.Count > MaxBatchSize)
                return false;

            // put into a (pooled) writer
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            // -> will be returned to pool when making the batch!
            // IMPORTANT: NOT adding a size header / msg saves LOTS of bandwidth
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(message.Array, message.Offset, message.Count);
            messages.Enqueue(writer);
            return true;
        }

        // batch as many messages as possible into writer
        // returns true if any batch was made.
        public bool MakeNextBatch(NetworkWriter writer)
        {
            // if we have no messages then there's nothing to do
            if (messages.Count == 0)
                return false;

            // make sure the writer is fresh to avoid uncertain situations
            if (writer.Position != 0)
                throw new ArgumentException($"MakeNextBatch needs a fresh writer!");

            // for each queued message
            while (messages.Count > 0)
            {
                // peek and see if it still fits
                PooledNetworkWriter message = messages.Peek();
                ArraySegment<byte> segment = message.ToArraySegment();

                // still fits?
                if (writer.Position + segment.Count <= MaxBatchSize)
                {
                    // add it
                    // (without any size prefixes. we can fit exactly segment.count!)
                    writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

                    // eat it & return to pool
                    messages.Dequeue();
                    NetworkWriterPool.Recycle(message);
                }
                // doesn't fit. this batch is done
                else break;
            }

            // we had messages, so a batch was made
            return true;
        }
    }
}
