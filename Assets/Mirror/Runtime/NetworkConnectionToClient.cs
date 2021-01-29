using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkConnectionToClient>();

        public override string address => Transport.activeTransport.ServerGetClientAddress(connectionId);

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
            // each batch needs a writer for batching
            // => we allocate one writer per channel
            // => it grows to Transport MaxMessageSize automatically
            // TODO maybe use a pooled writer and return when disconnecting?
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

        // batching is optional because due to mirror's non-optimal update order
        // it would increase latency.

        // batching is still optional until we improve mirror's update order.
        // right now it increases latency because:
        //   enabling batching flushes all state updates in same frame, but
        //   transport processes incoming messages afterwards so server would
        //   batch them until next frame's flush
        // => disable it for super fast paced games
        // => enable it for high scale / cpu heavy games
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

        void SendBatch(int channelId, Batch batch)
        {
            // send batch
            Transport.activeTransport.ServerSend(connectionId, channelId, batch.writer.ToArraySegment());

            // clear batch
            batch.writer.Reset();

            // reset send time for this channel's batch
            batch.lastSendTime = NetworkTime.time;
        }

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (logger.LogEnabled()) logger.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            //Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                // batching?
                if (batching)
                {
                    // always batch!
                    // (even if interval == 0, in which case we flush in Update())
                    //
                    // if batch would become bigger than MaxBatchPacketSize for this
                    // channel then send out the previous batch first.
                    //
                    // IMPORTANT: we use maxBATCHsize not maxPACKETsize.
                    //            some transports like kcp have large maxPACKETsize
                    //            like 144kb, but those would extremely slow to use
                    //            all the time for batching.
                    //            (maxPACKETsize messages still work fine, we just
                    //             aim for maxBATCHsize where possible)
                    Batch batch = GetBatchForChannelId(channelId);
                    int max = Transport.activeTransport.GetMaxBatchSize(channelId);
                    if (batch.writer.Position + segment.Count > max)
                    {
                        //UnityEngine.Debug.LogWarning($"sending batch {batch.writer.Position} / {max} after full for segment={segment.Count} for connectionId={connectionId}");
                        SendBatch(channelId, batch);
                    }

                    // now add segment to batch
                    batch.writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
                }
                // otherwise send directly to minimize latency
                else
                {
                    Transport.activeTransport.ServerSend(connectionId, channelId, segment);
                }
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
                    if (elapsed >= batchInterval &&
                        kvp.Value.writer.Position > 0)
                    {
                        // send the batch. time will be reset internally.
                        //Debug.Log($"sending batch of {kvp.Value.writer.Position} bytes for channel={kvp.Key} connId={connectionId}");
                        SendBatch(kvp.Key, kvp.Value);
                    }
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
            RemoveObservers();
        }
    }
}
