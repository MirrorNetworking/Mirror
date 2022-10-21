using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public override string address =>
            Transport.active.ServerGetClientAddress(connectionId);

        /// <summary>NetworkIdentities that this connection can see</summary>
        // TODO move to server's NetworkConnectionToClient?
        public new readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

        [Obsolete(".clientOwnedObjects was renamed to .owned :)")] // 2022-10-13
        public new HashSet<NetworkIdentity> clientOwnedObjects => owned;

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
            driftEma        = new ExponentialMovingAverage(NetworkServer.sendRate * NetworkClient.driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(NetworkServer.sendRate * NetworkClient.deliveryTimeEmaDuration);

            // buffer limit should be at least multiplier to have enough in there
            snapshotBufferSizeLimit = Mathf.Max((int)NetworkClient.bufferTimeMultiplier, snapshotBufferSizeLimit);
        }

        public void OnTimeSnapshot(TimeSnapshot snapshot)
        {
            // protect against ever growing buffer size attacks
            if (snapshots.Count >= snapshotBufferSizeLimit) return;

            // (optional) dynamic adjustment
            if (NetworkClient.dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    NetworkServer.sendInterval,
                    deliveryTimeEma.StandardDeviation,
                    NetworkClient.dynamicAdjustmentTolerance
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
                NetworkClient.catchupSpeed,
                NetworkClient.slowdownSpeed,
                ref driftEma,
                NetworkClient.catchupNegativeThreshold,
                NetworkClient.catchupPositiveThreshold,
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

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
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
