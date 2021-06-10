using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public override string address => "";

        public NetworkConnectionToServer(bool batching) : base(batching)
        {
        }

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                // batching enabled?
                if (batching)
                {
                    // try to batch, or send directly if too big.
                    // (user might try to send a max message sized message,
                    //  where max message size is larger than max batch size.
                    //  for example, kcp2k max message size is 144 KB but we
                    //  only want to batch MTU each time)
                    if (!GetBatchForChannelId(channelId).AddMessage(segment))
                        Transport.activeTransport.ClientSend(segment, channelId);
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
