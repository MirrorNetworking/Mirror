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

        // Send stage two: serialized NetworkMessage as ArraySegment<byte>
        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // get a writer to copy the message into since the segment is only
            // valid until returning.
            // => pooled writer will be returned to pool when dequeuing.
            // => WriteBytes instead of WriteArraySegment because the latter
            //    includes a 4 bytes header. we just want to write raw.
            //Debug.Log($"Enqueue {BitConverter.ToString(segment.Array, segment.Offset, segment.Count)}");
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
            connectionToServer.queue.Enqueue(writer);
        }

        // true because local connections never timeout
        internal override bool IsAlive(float timeout) => true;

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
