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
        Queue<PooledNetworkWriter> batches = new Queue<PooledNetworkWriter>();

        // NetworkReader is only created once,
        // then pointed to the first batch.
        NetworkReader reader = new NetworkReader(new byte[0]);

        // timestamp that was written into the batch remotely.
        // for the batch that our reader is currently pointed at.
        double readerRemoteTimeStamp;

        // helper function to start reading a batch.
        void StartReadingBatch(PooledNetworkWriter batch)
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
            if (batch.Count < Batcher.HeaderSize)
                return false;

            // put into a (pooled) writer
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            // -> will be returned to pool when sending!
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
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
        // timestamp is the REMOTE time when the batch was created remotely.
        public bool GetNextMessage(out NetworkReader message, out double remoteTimeStamp)
        {
            // getting messages would be easy via
            //   <<size, message, size, message, ...>>
            // but to save A LOT of bandwidth, we use
            //   <<message, message, ...>
            // in other words, we don't know where the current message ends
            //
            // BUT: it doesn't matter!
            // -> we simply return the reader
            //    * if we have one yet
            //    * and if there's more to read
            // -> the caller can then read one message from it
            // -> when the end is reached, we retire the batch!
            //
            // for example:
            //   while (GetNextMessage(out message))
            //       ProcessMessage(message);
            //
            message = null;

            // do nothing if we don't have any batches.
            // otherwise the below queue.Dequeue() would throw an
            // InvalidOperationException if operating on empty queue.
            if (batches.Count == 0)
            {
                remoteTimeStamp = 0;
                return false;
            }

            // was our reader pointed to anything yet?
            if (reader.Length == 0)
            {
                remoteTimeStamp = 0;
                return false;
            }

            // no more data to read?
            if (reader.Remaining == 0)
            {
                // retire the batch
                PooledNetworkWriter writer = batches.Dequeue();
                NetworkWriterPool.Recycle(writer);

                // do we have another batch?
                if (batches.Count > 0)
                {
                    // point reader to the next batch.
                    // we'll return the reader below.
                    PooledNetworkWriter next = batches.Peek();
                    StartReadingBatch(next);
                }
                // otherwise there's nothing more to read
                else
                {
                    remoteTimeStamp = 0;
                    return false;
                }
            }

            // use the current batch's remote timestamp
            // AFTER potentially moving to the next batch ABOVE!
            remoteTimeStamp = readerRemoteTimeStamp;

            // if we got here, then we have more data to read.
            message = reader;
            return true;
        }
    }
}
