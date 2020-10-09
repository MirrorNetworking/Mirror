using System;

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
        NetworkWriter batch = new NetworkWriter();

        // batch interval is 0 by default, meaning that we send immediately.
        // (useful to run tests without waiting for intervals too)
        float batchInterval;
        double batchSendTime;

        public NetworkConnectionToClient(int networkConnectionId, float batchInterval) : base(networkConnectionId)
        {
            this.batchInterval = batchInterval;
        }

        public override string address => Transport.activeTransport.ServerGetClientAddress(connectionId);

        void SendBatch(int channelId)
        {
            // send batch
            Transport.activeTransport.ServerSend(connectionId, channelId, batch.ToArraySegment());

            // clear batch
            batch.Position = 0;

            // reset send time
            // (use NetworkTime for maximum precision over days)
            batchSendTime = NetworkTime.time;
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
                    // TODO respect channelId
                    int max = Transport.activeTransport.GetMaxPacketSize(Channels.DefaultReliable);
                    if (batch.Position + segment.Count > max)
                    {
                        // TODO respect channelId
                        //Debug.LogWarning($"sending batch {batch.Position} / {max} after full for segment={segment.Count} for connectionId={connectionId}");
                        SendBatch(Channels.DefaultReliable);
                    }

                    // now add segment to batch
                    batch.WriteBytes(segment.Array, segment.Offset, segment.Count);
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
            // enough time elapsed and anything batched?
            if (NetworkTime.time > batchSendTime + batchInterval &&
                batch.Position > 0)
            {
                // TODO respect channelId
                //Debug.LogWarning($"sending batch of {batch.Position} bytes after time for connId= {connectionId}");
                SendBatch(Channels.DefaultReliable);
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
