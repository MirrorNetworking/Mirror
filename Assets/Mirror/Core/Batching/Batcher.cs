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

        // TimeStamp header size. each batch has one.
        public const int TimestampSize = sizeof(double);

        // Message header size. each message has one.
        public static int MessageHeaderSize(int messageSize) =>
            Compression.VarUIntSize((ulong)messageSize);

        // maximum overhead for a single message.
        // useful for the outside to calculate max message sizes.
        public static int MaxMessageOverhead(int messageSize) =>
            TimestampSize + MessageHeaderSize(messageSize);

        // full batches ready to be sent.
        // DO NOT queue NetworkMessage, it would box.
        // DO NOT queue each serialization separately.
        //        it would allocate too many writers.
        //        https://github.com/vis2k/Mirror/pull/3127
        // => best to build batches on the fly.
        readonly Queue<NetworkWriterPooled> batches = new Queue<NetworkWriterPooled>();

        // current batch in progress.
        // we also store the timestamp to ensure we don't add a message from another frame,
        // as this would introduce subtle jitter!
        //
        // for example:
        // - a batch is started at t=1, another message is added at t=2 and then it's flushed
        // - NetworkTransform uses remoteTimestamp which is t=1
        // - snapshot interpolation would off by one (or multiple) frames!
        NetworkWriterPooled batch;
        double batchTimestamp;

        public Batcher(int threshold)
        {
            this.threshold = threshold;
        }

        // add a message for batching
        // we allow any sized messages.
        // caller needs to make sure they are within max packet size.
        public void AddMessage(ArraySegment<byte> message, double timeStamp)
        {
            // safety: message timestamp is only written once.
            // make sure all messages in this batch are from the same timestamp.
            // otherwise it could silently introduce jitter.
            //
            // this happened before:
            // - NetworkEarlyUpdate @ t=1 processes transport messages
            //   - a handler replies by sending a message
            //     - a new batch is started @ t=1, timestamp is encoded
            // - NetworkLateUpdate @ t=2 decides it's time to broadcast
            //   - NetworkTransform sends @ t=2
            //     - we add to the above batch which already encoded t=1
            // - Client receives the batch which timestamp t=1
            //   - NetworkTransform uses remoteTime for interpolation
            //     remoteTime is the batch timestamp which is t=1
            //     - the NetworkTransform message is actually t=2
            // => smooth interpolation would be impossible!
            //    NT thinks the position was @ t=1 but actually it was @ t=2 !
            //
            // the solution: if timestamp changed, enqueue the existing batch
            if (batch != null && batchTimestamp != timeStamp)
            {
                batches.Enqueue(batch);
                batch = null;
                batchTimestamp = 0;
            }

            // predict the needed size, which is varint(size) + content
            int headerSize = Compression.VarUIntSize((ulong)message.Count);
            int neededSize = headerSize + message.Count;

            // when appending to a batch in progress, check final size.
            // if it expands beyond threshold, then we should finalize it first.
            // => less than or exactly threshold is fine.
            //    GetBatch() will finalize it.
            // => see unit tests.
            if (batch != null &&
                batch.Position + neededSize > threshold)
            {
                batches.Enqueue(batch);
                batch = null;
                batchTimestamp = 0;
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

                // remember the encoded timestamp, see safety check below.
                batchTimestamp = timeStamp;
            }

            // add serialization to current batch. even if > threshold.
            // -> we do allow > threshold sized messages as single batch
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            //
            // include size prefix as varint!
            // -> fixes NetworkMessage serialization mismatch corrupting the
            //    next message in a batch.
            // -> a _lot_ of time was wasted debugging corrupt batches.
            //    no easy way to figure out which NetworkMessage has a mismatch.
            // -> this is worth everyone's sanity.
            // -> varint means we prefix with 1 byte most of the time.
            // -> the same issue in NetworkIdentity was why Mirror started!
            Compression.CompressVarUInt(batch, (ulong)message.Count);
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

        // return all batches to the pool for cleanup
        public void Clear()
        {
            // return batch in progress
            if (batch != null)
            {
                NetworkWriterPool.Return(batch);
                batch = null;
                batchTimestamp = 0;
            }

            // return all queued batches
            foreach (NetworkWriterPooled queued in batches)
                NetworkWriterPool.Return(queued);

            batches.Clear();
        }
    }
}
