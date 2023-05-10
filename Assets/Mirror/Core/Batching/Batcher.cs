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

        // full batches ready to be sent.
        // DO NOT queue NetworkMessage, it would box.
        // DO NOT queue each serialization separately.
        //        it would allocate too many writers.
        //        https://github.com/vis2k/Mirror/pull/3127
        // => best to build batches on the fly.
        readonly Queue<NetworkWriterPooled> batches = new Queue<NetworkWriterPooled>();

        // current batch in progress
        NetworkWriterPooled batch;

        public Batcher(int threshold)
        {
            this.threshold = threshold;
        }

        // add a message for batching
        // we allow any sized messages.
        // caller needs to make sure they are within max packet size.
        public void AddMessage(ArraySegment<byte> message, double timeStamp)
        {
            // when appending to a batch in progress, check final size.
            // if it expands beyond threshold, then we should finalize it first.
            // => less than or exactly threshold is fine.
            //    GetBatch() will finalize it.
            // => see unit tests.
            if (batch != null &&
                batch.Position + message.Count > threshold)
            {
                batches.Enqueue(batch);
                batch = null;
            }

            // initialize a new batch if necessary
            if (batch == null)
            {
                // borrow from pool. we return it in GetBatch.
                batch = NetworkWriterPool.Get();

                // write timestamp first.
                // -> double precision for accuracy over long periods of time
                // -> batches are per-frame, it doesn't matter which message's
                //    timestamp we use.
                batch.WriteDouble(timeStamp);
            }

            // add serialization to current batch. even if > threshold.
            // -> we do allow > threshold sized messages as single batch
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            batch.WriteBytes(message.Array, message.Offset, message.Count);
        }

        // helper function to copy a batch to writer and return it to pool
        static void CopyAndReturn(NetworkWriterPooled batch, NetworkWriter writer)
        {
            // make sure the writer is fresh to avoid uncertain situations
            if (writer.Position != 0)
                throw new ArgumentException($"GetBatch needs a fresh writer!");

            // copy to the target writer
            ArraySegment<byte> segment = batch.ToArraySegment();
            writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

            // return batch to pool for reuse
            NetworkWriterPool.Return(batch);
        }

        // get the next batch which is available for sending (if any).
        // TODO safely get & return a batch instead of copying to writer?
        // TODO could return pooled writer & use GetBatch in a 'using' statement!
        public bool GetBatch(NetworkWriter writer)
        {
            // get first batch from queue (if any)
            if (batches.TryDequeue(out NetworkWriterPooled first))
            {
                CopyAndReturn(first, writer);
                return true;
            }

            // if queue was empty, we can send the batch in progress.
            if (batch != null)
            {
                CopyAndReturn(batch, writer);
                batch = null;
                return true;
            }

            // nothing was written
            return false;
        }
    }
}
