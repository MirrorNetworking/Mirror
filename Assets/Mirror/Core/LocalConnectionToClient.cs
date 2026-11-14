using System;
using System.Collections.Generic;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    public class LocalConnectionToClient : NetworkConnectionToClient
    {
        internal LocalConnectionToServer connectionToServer;

        // packet queue
        internal readonly Queue<NetworkWriterPooled> queue = new Queue<NetworkWriterPooled>();

        public LocalConnectionToClient() : base(LocalConnectionId) {}

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // instead of invoking it directly, we enqueue and process next update.
            // this way we can simulate a similar call flow as with remote clients.
            // the closer we get to simulating host as remote, the better!
            // both directions do this, so [Command] and [Rpc] behave the same way.

            //Debug.Log($"Enqueue {BitConverter.ToString(segment.Array, segment.Offset, segment.Count)}");
            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
            connectionToServer.queue.Enqueue(writer);
        }

        // true because local connections never timeout
        internal override bool IsAlive(float timeout) => true;

        // don't ping host client in host mode
        protected override void UpdatePing() {}

        internal override void Update()
        {
            base.Update();

            // process internal messages so they are applied at the correct time
            while (queue.Count > 0)
            {
                // call receive on queued writer's content, return to pool
                NetworkWriterPooled writer = queue.Dequeue();
                ArraySegment<byte> message = writer.ToArraySegment();

                // OnTransportData assumes a proper batch with timestamp etc.
                // let's make a proper batch and pass it to OnTransportData.
                Batcher batcher = GetBatchForChannelId(Channels.Reliable);
                batcher.AddMessage(message, NetworkTime.localTime);

                using (NetworkWriterPooled batchWriter = NetworkWriterPool.Get())
                {
                    // make a batch with our local time (double precision)
                    if (batcher.GetBatch(batchWriter))
                    {
                        NetworkServer.OnTransportData(connectionId, batchWriter.ToArraySegment(), Channels.Reliable);
                    }
                }

                NetworkWriterPool.Return(writer);
            }
        }

        internal void DisconnectInternal()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            RemoveFromObservingsObservers();
        }

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            DisconnectInternal();
            connectionToServer.DisconnectInternal();
        }
    }
}
