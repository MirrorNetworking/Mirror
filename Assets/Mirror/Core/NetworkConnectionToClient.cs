using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        // rpcs are collected in a buffer, and then flushed out together.
        // this way we don't need one NetworkMessage per rpc.
        // => prepares for LocalWorldState as well.
        // ensure max size when adding!
        readonly NetworkWriter reliableRpcs = new NetworkWriter();
        readonly NetworkWriter unreliableRpcs = new NetworkWriter();

        public virtual string address => Transport.active.ServerGetClientAddress(connectionId);

        /// <summary>NetworkIdentities that this connection can see</summary>
        // TODO move to server's NetworkConnectionToClient?
        public readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

        // Deprecated 2022-10-13
        [Obsolete(".clientOwnedObjects was renamed to .owned :)")]
        public HashSet<NetworkIdentity> clientOwnedObjects => owned;

        // unbatcher
        public Unbatcher unbatcher = new Unbatcher();

        // server runs a time snapshot interpolation for each client's local time.
        // this is necessary for client auth movement to still be smooth on the
        // server for host mode.
        // TODO move them along server's timeline in the future.
        //      perhaps with an offset.
        //      for now, keep compatibility by manually constructing a timeline.
        ExponentialMovingAverage driftEma;
        ExponentialMovingAverage deliveryTimeEma; // average delivery time (standard deviation gives average jitter)
        public double remoteTimeline;
        public double remoteTimescale;
        double bufferTimeMultiplier = 2;
        double bufferTime => NetworkServer.sendInterval * bufferTimeMultiplier;

        // <clienttime, snaps>
        readonly SortedList<double, TimeSnapshot> snapshots = new SortedList<double, TimeSnapshot>();

        // Snapshot Buffer size limit to avoid ever growing list memory consumption attacks from clients.
        public int snapshotBufferSizeLimit = 64;

        public NetworkConnectionToClient(int networkConnectionId)
            : base(networkConnectionId)
        {
            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            driftEma = new ExponentialMovingAverage(NetworkServer.sendRate * NetworkClient.snapshotSettings.driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(NetworkServer.sendRate * NetworkClient.snapshotSettings.deliveryTimeEmaDuration);

            // buffer limit should be at least multiplier to have enough in there
            snapshotBufferSizeLimit = Mathf.Max((int)NetworkClient.snapshotSettings.bufferTimeMultiplier, snapshotBufferSizeLimit);
        }

        public void OnTimeSnapshot(TimeSnapshot snapshot)
        {
            // protect against ever growing buffer size attacks
            if (snapshots.Count >= snapshotBufferSizeLimit) return;

            // (optional) dynamic adjustment
            if (NetworkClient.snapshotSettings.dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    NetworkServer.sendInterval,
                    deliveryTimeEma.StandardDeviation,
                    NetworkClient.snapshotSettings.dynamicAdjustmentTolerance
                );
                // Debug.Log($"[Server]: {name} delivery std={serverDeliveryTimeEma.StandardDeviation} bufferTimeMult := {bufferTimeMultiplier} ");
            }

            // insert into the server buffer & initialize / adjust / catchup
            SnapshotInterpolation.InsertAndAdjust(
                snapshots,
                snapshot,
                ref remoteTimeline,
                ref remoteTimescale,
                NetworkServer.sendInterval,
                bufferTime,
                NetworkClient.snapshotSettings.catchupSpeed,
                NetworkClient.snapshotSettings.slowdownSpeed,
                ref driftEma,
                NetworkClient.snapshotSettings.catchupNegativeThreshold,
                NetworkClient.snapshotSettings.catchupPositiveThreshold,
                ref deliveryTimeEma
            );
        }

        public void UpdateTimeInterpolation()
        {
            // timeline starts when the first snapshot arrives.
            if (snapshots.Count > 0)
            {
                // progress local timeline.
                SnapshotInterpolation.StepTime(Time.unscaledDeltaTime, ref remoteTimeline, remoteTimescale);

                // progress local interpolation.
                // TimeSnapshot doesn't interpolate anything.
                // this is merely to keep removing older snapshots.
                SnapshotInterpolation.StepInterpolation(snapshots, remoteTimeline, out _, out _, out _);
                // Debug.Log($"NetworkClient SnapshotInterpolation @ {localTimeline:F2} t={t:F2}");
            }
        }

        // Send stage three: hand off to transport
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            Transport.active.ServerSend(connectionId, segment, channelId);

        void FlushRpcs(NetworkWriter buffer, int channelId)
        {
            if (buffer.Position > 0)
            {
                Send(new RpcBufferMessage { payload = buffer }, channelId);
                buffer.Position = 0;
            }
        }

        // helper for both channels
        void BufferRpc(RpcMessage message, NetworkWriter buffer, int channelId, int maxMessageSize)
        {
            // calculate buffer limit. we can only fit so much into a message.
            // max - message header - WriteArraySegment size header - batch header
            int bufferLimit = maxMessageSize - NetworkMessages.IdSize - sizeof(int) - Batcher.HeaderSize;

            // remember previous valid position
            int before = buffer.Position;

            // serialize the message without header
            buffer.Write(message);

            // before we potentially flush out old messages,
            // let's ensure this single message can even fit the limit.
            // otherwise no point in flushing.
            int messageSize = buffer.Position - before;
            if (messageSize > bufferLimit)
            {
                Debug.LogWarning($"NetworkConnectionToClient: discarded RpcMesage for netId={message.netId} componentIndex={message.componentIndex} functionHash={message.functionHash} because it's larger than the rpc buffer limit of {bufferLimit} bytes for the channel: {channelId}");
                return;
            }

            // too much to fit into max message size?
            // then flush first, then write it again.
            // (message + message header + 4 bytes WriteArraySegment header)
            if (buffer.Position > bufferLimit)
            {
                buffer.Position = before;
                FlushRpcs(buffer, channelId); // this resets position
                buffer.Write(message);
            }
        }

        internal void BufferRpc(RpcMessage message, int channelId)
        {
            int maxMessageSize = Transport.active.GetMaxPacketSize(channelId);
            if (channelId == Channels.Reliable)
            {
                BufferRpc(message, reliableRpcs, Channels.Reliable, maxMessageSize);
            }
            else if (channelId == Channels.Unreliable)
            {
                BufferRpc(message, unreliableRpcs, Channels.Unreliable, maxMessageSize);
            }
        }

        internal override void Update()
        {
            // send rpc buffers
            FlushRpcs(reliableRpcs, Channels.Reliable);
            FlushRpcs(unreliableRpcs, Channels.Unreliable);

            // call base update to flush out batched messages
            base.Update();
        }

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            reliableRpcs.Position = 0;
            unreliableRpcs.Position = 0;
            Transport.active.ServerDisconnect(connectionId);

            // IMPORTANT: NetworkConnection.Disconnect() is NOT called for
            // voluntary disconnects from the other end.
            // -> so all 'on disconnect' cleanup code needs to be in
            //    OnTransportDisconnect, where it's called for both voluntary
            //    and involuntary disconnects!
        }

        internal void AddToObserving(NetworkIdentity netIdentity)
        {
            observing.Add(netIdentity);

            // spawn identity for this conn
            NetworkServer.ShowForConnection(netIdentity, this);
        }

        internal void RemoveFromObserving(NetworkIdentity netIdentity, bool isDestroyed)
        {
            observing.Remove(netIdentity);

            if (!isDestroyed)
            {
                // hide identity for this conn
                NetworkServer.HideForConnection(netIdentity, this);
            }
        }

        internal void RemoveFromObservingsObservers()
        {
            foreach (NetworkIdentity netIdentity in observing)
            {
                netIdentity.RemoveObserver(this);
            }
            observing.Clear();
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            owned.Add(obj);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            owned.Remove(obj);
        }

        internal void DestroyOwnedObjects()
        {
            // create a copy because the list might be modified when destroying
            HashSet<NetworkIdentity> tmp = new HashSet<NetworkIdentity>(owned);
            foreach (NetworkIdentity netIdentity in tmp)
            {
                if (netIdentity != null)
                {
                    NetworkServer.Destroy(netIdentity.gameObject);
                }
            }

            // clear the hashset because we destroyed them all
            owned.Clear();
        }
    }
}
