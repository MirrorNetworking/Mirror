using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public override string address => "";

        public NetworkConnectionToServer(bool batching) : base(batching) {}

        // Send stage two: serialized NetworkMessage as ArraySegment<byte>
        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                // batching enabled?
                if (batching)
                {
                    // add to batch no matter what.
                    // batching will try to fit as many as possible into MTU.
                    // but we still allow > MTU, e.g. kcp max packet size 144kb.
                    // those are simply sent as single batches.
                    //
                    // IMPORTANT: do NOT send > batch sized messages directly:
                    // - data race: large messages would be sent directly. small
                    //   messages would be sent in the batch at the end of frame
                    // - timestamps: if batching assumes a timestamp, then large
                    //   messages need that too.
                    GetBatchForChannelId(channelId).AddMessage(segment);
                }
                // otherwise send directly to minimize latency
                else Transport.activeTransport.ClientSend(segment, channelId);
            }
        }

        // flush batched messages at the end of every Update.
        internal virtual void Update()
        {
            // batching?
            if (batching)
            {
                // go through batches for all channels
                foreach (KeyValuePair<int, Batcher> kvp in batches)
                {
                    // make and send as many batches as necessary from the stored
                    // messages.
                    Batcher batcher = kvp.Value;
                    using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                    {
                        while (batcher.MakeNextBatch(writer))
                        {
                            // validate packet before handing the batch to the
                            // transport. this guarantees that we always stay
                            // within transport's max message size limit.
                            // => just in case transport forgets to check it
                            // => just in case mirror miscalulated it etc.
                            ArraySegment<byte> segment = writer.ToArraySegment();
                            if (ValidatePacketSize(segment, kvp.Key))
                            {
                                // send
                                Transport.activeTransport.ClientSend(writer.ToArraySegment(), kvp.Key);
                                //UnityEngine.Debug.Log($"sending batch of {writer.Position} bytes for channel={kvp.Key}");

                                // reset writer for each new batch
                                writer.Position = 0;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            // TODO remove redundant state. have one source of truth for .ready!
            isReady = false;
            NetworkClient.ready = false;
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
