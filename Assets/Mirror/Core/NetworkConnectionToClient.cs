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

        public virtual string address { get; private set; }

        /// <summary>NetworkIdentities that this connection can see</summary>
        // TODO move to server's NetworkConnectionToClient?
        public readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

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

        // ping for rtt (round trip time)
        // useful for statistics, lag compensation, etc.
        double lastPingTime = 0;
        internal ExponentialMovingAverage _rtt = new ExponentialMovingAverage(NetworkTime.PingWindowSize);

        /// <summary>Round trip time (in seconds) that it takes a message to go server->client->server.</summary>
        public double rtt => _rtt.Value;

        public NetworkConnectionToClient(int networkConnectionId, string clientAddress = "localhost")
            : base(networkConnectionId)
        {
            address = clientAddress;

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
                NetworkClient.snapshotSettings.bufferLimit,
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

        protected virtual void UpdatePing()
        {
            // localTime (double) instead of Time.time for accuracy over days
            if (NetworkTime.localTime >= lastPingTime + NetworkTime.PingInterval)
            {
                // TODO it would be safer for the server to store the last N
                // messages' timestamp and only send a message number.
                // This way client's can't just modify the timestamp.
                // predictedTime parameter is 0 because the server doesn't predict.
                NetworkPingMessage pingMessage = new NetworkPingMessage(NetworkTime.localTime, 0);
                Send(pingMessage, Channels.Unreliable);
                lastPingTime = NetworkTime.localTime;
            }
        }

        internal override void Update()
        {
            UpdatePing();
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
                    // disown scene objects, destroy instantiated objects.
                    if (netIdentity.sceneId != 0)
                        NetworkServer.RemovePlayerForConnection(this, RemovePlayerOptions.KeepActive);
                    else
                        NetworkServer.Destroy(netIdentity.gameObject);
                }
            }

            // clear the hashset because we destroyed them all
            owned.Clear();
        }
    }
}
