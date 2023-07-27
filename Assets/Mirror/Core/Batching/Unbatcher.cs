// un-batching functionality encapsulated into one class.
// -> less complexity
// -> easy to test
//
// includes timestamp for tick batching.
// -> allows NetworkTransform etc. to use timestamp without including it in
//    every single message
using System;
using System.Collections.Generic;

namespace Mirror
{
    public class Unbatcher
    {
        // supporting adding multiple batches before GetNextMessage is called.
        // just in case.
        readonly Queue<NetworkWriterPooled> batches = new Queue<NetworkWriterPooled>();

        public int BatchesCount => batches.Count;

        // NetworkReader is only created once,
        // then pointed to the first batch.
        readonly NetworkReader reader = new NetworkReader(new byte[0]);

        // timestamp that was written into the batch remotely.
        // for the batch that our reader is currently pointed at.
        double readerRemoteTimeStamp;

        // helper function to start reading a batch.
        void StartReadingBatch(NetworkWriterPooled batch)
        {
            // point reader to it
            reader.SetBuffer(batch.ToArraySegment());

            // read remote timestamp (double)
            // -> AddBatch quarantees that we have at least 8 bytes to read
            readerRemoteTimeStamp = reader.ReadDouble();
        }

        // add a new batch.
        // returns true if valid.
        // returns false if not, in which case the connection should be disconnected.
        public bool AddBatch(ArraySegment<byte> batch)
        {
            // IMPORTANT: ArraySegment is only valid until returning. we copy it!
            //
            // NOTE: it's not possible to create empty ArraySegments, so we
            //       don't need to check against that.

            // make sure we have at least 8 bytes to read for tick timestamp
            if (batch.Count < Batcher.TimestampSize)
                return false;

            // put into a (pooled) writer
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            // -> will be returned to pool when sending!
            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteBytes(batch.Array, batch.Offset, batch.Count);

            // first batch? then point reader there
            if (batches.Count == 0)
                StartReadingBatch(writer);

            // add batch
            batches.Enqueue(writer);
            //Debug.Log($"Adding Batch {BitConverter.ToString(batch.Array, batch.Offset, batch.Count)} => batches={batches.Count} reader={reader}");
            return true;
        }

        // get next message, unpacked from batch (if any)
        // message ArraySegment is only valid until the next call.
        // timestamp is the REMOTE time when the batch was created remotely.
        public bool GetNextMessage(out ArraySegment<byte> message, out double remoteTimeStamp)
        {
            message = default;
            remoteTimeStamp = 0;

            // do nothing if we don't have any batches.
            // otherwise the below queue.Dequeue() would throw an
            // InvalidOperationException if operating on empty queue.
            if (batches.Count == 0)
                return false;

            // was our reader pointed to anything yet?
            if (reader.Capacity == 0)
                return false;

            // no more data to read?
            if (reader.Remaining == 0)
            {
                // retire the batch
                NetworkWriterPooled writer = batches.Dequeue();
                NetworkWriterPool.Return(writer);

                // do we have another batch?
                if (batches.Count > 0)
                {
                    // point reader to the next batch.
                    // we'll return the reader below.
                    NetworkWriterPooled next = batches.Peek();
                    StartReadingBatch(next);
                }
                // otherwise there's nothing more to read
                else return false;
            }

            // use the current batch's remote timestamp
            // AFTER potentially moving to the next batch ABOVE!
            remoteTimeStamp = readerRemoteTimeStamp;

            // enough data to read the size prefix?
            if (reader.Remaining == 0)
                return false;

            // read the size prefix as varint
            // see Batcher.AddMessage comments for explanation.
            int size = (int)Compression.DecompressVarUInt(reader);

            // validate size prefix, in case attackers send malicious data
            if (reader.Remaining < size)
                return false;

            // return the message of size
            message = reader.ReadBytesSegment(size);
            return true;
        }
    }
}
