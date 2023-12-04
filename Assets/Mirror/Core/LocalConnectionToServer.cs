using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // a localClient's connection TO a server.
    // send messages on this connection causes the server's handler function to be invoked directly.
    public class LocalConnectionToServer : NetworkConnectionToServer
    {
        internal LocalConnectionToClient connectionToClient;

        // packet queue
        internal readonly Queue<NetworkWriterPooled> queue = new Queue<NetworkWriterPooled>();

        // see caller for comments on why we need this
        bool connectedEventPending;
        bool disconnectedEventPending;
        internal void QueueConnectedEvent() => connectedEventPending = true;
        internal void QueueDisconnectedEvent() => disconnectedEventPending = true;

        // Send stage two: serialized NetworkMessage as ArraySegment<byte>
        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (segment.Count == 0)
            {
                Debug.LogError("LocalConnection.SendBytes cannot send zero bytes");
                return;
            }

            // instead of invoking it directly, we enqueue and process next update.
            // this way we can simulate a similar call flow as with remote clients.
            // the closer we get to simulating host as remote, the better!
            // both directions do this, so [Command] and [Rpc] behave the same way.

            //Debug.Log($"Enqueue {BitConverter.ToString(segment.Array, segment.Offset, segment.Count)}");
            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
            connectionToClient.queue.Enqueue(writer);
        }

        internal override void Update()
        {
            base.Update();

            // should we still process a connected event?
            if (connectedEventPending)
            {
                connectedEventPending = false;
                NetworkClient.OnConnectedEvent?.Invoke();
            }

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
                        NetworkClient.OnTransportData(batchWriter.ToArraySegment(), Channels.Reliable);
                    }
                }

                NetworkWriterPool.Return(writer);
            }

            // should we still process a disconnected event?
            if (disconnectedEventPending)
            {
                disconnectedEventPending = false;
                NetworkClient.OnDisconnectedEvent?.Invoke();
            }
        }

        /// <summary>Disconnects this connection.</summary>
        internal void DisconnectInternal()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            // TODO remove redundant state. have one source of truth for .ready!
            isReady = false;
            NetworkClient.ready = false;
        }

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            connectionToClient.DisconnectInternal();
            DisconnectInternal();

            // simulate what a true remote connection would do:
            // first, the server should remove it:
            // TODO should probably be in connectionToClient.DisconnectInternal
            //      because that's the NetworkServer's connection!
            NetworkServer.RemoveLocalConnection();

            // then call OnTransportDisconnected for proper disconnect handling,
            // callbacks & cleanups.
            // => otherwise OnClientDisconnected() is never called!
            // => see NetworkClientTests.DisconnectCallsOnClientDisconnect_HostMode()
            NetworkClient.OnTransportDisconnected();
        }

        // true because local connections never timeout
        internal override bool IsAlive(float timeout) => true;
    }
}
