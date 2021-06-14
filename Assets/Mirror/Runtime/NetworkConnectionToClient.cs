using System;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public override string address =>
            Transport.activeTransport.ServerGetClientAddress(connectionId);

        // unbatcher
        public Unbatcher unbatcher = new Unbatcher();

        public NetworkConnectionToClient(int networkConnectionId, bool batching)
            : base(networkConnectionId, batching)
        {
        }

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            //Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

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
                        Transport.activeTransport.ServerSend(connectionId, segment, channelId);
                }
                // otherwise send directly to minimize latency
                else Transport.activeTransport.ServerSend(connectionId, segment, channelId);
            }
        }

        // flush batched messages at the end of every Update.
        internal void Update()
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
                            Transport.activeTransport.ServerSend(connectionId, writer.ToArraySegment(), kvp.Key);
                            //UnityEngine.Debug.Log($"sending batch of {writer.Position} bytes for channel={kvp.Key} connId={connectionId}");

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
