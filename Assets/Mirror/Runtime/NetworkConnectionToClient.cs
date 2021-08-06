using System;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public override string address =>
            Transport.activeTransport.ServerGetClientAddress(connectionId);

        // unbatcher
        public Unbatcher unbatcher = new Unbatcher();

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
                        Transport.activeTransport.ServerSend(connectionId, writer.ToArraySegment(), channelId);
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
                    Transport.activeTransport.ServerSend(connectionId, writer.ToArraySegment(), channelId);
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
                    if (elapsed >= batchInterval && kvp.Value.messages.Count > 0)
                    {
                        // send the batch. time will be reset internally.
                        //Debug.Log($"sending batch of {kvp.Value.writer.Position} bytes for channel={kvp.Key} connId={connectionId}");
                        SendBatch(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        public NetworkConnectionToClient(int networkConnectionId)
            : base(networkConnectionId) {}

        // Send stage three: hand off to transport
        protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            Transport.activeTransport.ServerSend(connectionId, segment, channelId);

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            Transport.activeTransport.ServerDisconnect(connectionId);

            // IMPORTANT: NetworkConnection.Disconnect() is NOT called for
            // voluntary disconnects from the other end.
            // -> so all 'on disconnect' cleanup code needs to be in
            //    OnTransportDisconnect, where it's called for both voluntary
            //    and involuntary disconnects!
        }
    }
}
