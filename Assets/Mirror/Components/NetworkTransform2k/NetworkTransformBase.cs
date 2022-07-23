// NetworkTransform V2 aka project Oumuamua by vis2k (2021-07)
// Snapshot Interpolation: https://gafferongames.com/post/snapshot_interpolation/
//
// Base class for NetworkTransform and NetworkTransformChild.
// => simple unreliable sync without any interpolation for now.
// => which means we don't need teleport detection either
//
// NOTE: several functions are virtual in case someone needs to modify a part.
//
// Channel: uses UNRELIABLE at all times.
// -> out of order packets are dropped automatically
// -> it's better than RELIABLE for several reasons:
//    * head of line blocking would add delay
//    * resending is mostly pointless
//    * bigger data race:
//      -> if we use a Cmd() at position X over reliable
//      -> client gets Cmd() and X at the same time, but buffers X for bufferTime
//      -> for unreliable, it would get X before the reliable Cmd(), still
//         buffer for bufferTime but end up closer to the original time
// comment out the below line to quickly revert the onlySyncOnChange feature
#define onlySyncOnChange_BANDWIDTH_SAVING
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        // TODO SyncDirection { CLIENT_TO_SERVER, SERVER_TO_CLIENT } is easier?
        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        protected bool IsClientWithAuthority => hasAuthority && clientAuthority;

        // target transform to sync. can be on a child.
        protected abstract Transform targetComponent { get; }

        [Header("Synchronization")]
        [Tooltip("Send N snapshots per second. Multiples of frame rate make sense.")]
        public int sendRate = 30; // in Hz. easier to work with as int for EMA. easier to display '30' than '0.333333333'
        public float sendInterval => 1f / sendRate;

        // decrease bufferTime at runtime to see the catchup effect.
        // increase to see slowdown.
        // 'double' so we can have very precise dynamic adjustment without rounding
        [Header("Buffering")]
        [Tooltip("Local simulation is behind by sendInterval * multiplier seconds.\n\nThis guarantees that we always have enough snapshots in the buffer to mitigate lags & jitter.\n\nIncrease this if the simulation isn't smooth. By default, it should be around 2.")]
        public double bufferTimeMultiplier = 2;
        public double bufferTime => sendInterval * bufferTimeMultiplier;

        [Tooltip("Buffer size limit to avoid ever growing list memory consumption attacks.")]
        public int bufferSizeLimit = 64;

        // catchup /////////////////////////////////////////////////////////////
        // catchup thresholds in 'frames'.
        // half a frame might be too aggressive.
        [Header("Catchup / Slowdown")]
        [Tooltip("Slowdown begins when the local timeline is moving too fast towards remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be negative.\n\nDon't modify unless you know what you are doing.")]
        public float catchupNegativeThreshold = -1; // careful, don't want to run out of snapshots

        [Tooltip("Catchup begins when the local timeline is moving too slow and getting too far away from remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be positive.\n\nDon't modify unless you know what you are doing.")]
        public float catchupPositiveThreshold =  1;

        [Tooltip("Local timeline acceleration in % while catching up.")]
        [Range(0, 1)]
        public double catchupSpeed = 0.01f;  // 1%

        [Tooltip("Local timeline slowdown in % while slowing down.")]
        [Range(0, 1)]
        public double slowdownSpeed = 0.01f; // 1%

        [Tooltip("Catchup/Slowdown is adjusted over n-second exponential moving average.")]
        public int driftEmaDuration = 1; // shouldn't need to modify this, but expose it anyway

        // we use EMA to average the last second worth of snapshot time diffs.
        // manually averaging the last second worth of values with a for loop
        // would be the same, but a moving average is faster because we only
        // ever add one value.
        ExponentialMovingAverage serverDriftEma;
        ExponentialMovingAverage clientDriftEma;

        // dynamic buffer time adjustment //////////////////////////////////////
        // dynamically adjusts bufferTimeMultiplier for smooth results.
        // to understand how this works, try this manually:
        //
        // - disable dynamic adjustment
        // - set jitter = 0.2 (20% is a lot!)
        // - notice some stuttering
        // - disable interpolation to see just how much jitter this really is(!)
        // - enable interpolation again
        // - manually increase bufferTimeMultiplier to 3-4
        //   ... the cube slows down (blue) until it's smooth
        // - with dynamic adjustment enabled, it will set 4 automatically
        //   ... the cube slows down (blue) until it's smooth as well
        //
        // note that 20% jitter is extreme.
        // for this to be perfectly smooth, set the safety tolerance to '2'.
        // but realistically this is not necessary, and '1' is enough.
        [Header("Dynamic Adjustment")]
        [Tooltip("Automatically adjust bufferTimeMultiplier for smooth results.\nSets a low multiplier on stable connections, and a high multiplier on jittery connections.")]
        public bool dynamicAdjustment = true;

        [Tooltip("Safety buffer that is always added to the dynamic bufferTimeMultiplier adjustment.")]
        public float dynamicAdjustmentTolerance = 1; // 1 is realistically just fine, 2 is very very safe even for 20% jitter. can be half a frame too. (see above comments)

        [Tooltip("Dynamic adjustment is computed over n-second exponential moving average standard deviation.")]
        public int deliveryTimeEmaDuration = 2;      // 1-2s recommended to capture average delivery time

        ExponentialMovingAverage serverDeliveryTimeEma; // average delivery time (standard deviation gives average jitter)
        ExponentialMovingAverage clientDeliveryTimeEma; // average delivery time (standard deviation gives average jitter)

        // buffers & time //////////////////////////////////////////////////////
        // snapshots sorted by timestamp
        // in the original article, glenn fiedler drops any snapshots older than
        // the last received snapshot.
        // -> instead, we insert into a sorted buffer
        // -> the higher the buffer information density, the better
        // -> we still drop anything older than the first element in the buffer
        // => internal for testing
        //
        // IMPORTANT: of explicit 'NTSnapshot' type instead of 'Snapshot'
        //            interface because List<interface> allocates through boxing
        internal SortedList<double, NTSnapshot> serverSnapshots = new SortedList<double, NTSnapshot>();
        internal SortedList<double, NTSnapshot> clientSnapshots = new SortedList<double, NTSnapshot>();

        // only convert the static Interpolation function to Func<T> once to
        // avoid allocations
        Func<NTSnapshot, NTSnapshot, double, NTSnapshot> Interpolate = NTSnapshot.Interpolate;

        // for smooth interpolation, we need to interpolate along server time.
        // any other time (arrival on client, client local time, etc.) is not
        // going to give smooth results.
        double serverTimeline;
        double serverTimescale;

        // catchup / slowdown adjustments are applied to timescale,
        // to be adjusted in every update instead of when receiving messages.
        double clientTimeline;
        double clientTimescale;

        // only sync when changed hack /////////////////////////////////////////
