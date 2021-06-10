// un-batching functionality encapsulated into one class.
// -> less complexity
// -> easy to test
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

        // add a new batch
        public void AddBatch(ArraySegment<byte> batch)
        {
            // IMPORTANT: ArraySegment is only valid until returning. we copy it!

            // NOTE: it's not possible to create empty ArraySegments, so we
            //       don't need to check against that.

            // put into a (pooled) writer
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            // -> will be returned to pool when sending!
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(batch.Array, batch.Offset, batch.Count);

            // first batch? then point reader there
            if (batches.Count == 0)
                reader.SetBuffer(writer.ToArraySegment());

            // add batch
            batches.Enqueue(writer);
            //Debug.Log($"Adding Batch {BitConverter.ToString(batch.Array, batch.Offset, batch.Count)} => batches={batches.Count} reader={reader}");
        }

        // get next message, unpacked from batch (if any)
        public bool GetNextMessage(out NetworkReader message)
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

            // was our reader pointed to anything yet?
            if (reader.Length == 0)
                return false;

            // no more data to read?
            if (reader.Position >= reader.Length)
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
                    reader.SetBuffer(next.ToArraySegment());
                }
                // otherwise there's nothing more to read
                else return false;
            }

            // if we got here, then we have more data to read.
            message = reader;
            return true;
        }
    }
}
