using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>Base NetworkConnection class for server-to-client and client-to-server connection.</summary>
    public abstract class NetworkConnection
    {
        public const int LocalConnectionId = 0;

        /// <summary>NetworkIdentities that this connection can see</summary>
        // TODO move to server's NetworkConnectionToClient?
        public readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

        /// <summary>Unique identifier for this connection that is assigned by the transport layer.</summary>
        // assigned by transport, this id is unique for every connection on server.
        // clients don't know their own id and they don't know other client's ids.
        public readonly int connectionId;

        /// <summary>Flag that indicates the client has been authenticated.</summary>
        public bool isAuthenticated;

        /// <summary>General purpose object to hold authentication data, character selection, tokens, etc.</summary>
        public object authenticationData;

        /// <summary>A server connection is ready after joining the game world.</summary>
        // TODO move this to ConnectionToClient so the flag only lives on server
        // connections? clients could use NetworkClient.ready to avoid redundant
        // state.
        public bool isReady;

        /// <summary>IP address of the connection. Can be useful for game master IP bans etc.</summary>
        public abstract string address { get; }

        /// <summary>Last time a message was received for this connection. Includes system and user messages.</summary>
        public float lastMessageTime;

        /// <summary>This connection's main object (usually the player object).</summary>
        public NetworkIdentity identity { get; internal set; }

        /// <summary>All NetworkIdentities owned by this connection. Can be main player, pets, etc.</summary>
        // IMPORTANT: this needs to be <NetworkIdentity>, not <uint netId>.
        //            fixes a bug where DestroyOwnedObjects wouldn't find the
        //            netId anymore: https://github.com/vis2k/Mirror/issues/1380
        //            Works fine with NetworkIdentity pointers though.
        public readonly HashSet<NetworkIdentity> clientOwnedObjects = new HashSet<NetworkIdentity>();

        // batching from server to client & client to server.
        // fewer transport calls give us significantly better performance/scale.
        //
        // for a 64KB max message transport and 64 bytes/message on average, we
        // reduce transport calls by a factor of 1000.
        //
        // depending on the transport, this can give 10x performance.
        //
        // Dictionary<channelId, batch> because we have multiple channels.
        protected Dictionary<int, Batcher> batches = new Dictionary<int, Batcher>();

        /// <summary>last batch's remote timestamp. not interpolated. useful for NetworkTransform etc.</summary>
        // for any given NetworkMessage/Rpc/Cmd/OnSerialize, this was the time
        // on the REMOTE END when it was sent.
        //
        // NOTE: this is NOT in NetworkTime, it needs to be per-connection
        //       because the server receives different batch timestamps from
        //       different connections.
        public double remoteTimeStamp { get; internal set; }

        internal NetworkConnection()
        {
            // set lastTime to current time when creating connection to make
            // sure it isn't instantly kicked for inactivity
            lastMessageTime = Time.time;
        }

        internal NetworkConnection(int networkConnectionId) : this()
        {
            connectionId = networkConnectionId;
            // TODO why isn't lastMessageTime set in here like in the other ctor?
        }

        // TODO if we only have Reliable/Unreliable, then we could initialize
        // two batches and avoid this code
        protected Batcher GetBatchForChannelId(int channelId)
        {
            // get existing or create new writer for the channelId
            Batcher batch;
            if (!batches.TryGetValue(channelId, out batch))
            {
                // get max batch size for this channel
                int threshold = Transport.activeTransport.GetBatchThreshold(channelId);

                // create batcher
                batch = new Batcher(threshold);
                batches[channelId] = batch;
            }
            return batch;
        }

        // validate packet size before sending. show errors if too big/small.
        // => it's best to check this here, we can't assume that all transports
        //    would check max size and show errors internally. best to do it
        //    in one place in Mirror.
        // => it's important to log errors, so the user knows what went wrong.
        protected static bool ValidatePacketSize(ArraySegment<byte> segment, int channelId)
        {
            int max = Transport.activeTransport.GetMaxPacketSize(channelId);
            if (segment.Count > max)
            {
                Debug.LogError($"NetworkConnection.ValidatePacketSize: cannot send packet larger than {max} bytes, was {segment.Count} bytes");
                return false;
            }

            if (segment.Count == 0)
            {
                // zero length packets getting into the packet queues are bad.
                Debug.LogError("NetworkConnection.ValidatePacketSize: cannot send zero bytes");
                return false;
            }

            // good size
            return true;
        }

        // Send stage one: NetworkMessage<T>
        /// <summary>Send a NetworkMessage to this connection over the given channel.</summary>
        public void Send<T>(T message, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message and send allocation free
                MessagePacking.Pack(message, writer);
                NetworkDiagnostics.OnSend(message, channelId, writer.Position, 1);
                Send(writer.ToArraySegment(), channelId);
            }
        }

        // Send stage two: serialized NetworkMessage as ArraySegment<byte>
        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        internal virtual void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            //Debug.Log($"ConnectionSend {this} bytes:{BitConverter.ToString(segment.Array, segment.Offset, segment.Count)}");

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
            //
            // NOTE: we ALWAYS batch. it's not optional, because the
            //       receiver needs timestamps for NT etc.
            //
            // NOTE: we do NOT ValidatePacketSize here yet. the final packet
            //       will be the full batch, including timestamp.
            GetBatchForChannelId(channelId).AddMessage(segment);
        }

        // Send stage three: hand off to transport
        protected abstract void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable);

        // flush batched messages at the end of every Update.
        internal virtual void Update()
        {
            // go through batches for all channels
            foreach (KeyValuePair<int, Batcher> kvp in batches)
            {
                // make and send as many batches as necessary from the stored
                // messages.
                Batcher batcher = kvp.Value;
                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    // make a batch with our local time (double precision)
                    while (batcher.MakeNextBatch(writer, NetworkTime.localTime))
                    {
                        // validate packet before handing the batch to the
                        // transport. this guarantees that we always stay
                        // within transport's max message size limit.
                        // => just in case transport forgets to check it
                        // => just in case mirror miscalulated it etc.
                        ArraySegment<byte> segment = writer.ToArraySegment();
                        if (ValidatePacketSize(segment, kvp.Key))
                        {
                            // send to transport
                            SendToTransport(segment, kvp.Key);
                            //UnityEngine.Debug.Log($"sending batch of {writer.Position} bytes for channel={kvp.Key} connId={connectionId}");

                            // reset writer for each new batch
                            writer.Position = 0;
                        }
                    }
                }
            }
        }

        /// <summary>Disconnects this connection.</summary>
        // for future reference, here is how Disconnects work in Mirror.
        //
        // first, there are two types of disconnects:
        // * voluntary: the other end simply disconnected
        // * involuntary: server disconnects a client by itself
        //
        // UNET had special (complex) code to handle both cases differently.
        //
        // Mirror handles both cases the same way:
        // * Disconnect is called from TOP to BOTTOM
        //   NetworkServer/Client -> NetworkConnection -> Transport.Disconnect()
        // * Disconnect is handled from BOTTOM to TOP
        //   Transport.OnDisconnected -> ...
        //
        // in other words, calling Disconnect() does no cleanup whatsoever.
        // it simply asks the transport to disconnect.
        // then later the transport events will do the clean up.
        public abstract void Disconnect();

        public override string ToString() => $"connection({connectionId})";

        // TODO move to server's NetworkConnectionToClient?
        internal void AddToObserving(NetworkIdentity netIdentity)
        {
            observing.Add(netIdentity);

            // spawn identity for this conn
            NetworkServer.ShowForConnection(netIdentity, this);
        }

        // TODO move to server's NetworkConnectionToClient?
        internal void RemoveFromObserving(NetworkIdentity netIdentity, bool isDestroyed)
        {
            observing.Remove(netIdentity);

            if (!isDestroyed)
            {
                // hide identity for this conn
                NetworkServer.HideForConnection(netIdentity, this);
            }
        }

        // TODO move to server's NetworkConnectionToClient?
        internal void RemoveFromObservingsObservers()
        {
            foreach (NetworkIdentity netIdentity in observing)
            {
                netIdentity.RemoveObserver(this);
            }
            observing.Clear();
        }

        /// <summary>Check if we received a message within the last 'timeout' seconds.</summary>
        internal virtual bool IsAlive(float timeout) => Time.time - lastMessageTime < timeout;

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Add(obj);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Remove(obj);
        }

        internal void DestroyOwnedObjects()
        {
            // create a copy because the list might be modified when destroying
            HashSet<NetworkIdentity> tmp = new HashSet<NetworkIdentity>(clientOwnedObjects);
            foreach (NetworkIdentity netIdentity in tmp)
            {
                if (netIdentity != null)
                {
                    NetworkServer.Destroy(netIdentity.gameObject);
                }
            }

            // clear the hashset because we destroyed them all
            clientOwnedObjects.Clear();
        }
    }
}
