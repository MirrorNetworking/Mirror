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
            internal NetworkWriter writer = new NetworkWriter();

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
            if (batch.writer.Position > 0)
            {
                Transport.activeTransport.ServerSend(connectionId, batch.writer.ToArraySegment(), channelId);
                batch.writer.Position = 0;
                // reset send time for this channel's batch
                batch.lastSendTime = NetworkTime.time;
            }
        }

        // add a message to a batch. internal so we can test it
        internal void AddMessageToBatch(Batch batch, ArraySegment<byte> segment, int channelId)
        {
            // if the current batch would exceed the maximum size the transport asks for, flush first
            if (batch.writer.Position + segment.Count > Transport.activeTransport.GetMaxBatchSize(channelId))
            {
                SendBatch(channelId, batch);
            }
            // -> WriteBytes instead of WriteSegment because the latter
            //    would add a size header. we want to write directly.
            batch.writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
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
                    Batch batch = GetBatchForChannelId(channelId);
                    AddMessageToBatch(batch, segment, channelId);
                }
                // otherwise send directly to minimize latency
                else Transport.activeTransport.ServerSend(connectionId, segment, channelId);
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
                    if (elapsed >= batchInterval)
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
            RemoveFromObservingsObservers();
        }
    }
}
