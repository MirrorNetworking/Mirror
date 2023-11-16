using System;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    public class LocalConnectionToClient : NetworkConnectionToClient
    {
        internal LocalConnectionToServer connectionToServer;

        public LocalConnectionToClient() : base(LocalConnectionId) {}

        public override string address => "localhost";

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // instead of invoking it directly, we enqueue and process next update.
            // this way we can simulate a similar call flow as with remote clients.
            // the closer we get to simulating host as remote, the better!

            //Debug.Log($"Enqueue {BitConverter.ToString(segment.Array, segment.Offset, segment.Count)}");
            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
            connectionToServer.queue.Enqueue(writer);
        }

        // true because local connections never timeout
        internal override bool IsAlive(float timeout) => true;

        // don't ping host client in host mode
        protected override void UpdatePing() {}

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