#if onlySyncOnChange_BANDWIDTH_SAVING
        [Header("Sync Only If Changed")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;

        // 3 was original, but testing under really bad network conditions, 2%-5% packet loss and 250-1200ms ping, 5 proved to eliminate any twitching.
        [Tooltip("How much time, as a multiple of send interval, has passed before clearing buffers.")]
        public float bufferResetMultiplier = 5;

        [Header("Sensitivity"), Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float positionSensitivity = 0.01f;
        public float rotationSensitivity = 0.01f;
        public float scaleSensitivity = 0.01f;

        protected bool positionChanged;
        protected bool rotationChanged;
        protected bool scaleChanged;

        // Used to store last sent snapshots
        protected NTSnapshot lastSnapshot;
        protected bool cachedSnapshotComparison;
        protected bool hasSentUnchangedPosition;
#endif
        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync & interpolation")]
        public bool syncPosition = true;
        public bool syncRotation = true;
        public bool syncScale = false; // rare. off by default.

        double lastClientSendTime;
        double lastServerSendTime;

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public bool showGizmos;
        public bool showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // initialization //////////////////////////////////////////////////////
        // make sure to call this when inheriting too!
        protected virtual void Awake()
        {
            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            serverDriftEma = new ExponentialMovingAverage(sendRate * driftEmaDuration);
            clientDriftEma = new ExponentialMovingAverage(sendRate * driftEmaDuration);
            serverDeliveryTimeEma = new ExponentialMovingAverage(sendRate * deliveryTimeEmaDuration);
            clientDeliveryTimeEma = new ExponentialMovingAverage(sendRate * deliveryTimeEmaDuration);
        }

        // snapshot functions //////////////////////////////////////////////////
        // construct a snapshot of the current state
        // => internal for testing
        protected virtual NTSnapshot ConstructSnapshot()
        {
            // NetworkTime.localTime for double precision until Unity has it too
            return new NTSnapshot(
                // our local time is what the other end uses as remote time
                NetworkTime.localTime,
                // the other end fills out local time itself
                0,
                targetComponent.localPosition,
                targetComponent.localRotation,
                targetComponent.localScale
            );
        }

        // apply a snapshot to the Transform.
        // -> start, end, interpolated are all passed in caes they are needed
        // -> a regular game would apply the 'interpolated' snapshot
        // -> a board game might want to jump to 'goal' directly
        // (it's easier to always interpolate and then apply selectively,
        //  instead of manually interpolating x, y, z, ... depending on flags)
        // => internal for testing
        //
        // NOTE: stuck detection is unnecessary here.
        //       we always set transform.position anyway, we can't get stuck.
        protected virtual void ApplySnapshot(NTSnapshot interpolated)
        {
            // local position/rotation for VR support
            //
            // if syncPosition/Rotation/Scale is disabled then we received nulls
            // -> current position/rotation/scale would've been added as snapshot
            // -> we still interpolated
            // -> but simply don't apply it. if the user doesn't want to sync
            //    scale, then we should not touch scale etc.
            if (syncPosition)
                targetComponent.localPosition = interpolated.position;

            if (syncRotation)
                targetComponent.localRotation = interpolated.rotation;

            if (syncScale)
                targetComponent.localScale = interpolated.scale;
        }

#if onlySyncOnChange_BANDWIDTH_SAVING
        // Returns true if position, rotation AND scale are unchanged, within given sensitivity range.
        protected virtual bool CompareSnapshots(NTSnapshot currentSnapshot)
        {
            positionChanged = Vector3.SqrMagnitude(lastSnapshot.position - currentSnapshot.position) > positionSensitivity * positionSensitivity;
            rotationChanged = Quaternion.Angle(lastSnapshot.rotation, currentSnapshot.rotation) > rotationSensitivity;
            scaleChanged = Vector3.SqrMagnitude(lastSnapshot.scale - currentSnapshot.scale) > scaleSensitivity * scaleSensitivity;

            return (!positionChanged && !rotationChanged && !scaleChanged);
        }
#endif
        // cmd /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            OnClientToServerSync(position, rotation, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (clientAuthority)
            {
                RpcServerToClientSync(position, rotation, scale);
            }
        }

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (!clientAuthority) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= bufferSizeLimit) return;

            // only player owned objects (with a connection) can send to
            // server. we can get the timestamp from the connection.
            double timestamp = connectionToClient.remoteTimeStamp;
