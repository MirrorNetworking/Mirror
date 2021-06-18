// batching functionality encapsulated into one class.
// -> less complexity
// -> easy to test
//
// IMPORTANT: we use THRESHOLD batching, not MAXED SIZE batching.
// see threshold comments below.
//
// includes timestamp for tick batching.
// -> allows NetworkTransform etc. to use timestamp without including it in
//    every single message
using System;
using System.Collections.Generic;

namespace Mirror
{
    public class Batcher
    {
        // batching threshold instead of max size.
        // -> small messages are fit into threshold sized batches
        // -> messages larger than threshold are single batches
        //
        // in other words, we fit up to 'threshold' but still allow larger ones
        // for two reasons:
        // 1.) data races: skipping batching for larger messages would send a
        //     large spawn message immediately, while others are batched and
        //     only flushed at the end of the frame
        // 2) timestamp batching: if each batch is expected to contain a
        //    timestamp, then large messages have to be a batch too. otherwise
        //    they would not contain a timestamp
        readonly int threshold;

        // TimeStamp header size for those who need it
        public const int HeaderSize = sizeof(double);

        // batched messages
        // IMPORTANT: we queue the serialized messages!
        //            queueing NetworkMessage would box and allocate!
        Queue<PooledNetworkWriter> messages = new Queue<PooledNetworkWriter>();

        public Batcher(int threshold)
        {
            this.threshold = threshold;
        }

        // add a message for batching
        // we allow any sized messages.
        // caller needs to make sure they are within max packet size.
        public void AddMessage(ArraySegment<byte> message)
        {
            // put into a (pooled) writer
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            // -> will be returned to pool when making the batch!
            // IMPORTANT: NOT adding a size header / msg saves LOTS of bandwidth
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(message.Array, message.Offset, message.Count);
            messages.Enqueue(writer);
        }

        // batch as many messages as possible into writer
        // returns true if any batch was made.
        public bool MakeNextBatch(NetworkWriter writer, double timeStamp)
        {
            // if we have no messages then there's nothing to do
            if (messages.Count == 0)
                return false;

            // make sure the writer is fresh to avoid uncertain situations
            if (writer.Position != 0)
                throw new ArgumentException($"MakeNextBatch needs a fresh writer!");

            // write timestamp first
            // -> double precision for accuracy over long periods of time
            writer.WriteDouble(timeStamp);

            // do start no matter what
            do
            {
                // add next message no matter what. even if > threshold.
                // (we do allow > threshold sized messages as single batch)
                PooledNetworkWriter message = messages.Dequeue();
                ArraySegment<byte> segment = message.ToArraySegment();
                writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

                // return the writer to pool
                NetworkWriterPool.Recycle(message);
            }
            // keep going as long as we have more messages,
            // AND the next one would fit into threshold.
            while (messages.Count > 0 &&
                   writer.Position + messages.Peek().Position <= threshold);

            // we had messages, so a batch was made
            return true;
        }
    }
}
