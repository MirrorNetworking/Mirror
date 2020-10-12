using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        // batching from server to client.
        // fewer transport calls give us significantly better performance/scale.
        //
        // for a 64KB max message transport and 64 bytes/message on average, we
        // reduce transport calls by a factor of 1000.
        //
        // depending on the transport, this can give 10x performance.
        //
        // Dictionary<channelId, batch> because we have multiple channels.
        class Batch
        {
            // each batch needs a writer for batching
            // ushort.max to work with all Transport.GetMaxMessageSizes!
            // this way we don't depend & check on any transport size. it's const.
            public NetworkWriter writer = new NetworkWriter(ushort.MaxValue);

            // each channel's batch has its own lastSendTime.
            // (use NetworkTime for maximum precision over days)
            //
            // channel batches are full and flushed at different times. using
            // one global time wouldn't make sense.
            // -> we want to be able to reset a channels send time after Send()
            //    flushed it because full. global time wouldn't allow that, so
            //    we would often flush in Send() and then flush again in Update
            //    even though we just flushed in Send().
            public double lastSendTime;
        }
        Dictionary<int, Batch> batches = new Dictionary<int, Batch>();

        // batch interval is 0 by default, meaning that we send immediately.
        // (useful to run tests without waiting for intervals too)
        float batchInterval;

        public NetworkConnectionToClient(int networkConnectionId, float batchInterval) : base(networkConnectionId)
        {
            this.batchInterval = batchInterval;
        }

        public override string address => Transport.activeTransport.ServerGetClientAddress(connectionId);

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

        void SendBatch(int channelId, Batch batch)
        {
            // send batch
            Transport.activeTransport.ServerSend(connectionId, channelId, batch.writer.ToArraySegment());

            // clear batch
            batch.writer.Position = 0;

            // reset send time for this channel's batch
            batch.lastSendTime = NetworkTime.time;
        }

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            //Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                // batching?
                if (batchInterval > 0)
                {
                    // if batch would become bigger than MaxPacketSize then send
                    // out the previous batch first
                    Batch batch = GetBatchForChannelId(channelId);
                    int max = Transport.activeTransport.GetMaxPacketSize();
                    if (batch.writer.Position + segment.Count > max)
                    {
                        //UnityEngine.Debug.LogWarning($"sending batch {batch.writer.Position} / {max} after full for segment={segment.Count} for connectionId={connectionId}");
                        SendBatch(channelId, batch);
                    }

                    // now add segment to batch
                    batch.writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
                    return true;
                }
                // otherwise send directly (for tests etc.)
                else return Transport.activeTransport.ServerSend(connectionId, channelId, segment);
            }
            return false;
        }

        // flush batched messages every batchInterval to make sure that they are
        // sent out every now and then, even if the batch isn't full yet.
        // (avoids 30s latency if batches would only get full every 30s)
        internal void Update()
        {
            // go through batches for all channels
            foreach (KeyValuePair<int, Batch> kvp in batches)
            {
                // enough time elapsed to flush this channel's batch?
                // and not empty?
                if (NetworkTime.time >= kvp.Value.lastSendTime + batchInterval &&
                    kvp.Value.writer.Position > 0)
                {
                    // send the batch. time will be reset internally.
                    //UnityEngine.Debug.LogWarning($"sending batch of {kvp.Value.writer.Position} bytes after time for channel={kvp.Key} connId={connectionId}");
                    SendBatch(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            Transport.activeTransport.ServerDisconnect(connectionId);
        }
    }
}