#if onlySyncOnChange_BANDWIDTH_SAVING
            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendInterval;

                if (serverSnapshots.Count > 0 && serverSnapshots.Values[serverSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                {
                    Reset();
                }
            }
#endif
            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.
            if (!position.HasValue) position = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].position : targetComponent.localPosition;
            if (!rotation.HasValue) rotation = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].rotation : targetComponent.localRotation;
            if (!scale.HasValue) scale = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].scale : targetComponent.localScale;

            // construct snapshot with batch timestamp to save bandwidth
            NTSnapshot snapshot = new NTSnapshot(
                timestamp,
                NetworkTime.localTime,
                position.Value, rotation.Value, scale.Value
            );

            // (optional) dynamic adjustment
            if (dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    sendInterval,
                    serverDeliveryTimeEma.StandardDeviation,
                    dynamicAdjustmentTolerance
                );
                // Debug.Log($"[Server]: {name} delivery std={serverDeliveryTimeEma.StandardDeviation} bufferTimeMult := {bufferTimeMultiplier} ");
            }

            // insert into the server buffer & initialize / adjust / catchup
            SnapshotInterpolation.Insert(
                serverSnapshots,
                snapshot,
                ref serverTimeline,
                ref serverTimescale,
                sendInterval,
                bufferTime,
                catchupSpeed,
                slowdownSpeed,
                ref serverDriftEma,
                catchupNegativeThreshold,
                catchupPositiveThreshold,
                ref serverDeliveryTimeEma
            );
        }

        // rpc /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale) =>
            OnServerToClientSync(position, rotation, scale);

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // in host mode, the server sends rpcs to all clients.
            // the host client itself will receive them too.
            // -> host server is always the source of truth
            // -> we can ignore any rpc on the host client
            // => otherwise host objects would have ever growing clientBuffers
            // (rpc goes to clients. if isServer is true too then we are host)
            if (isServer) return;

            // don't apply for local player with authority
            if (IsClientWithAuthority) return;

            // protect against ever growing buffer size attacks
            if (clientSnapshots.Count >= bufferSizeLimit) return;

            // on the client, we receive rpcs for all entities.
            // not all of them have a connectionToServer.
            // but all of them go through NetworkClient.connection.
            // we can get the timestamp from there.
            double timestamp = NetworkClient.connection.remoteTimeStamp;
