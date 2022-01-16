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
        internal readonly Queue<PooledNetworkWriter> queue = new Queue<PooledNetworkWriter>();

        public override string address => "localhost";

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

            // OnTransportData assumes batching.
            // so let's make a batch with proper timestamp prefix.
            Batcher batcher = GetBatchForChannelId(channelId);
            batcher.AddMessage(segment);

            // flush it to the server's OnTransportData immediately.
            // local connection to server always invokes immediately.
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // make a batch with our local time (double precision)
                if (batcher.MakeNextBatch(writer, NetworkTime.localTime))
                {
                    NetworkServer.OnTransportData(connectionId, writer.ToArraySegment(), channelId);
                }
                else Debug.LogError("Local connection failed to make batch. This should never happen.");
            }
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
                PooledNetworkWriter writer = queue.Dequeue();
                ArraySegment<byte> message = writer.ToArraySegment();

                // OnTransportData assumes a proper batch with timestamp etc.
                // let's make a proper batch and pass it to OnTransportData.
                Batcher batcher = GetBatchForChannelId(Channels.Reliable);
                batcher.AddMessage(message);

                using (PooledNetworkWriter batchWriter = NetworkWriterPool.GetWriter())
                {
                    // make a batch with our local time (double precision)
                    if (batcher.MakeNextBatch(batchWriter, NetworkTime.localTime))
                    {
                        NetworkClient.OnTransportData(batchWriter.ToArraySegment(), Channels.Reliable);
                    }
                }

                NetworkWriterPool.Recycle(writer);
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
