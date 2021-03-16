using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public override string address =>
            Transport.activeTransport.ServerGetClientAddress(connectionId);

        // batching from server to client.
        // fewer transport calls give us significantly better performance/scale.
        //
        // for a 64KB max message transport and 64 bytes/message on average, we
        // reduce transport calls by a factor of 1000.
        //
        // depending on the transport, this can give 10x performance.
        //
        // Dictionary<channelId, batch> because we have multiple channels.
        internal class Batch
        {
            // batched messages
            internal Queue<PooledNetworkWriter> messages = new Queue<PooledNetworkWriter>();

            // each channel's batch has its own lastSendTime.
            // (use NetworkTime for maximum precision over days)
            //
            // channel batches are full and flushed at different times. using
            // one global time wouldn't make sense.
            // -> we want to be able to reset a channels send time after Send()
            //    flushed it because full. global time wouldn't allow that, so
            //    we would often flush in Send() and then flush again in Update
            //    even though we just flushed in Send().
            // -> initialize with current NetworkTime so first update doesn't
            //    calculate elapsed via 'now - 0'
            internal double lastSendTime = NetworkTime.time;
        }
        Dictionary<int, Batch> batches = new Dictionary<int, Batch>();

        // batch messages and send them out in LateUpdate (or after batchInterval)
        bool batching;

        // batch interval is 0 by default, meaning that we send immediately.
        // (useful to run tests without waiting for intervals too)
        float batchInterval;

        public NetworkConnectionToClient(int networkConnectionId, bool batching, float batchInterval)
            : base(networkConnectionId)
        {
            this.batching = batching;
            this.batchInterval = batchInterval;
        }

        Batch GetBatchForChannelId(int channelId)
        {
            // get existing or create new writer for the channelId
            Batch batch;
            if (!batches.TryGetValue(channelId, out batch))
            {
                batch = new Batch();
                batches[channelId] = batch;
            }
            return batch;
        }

        // send a batch. internal so we can test it.
        internal void SendBatch(int channelId, Batch batch)
        {
            // get max batch size for this channel
            int max = Transport.activeTransport.GetMaxBatchSize(channelId);

            // we need a writer to merge queued messages into a batch
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // for each queued message
                while (batch.messages.Count > 0)
                {
                    // get it
                    PooledNetworkWriter message = batch.messages.Dequeue();
                    ArraySegment<byte> segment = message.ToArraySegment();

                    // IF adding to writer would end up >= MTU then we should
                    // flush first. the goal is to always flush < MTU packets.
                    //
                    // IMPORTANT: if writer is empty and segment is > MTU
                    //            (which can happen for large max sized message)
                    //            then we would send an empty previous writer.
                    //            => don't do that.
                    //            => only send if not empty.
                    if (writer.Position > 0 &&
                        writer.Position + segment.Count >= max)
                    {
                        // flush & reset writer
                        Transport.activeTransport.ServerSend(connectionId, channelId, writer.ToArraySegment());
                        writer.SetLength(0);
                    }

                    // now add to writer in any case
                    // -> WriteBytes instead of WriteSegment because the latter
                    //    would add a size header. we want to write directly.
                    //
                    // NOTE: it's very possible that we add > MTU to writer if
                    //       message size is > MTU.
                    //       which is fine. next iteration will just flush it.
                    writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

                    // return queued message to pool
                    NetworkWriterPool.Recycle(message);
                }

                // done iterating queued messages.
                // batch might still contain the last message.
                // send it.
                if (writer.Position > 0)
                {
                    Transport.activeTransport.ServerSend(connectionId, channelId, writer.ToArraySegment());
                    writer.SetLength(0);
                }
            }

            // reset send time for this channel's batch
            batch.lastSendTime = NetworkTime.time;
        }

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            //Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                // batching? then add to queued messages
                if (batching)
                {
                    // put into a (pooled) writer
                    // -> WriteBytes instead of WriteSegment because the latter
                    //    would add a size header. we want to write directly.
                    // -> will be returned to pool when sending!
                    PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
                    writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

                    // add to batch queue
                    Batch batch = GetBatchForChannelId(channelId);
                    batch.messages.Enqueue(writer);
                }
                // otherwise send directly to minimize latency
                else Transport.activeTransport.ServerSend(connectionId, channelId, segment);
            }
        }

        // flush batched messages every batchInterval to make sure that they are
        // sent out every now and then, even if the batch isn't full yet.
        // (avoids 30s latency if batches would only get full every 30s)
        internal void Update()
        {
            // batching?
            if (batching)
            {
                // go through batches for all channels
                foreach (KeyValuePair<int, Batch> kvp in batches)
                {
                    // enough time elapsed to flush this channel's batch?
                    // and not empty?
                    double elapsed = NetworkTime.time - kvp.Value.lastSendTime;
                    if (elapsed >= batchInterval && kvp.Value.messages.Count > 0)
                    {
                        // send the batch. time will be reset internally.
                        //Debug.Log($"sending batch of {kvp.Value.writer.Position} bytes for channel={kvp.Key} connId={connectionId}");
                        SendBatch(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            Transport.activeTransport.ServerDisconnect(connectionId);
            RemoveObservers();
        }
    }
}