#if onlySyncOnChange_BANDWIDTH_SAVING
            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendInterval;

                if (clientSnapshots.Count > 0 && clientSnapshots.Values[clientSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                {
                    Reset();
                }
            }
#endif
            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.
            if (!position.HasValue) position = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].position : targetComponent.localPosition;
            if (!rotation.HasValue) rotation = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].rotation : targetComponent.localRotation;
            if (!scale.HasValue) scale = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].scale : targetComponent.localScale;

            // construct snapshot with batch timestamp to save bandwidth
            NTSnapshot snapshot = new NTSnapshot(
                timestamp,
                NetworkTime.localTime,
                position.Value, rotation.Value, scale.Value
            );

            // (optional) dynamic adjustment
            if (dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    sendInterval,
                    clientDeliveryTimeEma.StandardDeviation,
                    dynamicAdjustmentTolerance
                );
                // Debug.Log($"[Client]: {name} delivery std={clientDeliveryTimeEma.StandardDeviation} bufferTimeMult := {bufferTimeMultiplier} ");
            }

            // insert into the client buffer & initialize / adjust / catchup
            SnapshotInterpolation.Insert(
                clientSnapshots,
                snapshot,
                ref clientTimeline,
                ref clientTimescale,
                sendInterval,
                bufferTime,
                catchupSpeed,
                slowdownSpeed,
                ref clientDriftEma,
                catchupNegativeThreshold,
                catchupPositiveThreshold,
                ref clientDeliveryTimeEma
            );
        }

        // update //////////////////////////////////////////////////////////////
        void UpdateServer()
        {
            // broadcast to all clients each 'sendInterval'
            // (client with authority will drop the rpc)
            // NetworkTime.localTime for double precision until Unity has it too
            //
            // IMPORTANT:
            // snapshot interpolation requires constant sending.
            // DO NOT only send if position changed. for example:
            // ---
            // * client sends first position at t=0
            // * ... 10s later ...
            // * client moves again, sends second position at t=10
            // ---
            // * server gets first position at t=0
            // * server gets second position at t=10
            // * server moves from first to second within a time of 10s
            //   => would be a super slow move, instead of a wait & move.
            //
            // IMPORTANT:
            // DO NOT send nulls if not changed 'since last send' either. we
            // send unreliable and don't know which 'last send' the other end
            // received successfully.
            //
            // Checks to ensure server only sends snapshots if object is
            // on server authority(!clientAuthority) mode because on client
            // authority mode snapshots are broadcasted right after the authoritative
            // client updates server in the command function(see above), OR,
            // since host does not send anything to update the server, any client
            // authoritative movement done by the host will have to be broadcasted
            // here by checking IsClientWithAuthority.
            if (NetworkTime.localTime >= lastServerSendTime + sendInterval &&
                (!clientAuthority || IsClientWithAuthority))
            {
                // send snapshot without timestamp.
                // receiver gets it from batch timestamp to save bandwidth.
                NTSnapshot snapshot = ConstructSnapshot();
#if onlySyncOnChange_BANDWIDTH_SAVING
                cachedSnapshotComparison = CompareSnapshots(snapshot);
                if (cachedSnapshotComparison && hasSentUnchangedPosition && onlySyncOnChange) { return; }
#endif

#if onlySyncOnChange_BANDWIDTH_SAVING
                RpcServerToClientSync(
                    // only sync what the user wants to sync
                    syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                    syncRotation && rotationChanged ? snapshot.rotation : default(Quaternion?),
                    syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                );
#else
                RpcServerToClientSync(
                    // only sync what the user wants to sync
                    syncPosition ? snapshot.position : default(Vector3?),
                    syncRotation ? snapshot.rotation : default(Quaternion?),
                    syncScale ? snapshot.scale : default(Vector3?)
                );
#endif

                lastServerSendTime = NetworkTime.localTime;
#if onlySyncOnChange_BANDWIDTH_SAVING
                if (cachedSnapshotComparison)
                {
                    hasSentUnchangedPosition = true;
                }
                else
                {
                    hasSentUnchangedPosition = false;
                    lastSnapshot = snapshot;
                }
#endif
            }

            // apply buffered snapshots IF client authority
            // -> in server authority, server moves the object
            //    so no need to apply any snapshots there.
            // -> don't apply for host mode player objects either, even if in
            //    client authority mode. if it doesn't go over the network,
            //    then we don't need to do anything.
            if (clientAuthority && !hasAuthority)
            {
                if (serverSnapshots.Count > 0)
                {
                    // compute snapshot interpolation & apply if any was spit out
                    if (SnapshotInterpolation.Step(
                        serverSnapshots,
                        Time.unscaledDeltaTime,
                        ref serverTimeline,
                        serverTimescale,
                        Interpolate,
                        out NTSnapshot computed))
                    {
                        ApplySnapshot(computed);
                    }
                }
            }
        }

        void UpdateClient()
        {
            // client authority, and local player (= allowed to move myself)?
            if (IsClientWithAuthority)
            {
                // https://github.com/vis2k/Mirror/pull/2992/
                if (!NetworkClient.ready) return;

                // send to server each 'sendInterval'
                // NetworkTime.localTime for double precision until Unity has it too
                //
                // IMPORTANT:
                // snapshot interpolation requires constant sending.
                // DO NOT only send if position changed. for example:
                // ---
                // * client sends first position at t=0
                // * ... 10s later ...
                // * client moves again, sends second position at t=10
                // ---
                // * server gets first position at t=0
                // * server gets second position at t=10
                // * server moves from first to second within a time of 10s
                //   => would be a super slow move, instead of a wait & move.
                //
                // IMPORTANT:
                // DO NOT send nulls if not changed 'since last send' either. we
                // send unreliable and don't know which 'last send' the other end
                // received successfully.
                if (NetworkTime.localTime >= lastClientSendTime + sendInterval)
                {
                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    NTSnapshot snapshot = ConstructSnapshot();
#if onlySyncOnChange_BANDWIDTH_SAVING
                    cachedSnapshotComparison = CompareSnapshots(snapshot);
                    if (cachedSnapshotComparison && hasSentUnchangedPosition && onlySyncOnChange) { return; }
#endif

#if onlySyncOnChange_BANDWIDTH_SAVING
                    CmdClientToServerSync(
                        // only sync what the user wants to sync
                        syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                        syncRotation && rotationChanged ? snapshot.rotation : default(Quaternion?),
                        syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                    );
#else
                    CmdClientToServerSync(
                        // only sync what the user wants to sync
                        syncPosition ? snapshot.position : default(Vector3?),
                        syncRotation ? snapshot.rotation : default(Quaternion?),
                        syncScale ? snapshot.scale : default(Vector3?)
                    );
#endif

                    lastClientSendTime = NetworkTime.localTime;
#if onlySyncOnChange_BANDWIDTH_SAVING
                    if (cachedSnapshotComparison)
                    {
                        hasSentUnchangedPosition = true;
                    }
                    else
                    {
                        hasSentUnchangedPosition = false;
                        lastSnapshot = snapshot;
                    }
#endif
                }
            }
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer
            else
            {
                if (clientSnapshots.Count > 0)
                {
                    // compute snapshot interpolation & apply if any was spit out
                    if (SnapshotInterpolation.Step(
                        clientSnapshots,
                        Time.unscaledDeltaTime,
                        ref clientTimeline,
                        clientTimescale,
                        Interpolate,
                        out NTSnapshot computed))
                    {
                        ApplySnapshot(computed);
                    }
                }
            }
        }

        void Update()
        {
            // if server then always sync to others.
            if (isServer) UpdateServer();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClient();
        }

        // common Teleport code for client->server and server->client
        protected virtual void OnTeleport(Vector3 destination)
        {
            // reset any in-progress interpolation & buffers
            Reset();

            // set the new position.
            // interpolation will automatically continue.
            targetComponent.position = destination;

            // TODO
            // what if we still receive a snapshot from before the interpolation?
            // it could easily happen over unreliable.
            // -> maybe add destination as first entry?
        }

        // common Teleport code for client->server and server->client
        protected virtual void OnTeleport(Vector3 destination, Quaternion rotation)
        {
            // reset any in-progress interpolation & buffers
            Reset();

            // set the new position.
            // interpolation will automatically continue.
            targetComponent.position = destination;
            targetComponent.rotation = rotation;

            // TODO
            // what if we still receive a snapshot from before the interpolation?
            // it could easily happen over unreliable.
            // -> maybe add destination as first entry?
        }

        // server->client teleport to force position without interpolation.
        // otherwise it would interpolate to a (far away) new position.
        // => manually calling Teleport is the only 100% reliable solution.
        [ClientRpc]
        public void RpcTeleport(Vector3 destination)
        {
            // NOTE: even in client authority mode, the server is always allowed
            //       to teleport the player. for example:
            //       * CmdEnterPortal() might teleport the player
            //       * Some people use client authority with server sided checks
            //         so the server should be able to reset position if needed.

            // TODO what about host mode?
            OnTeleport(destination);
        }

        // server->client teleport to force position and rotation without interpolation.
        // otherwise it would interpolate to a (far away) new position.
        // => manually calling Teleport is the only 100% reliable solution.
        [ClientRpc]
        public void RpcTeleport(Vector3 destination, Quaternion rotation)
        {
            // NOTE: even in client authority mode, the server is always allowed
            //       to teleport the player. for example:
            //       * CmdEnterPortal() might teleport the player
            //       * Some people use client authority with server sided checks
            //         so the server should be able to reset position if needed.

            // TODO what about host mode?
            OnTeleport(destination, rotation);
        }

        // client->server teleport to force position without interpolation.
        // otherwise it would interpolate to a (far away) new position.
        // => manually calling Teleport is the only 100% reliable solution.
        [Command]
        public void CmdTeleport(Vector3 destination)
        {
            // client can only teleport objects that it has authority over.
            if (!clientAuthority) return;

            // TODO what about host mode?
            OnTeleport(destination);

            // if a client teleports, we need to broadcast to everyone else too
            // TODO the teleported client should ignore the rpc though.
            //      otherwise if it already moved again after teleporting,
            //      the rpc would come a little bit later and reset it once.
            // TODO or not? if client ONLY calls Teleport(pos), the position
            //      would only be set after the rpc. unless the client calls
            //      BOTH Teleport(pos) and targetComponent.position=pos
            RpcTeleport(destination);
        }

        // client->server teleport to force position and rotation without interpolation.
        // otherwise it would interpolate to a (far away) new position.
        // => manually calling Teleport is the only 100% reliable solution.
        [Command]
        public void CmdTeleport(Vector3 destination, Quaternion rotation)
        {
            // client can only teleport objects that it has authority over.
            if (!clientAuthority) return;

            // TODO what about host mode?
            OnTeleport(destination, rotation);

            // if a client teleports, we need to broadcast to everyone else too
            // TODO the teleported client should ignore the rpc though.
            //      otherwise if it already moved again after teleporting,
            //      the rpc would come a little bit later and reset it once.
            // TODO or not? if client ONLY calls Teleport(pos), the position
            //      would only be set after the rpc. unless the client calls
            //      BOTH Teleport(pos) and targetComponent.position=pos
            RpcTeleport(destination, rotation);
        }

        public virtual void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();

            // reset interpolation time too so we start at t=0 next time
            serverTimeline  = 0;
            serverTimescale = 0;
            clientTimeline  = 0;
            clientTimescale = 0;
        }

        protected virtual void OnDisable() => Reset();
        protected virtual void OnEnable() => Reset();

        protected virtual void OnValidate()
        {
            // thresholds need to be <0 and >0
            catchupNegativeThreshold = Math.Min(catchupNegativeThreshold, 0);
            catchupPositiveThreshold = Math.Max(catchupPositiveThreshold, 0);

            // buffer limit should be at least multiplier to have enough in there
            bufferSizeLimit = Mathf.Max((int)bufferTimeMultiplier, bufferSizeLimit);
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                if (syncPosition) writer.WriteVector3(targetComponent.localPosition);
                if (syncRotation) writer.WriteQuaternion(targetComponent.localRotation);
                if (syncScale)    writer.WriteVector3(targetComponent.localScale);
                return true;
            }
            return false;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                if (syncPosition) targetComponent.localPosition = reader.ReadVector3();
                if (syncRotation) targetComponent.localRotation = reader.ReadQuaternion();
                if (syncScale)    targetComponent.localScale = reader.ReadVector3();
            }
        }

        // OnGUI allocates even if it does nothing. avoid in release.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // debug ///////////////////////////////////////////////////////////////
        protected virtual void OnGUI()
        {
            if (!showOverlay) return;

            // show data next to player for easier debugging. this is very useful!
            // IMPORTANT: this is basically an ESP hack for shooter games.
            //            DO NOT make this available with a hotkey in release builds
            if (!Debug.isDebugBuild) return;

            // project position to screen
            Vector3 point = Camera.main.WorldToScreenPoint(targetComponent.position);

            // enough alpha, in front of camera and in screen?
            if (point.z >= 0 && Utils.IsPointInScreen(point))
            {
                GUI.color = overlayColor;
                GUILayout.BeginArea(new Rect(point.x, Screen.height - point.y, 200, 100));

                // always show both client & server buffers so it's super
                // obvious if we accidentally populate both.
                if (serverSnapshots.Count > 0)
                {
                    GUILayout.Label($"Server Buffer:{serverSnapshots.Count}");
                    GUILayout.Label($"Server Timescale:{serverTimescale * 100:F2}%");
                }

                if (clientSnapshots.Count > 0)
                {
                    GUILayout.Label($"Client Buffer:{clientSnapshots.Count}");
                    GUILayout.Label($"Client Timescale:{clientTimescale * 100:F2}%");
                }

                GUILayout.EndArea();
                GUI.color = Color.white;
            }
        }

        protected virtual void DrawGizmos(SortedList<double, NTSnapshot> buffer)
        {
            // only draw if we have at least two entries
            if (buffer.Count < 2) return;

            // calculate threshold for 'old enough' snapshots
            double threshold = NetworkTime.localTime - bufferTime;
            Color oldEnoughColor = new Color(0, 1, 0, 0.5f);
            Color notOldEnoughColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            // draw the whole buffer for easier debugging.
            // it's worth seeing how much we have buffered ahead already
            for (int i = 0; i < buffer.Count; ++i)
            {
                // color depends on if old enough or not
                NTSnapshot entry = buffer.Values[i];
                bool oldEnough = entry.localTime <= threshold;
                Gizmos.color = oldEnough ? oldEnoughColor : notOldEnoughColor;
                Gizmos.DrawCube(entry.position, Vector3.one);
            }

            // extra: lines between start<->position<->goal
            Gizmos.color = Color.green;
            Gizmos.DrawLine(buffer.Values[0].position, targetComponent.position);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(targetComponent.position, buffer.Values[1].position);
        }

        protected virtual void OnDrawGizmos()
        {
            // This fires in edit mode but that spams NRE's so check isPlaying
            if (!Application.isPlaying) return;
            if (!showGizmos) return;

            if (isServer) DrawGizmos(serverSnapshots);
            if (isClient) DrawGizmos(clientSnapshots);
        }
#endif
    }
}
