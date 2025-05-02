// NetworkTransform V3 based on NetworkTransformUnreliable, using Mirror's new
// Unreliable quake style networking model with delta compression.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Hybrid)")]
    public class NetworkTransformHybrid : NetworkTransformBase
    {
        [Header("Additional Settings")]
        [Tooltip("If we only sync on change, then we need to correct old snapshots if more time than sendInterval * multiplier has elapsed.\n\nOtherwise the first move will always start interpolating from the last move sequence's time, which will make it stutter when starting every time.")]
        public float onlySyncOnChangeCorrectionMultiplier = 2;

        [Header("Rotation")]
        [Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float rotationSensitivity = 0.01f;

        // delta compression is capable of detecting byte-level changes.
        // if we scale float position to bytes,
        // then small movements will only change one byte.
        // this gives optimal bandwidth.
        //   benchmark with 0.01 precision: 130 KB/s => 60 KB/s
        //   benchmark with 0.1  precision: 130 KB/s => 30 KB/s
        [Header("Precision")]
        [Tooltip("Position is rounded in order to drastically minimize bandwidth.\n\nFor example, a precision of 0.01 rounds to a centimeter. In other words, sub-centimeter movements aren't synced until they eventually exceeded an actual centimeter.\n\nDepending on how important the object is, a precision of 0.01-0.10 (1-10 cm) is recommended.\n\nFor example, even a 1cm precision combined with delta compression cuts the Benchmark demo's bandwidth in half, compared to sending every tiny change.")]
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float positionPrecision = 0.01f; // 1 cm
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float rotationPrecision = 0.001f; // this is for the quaternion's components, needs to be small
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float scalePrecision = 0.01f; // 1 cm

        [Header("Debug")]
        public bool debugDraw = false;

        // delta compression needs to remember 'last' to compress against.
        // this is from reliable full state serializations, not from last
        // unreliable delta since that isn't guaranteed to be delivered.
        protected Vector3Long lastSerializedPosition = Vector3Long.zero;
        protected Vector3Long lastDeserializedPosition = Vector3Long.zero;

        protected Vector4Long lastSerializedRotation = Vector4Long.zero;
        protected Vector4Long lastDeserializedRotation = Vector4Long.zero;

        protected Vector3Long lastSerializedScale = Vector3Long.zero;
        protected Vector3Long lastDeserializedScale = Vector3Long.zero;

        // Used to store last sent snapshots
        protected TransformSnapshot last;

        // validation //////////////////////////////////////////////////////////
        // Configure is called from OnValidate and Awake
        protected override void Configure()
        {
            base.Configure();

            // force syncMethod to unreliable
            syncMethod = SyncMethod.Hybrid;

            // Unreliable ignores syncInterval. don't need to force anymore:
            // sendIntervalMultiplier = 1;
        }

        // update //////////////////////////////////////////////////////////////
        void Update()
        {
            if (updateMethod == UpdateMethod.Update)
                DoUpdate();
        }

        void FixedUpdate()
        {
            if (updateMethod == UpdateMethod.FixedUpdate)
                DoUpdate();

            if (pendingSnapshot.HasValue && !IsClientWithAuthority)
            {
                // Apply via base method, but in FixedUpdate
                Apply(pendingSnapshot.Value, pendingSnapshot.Value);
                pendingSnapshot = null;
            }
        }

        void LateUpdate()
        {
            if (updateMethod == UpdateMethod.LateUpdate)
                DoUpdate();

            // set dirty to trigger OnSerialize. either always, or only if changed.
            // It has to be checked in LateUpdate() for onlySyncOnChange to avoid
            // the possibility of Update() running first before the object's movement
            // script's Update(), which then causes NT to send every alternate frame
            // instead.
            if (isServer || (IsClientWithAuthority && NetworkClient.ready))
            {
                if (!onlySyncOnChange || Changed(Construct()))
                    SetDirty();
            }
        }

        void DoUpdate()
        {
            // if server then always sync to others.
            if (isServer) UpdateServer();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClient();
        }

        protected virtual void UpdateServer()
        {
            // apply buffered snapshots IF client authority
            // -> in server authority, server moves the object
            //    so no need to apply any snapshots there.
            // -> don't apply for host mode player objects either, even if in
            //    client authority mode. if it doesn't go over the network,
            //    then we don't need to do anything.
            // -> connectionToClient is briefly null after scene changes:
            //    https://github.com/MirrorNetworking/Mirror/issues/3329
            if (syncDirection == SyncDirection.ClientToServer &&
                connectionToClient != null &&
                !isOwned)
            {
                if (serverSnapshots.Count > 0)
                {
                    // step the transform interpolation without touching time.
                    // NetworkClient is responsible for time globally.
                    SnapshotInterpolation.StepInterpolation(
                        serverSnapshots,
                        connectionToClient.remoteTimeline,
                        out TransformSnapshot from,
                        out TransformSnapshot to,
                        out double t);

                    // interpolate & apply
                    TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);

                    if (updateMethod == UpdateMethod.FixedUpdate)
                        pendingSnapshot = computed;
                    else
                        Apply(computed, to);
                }
            }
        }

        protected virtual void UpdateClient()
        {
            // client authority, and local player (= allowed to move myself)?
            if (!IsClientWithAuthority)
            {
                // only while we have snapshots
                if (clientSnapshots.Count > 0)
                {
                    // step the interpolation without touching time.
                    // NetworkClient is responsible for time globally.
                    SnapshotInterpolation.StepInterpolation(
                        clientSnapshots,
                        NetworkTime.time, // == NetworkClient.localTimeline from snapshot interpolation
                        out TransformSnapshot from,
                        out TransformSnapshot to,
                        out double t);

                    // interpolate & apply
                    TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);

                    if (updateMethod == UpdateMethod.FixedUpdate)
                        pendingSnapshot = computed;
                    else
                        Apply(computed, to);

                    if (debugDraw)
                    {
                        Debug.DrawLine(from.position, to.position, Color.white, 10f);
                        Debug.DrawLine(computed.position, computed.position + Vector3.up, Color.white, 10f);
                    }
                }
            }
        }

        // check if position / rotation / scale changed since last _full reliable_ sync.
        protected virtual bool Changed(TransformSnapshot current) =>
            // position is quantized and delta compressed.
            // only consider it changed if the quantized representation is changed.
            // careful: don't use 'serialized / deserialized last'. as it depends on sync mode etc.
            QuantizedChanged(last.position, current.position, positionPrecision) ||
            // rotation isn't quantized / delta compressed.
            // check with sensitivity.
            Quaternion.Angle(last.rotation, current.rotation) > rotationSensitivity ||
            // scale is quantized and delta compressed.
            // only consider it changed if the quantized representation is changed.
            // careful: don't use 'serialized / deserialized last'. as it depends on sync mode etc.
            QuantizedChanged(last.scale, current.scale, scalePrecision);

        // helper function to compare quantized representations of a Vector3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool QuantizedChanged(Vector3 u, Vector3 v, float precision)
        {
            Compression.ScaleToLong(u, precision, out Vector3Long uQuantized);
            Compression.ScaleToLong(v, precision, out Vector3Long vQuantized);
            return uQuantized != vQuantized;
        }

        // Unreliable OnSerialize:
        // - initial=true  sends reliable full state
        // - initial=false sends unreliable delta states
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // get current snapshot for broadcasting.
            TransformSnapshot snapshot = Construct();

            // ClientToServer optimization:
            // for interpolated client owned identities,
            // always broadcast the latest known snapshot so other clients can
            // interpolate immediately instead of catching up too

            // TODO dirty mask? [compression is very good w/o it already]
            // each vector's component is delta compressed.
            // an unchanged component would still require 1 byte.
            // let's use a dirty bit mask to filter those out as well.

            // Debug.Log($"NT OnSerialize: initial={initialState} method={syncMethod}");

            // reliable full state
            if (initialState)
            {
                // TODO initialState is now sent multiple times. find a new fix for this:
                // If there is a last serialized snapshot, we use it.
                // This prevents the new client getting a snapshot that is different
                // from what the older clients last got. If this happens, and on the next
                // regular serialisation the delta compression will get wrong values.
                // Notes:
                // 1. Interestingly only the older clients have it wrong, because at the end
                //    of this function, last = snapshot which is the initial state's snapshot
                // 2. Regular NTR gets by this bug because it sends every frame anyway so initialstate
                //    snapshot constructed would have been the same as the last anyway.
                // if (last.remoteTime > 0) snapshot = last;

                int startPosition = writer.Position;


                if (syncPosition) writer.WriteVector3(snapshot.position);
                // note: smallest-three compression sends absolute values.
                // technically it doesn't need a baseline, but we still need to sync the initial spawn rotation.
                // in other words: alwyas sync the initial rotation as full quaternion.
                if (syncRotation) writer.WriteQuaternion(snapshot.rotation);
                if (syncScale) writer.WriteVector3(snapshot.scale);

                // save serialized as 'last' for next delta compression.
                // only for reliable full sync, since unreliable isn't guaranteed to arrive.
                if (syncPosition) Compression.ScaleToLong(snapshot.position, positionPrecision, out lastSerializedPosition);
                if (syncRotation && !compressRotation) Compression.ScaleToLong(snapshot.rotation, rotationPrecision, out lastSerializedRotation);
                if (syncScale) Compression.ScaleToLong(snapshot.scale, scalePrecision, out lastSerializedScale);

                // set 'last'
                last = snapshot;
            }
            // unreliable delta: compress against last full reliable state
            else
            {
                int startPosition = writer.Position;

                if (syncPosition)
                {
                    // quantize -> delta -> varint
                    Compression.ScaleToLong(snapshot.position, positionPrecision, out Vector3Long quantized);
                    DeltaCompression.Compress(writer, lastSerializedPosition, quantized);
                }
                if (syncRotation)
                {
                    // (optional) smallest three compression for now. no delta.
                    if (compressRotation)
                    {
                        writer.WriteUInt(Compression.CompressQuaternion(snapshot.rotation));
                    }
                    else
                    {
                        // quantize -> delta -> varint
                        // this works for quaternions too, where xyzw are [-1,1]
                        // and gradually change as rotation changes.
                        Compression.ScaleToLong(snapshot.rotation, rotationPrecision, out Vector4Long quantized);
                        DeltaCompression.Compress(writer, lastSerializedRotation, quantized);
                    }
                }
                if (syncScale)
                {
                    // quantize -> delta -> varint
                    Compression.ScaleToLong(snapshot.scale, scalePrecision, out Vector3Long quantized);
                    DeltaCompression.Compress(writer, lastSerializedScale, quantized);
                }
            }
        }

        // Unreliable OnDeserialize:
        // - initial=true  sends reliable full state
        // - initial=false sends unreliable delta states
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            Vector3? position = null;
            Quaternion? rotation = null;
            Vector3? scale = null;

            // reliable full state
            if (initialState)
            {
                if (syncPosition)
                {
                    position = reader.ReadVector3();

                    if (debugDraw) Debug.DrawLine(position.Value, position.Value + Vector3.up , Color.green, 10.0f);
                }
                if (syncRotation) rotation = reader.ReadQuaternion();
                if (syncScale) scale = reader.ReadVector3();

                // save deserialized as 'last' for next delta compression.
                // only for reliable full sync, since unreliable isn't guaranteed to arrive.
                if (syncPosition) Compression.ScaleToLong(position.Value, positionPrecision, out lastDeserializedPosition);
                if (syncRotation && !compressRotation) Compression.ScaleToLong(rotation.Value, rotationPrecision, out lastDeserializedRotation);
                if (syncScale) Compression.ScaleToLong(scale.Value, scalePrecision, out lastDeserializedScale);

                // apply initial values.
                // Mirror already syncs spawn position, but NT has a 'target' (i.e. child object)
                // who's initial position we need to set as well.
                if (isServer) OnClientToServerSync(position, rotation, scale);
                else if (isClient) OnServerToClientSync(position, rotation, scale);
            }
            // unreliable delta: decompress against last full reliable state
            else
            {
                // varint -> delta -> quantize
                if (syncPosition)
                {
                    Vector3Long quantized = DeltaCompression.Decompress(reader, lastDeserializedPosition);
                    position = Compression.ScaleToFloat(quantized, positionPrecision);

                    if (debugDraw) Debug.DrawLine(position.Value, position.Value + Vector3.up , Color.yellow, 10.0f);
                }
                if (syncRotation)
                {
                    // (optional) smallest three compression for now. no delta.
                    if (compressRotation)
                    {
                        rotation = Compression.DecompressQuaternion(reader.ReadUInt());
                    }
                    else
                    {
                        // varint -> delta -> quantize
                        // this works for quaternions too, where xyzw are [-1,1]
                        // and gradually change as rotation changes.
                        Vector4Long quantized = DeltaCompression.Decompress(reader, lastDeserializedRotation);
                        rotation = Compression.ScaleToFloat(quantized, rotationPrecision);
                    }
                }
                if (syncScale)
                {
                    Vector3Long quantized = DeltaCompression.Decompress(reader, lastDeserializedScale);
                    scale = Compression.ScaleToFloat(quantized, scalePrecision);
                }

                // handle depending on server / client / host.
                // server has priority for host mode.
                //
                // only do this for the unreliable delta states!
                // processing the reliable baselines shows noticeable jitter
                // around baseline syncs (e.g. tanks demo @ 4 Hz sendRate).
                // unreliable deltas are always within the same time delta,
                // so this gives perfectly smooth results.
                if (isServer) OnClientToServerSync(position, rotation, scale);
                else if (isClient) OnServerToClientSync(position, rotation, scale);
            }
        }

        // sync ////////////////////////////////////////////////////////////////

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // 'only sync on change' needs a correction on every new move sequence.
            if (onlySyncOnChange &&
                NeedsCorrection(serverSnapshots, connectionToClient.remoteTimeStamp, NetworkServer.sendInterval * sendIntervalMultiplier, onlySyncOnChangeCorrectionMultiplier))
            {
                RewriteHistory(
                    serverSnapshots,
                    connectionToClient.remoteTimeStamp,
                    NetworkTime.localTime,                                  // arrival remote timestamp. NOT remote timeline.
                    NetworkServer.sendInterval * sendIntervalMultiplier,    // Unity 2019 doesn't have timeAsDouble yet
                    GetPosition(),
                    GetRotation(),
                    GetScale());
            }

            // add a small timeline offset to account for decoupled arrival of
            // NetworkTime and NetworkTransform snapshots.
            // needs to be sendInterval. half sendInterval doesn't solve it.
            // https://github.com/MirrorNetworking/Mirror/issues/3427
            // remove this after LocalWorldState.
            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // don't apply for local player with authority
            if (IsClientWithAuthority) return;

            // 'only sync on change' needs a correction on every new move sequence.
            if (onlySyncOnChange &&
                NeedsCorrection(clientSnapshots, NetworkClient.connection.remoteTimeStamp, NetworkClient.sendInterval * sendIntervalMultiplier, onlySyncOnChangeCorrectionMultiplier))
            {
                RewriteHistory(
                    clientSnapshots,
                    NetworkClient.connection.remoteTimeStamp,               // arrival remote timestamp. NOT remote timeline.
                    NetworkTime.localTime,                                  // Unity 2019 doesn't have timeAsDouble yet
                    NetworkClient.sendInterval * sendIntervalMultiplier,
                    GetPosition(),
                    GetRotation(),
                    GetScale());
            }

            // add a small timeline offset to account for decoupled arrival of
            // NetworkTime and NetworkTransform snapshots.
            // needs to be sendInterval. half sendInterval doesn't solve it.
            // https://github.com/MirrorNetworking/Mirror/issues/3427
            // remove this after LocalWorldState.
            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

        // only sync on change /////////////////////////////////////////////////
        // snap interp. needs a continous flow of packets.
        // 'only sync on change' interrupts it while not changed.
        // once it restarts, snap interp. will interp from the last old position.
        // this will cause very noticeable stutter for the first move each time.
        // the fix is quite simple.

        // 1. detect if the remaining snapshot is too old from a past move.
        static bool NeedsCorrection(
            SortedList<double, TransformSnapshot> snapshots,
            double remoteTimestamp,
            double bufferTime,
            double toleranceMultiplier) =>
                snapshots.Count == 1 &&
                remoteTimestamp - snapshots.Keys[0] >= bufferTime * toleranceMultiplier;

        // 2. insert a fake snapshot at current position,
        //    exactly one 'sendInterval' behind the newly received one.
        static void RewriteHistory(
            SortedList<double, TransformSnapshot> snapshots,
            // timestamp of packet arrival, not interpolated remote time!
            double remoteTimeStamp,
            double localTime,
            double sendInterval,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            // clear the previous snapshot
            snapshots.Clear();

            // insert a fake one at where we used to be,
            // 'sendInterval' behind the new one.
            SnapshotInterpolation.InsertIfNotExists(
                snapshots,
                NetworkClient.snapshotSettings.bufferLimit,
                new TransformSnapshot(
                    remoteTimeStamp - sendInterval, // arrival remote timestamp. NOT remote time.
                    localTime - sendInterval,       // Unity 2019 doesn't have timeAsDouble yet
                    position,
                    rotation,
                    scale
                )
            );
        }

        // reset state for next session.
        // do not ever call this during a session (i.e. after teleport).
        // calling this will break delta compression.
        public override void ResetState()
        {
            base.ResetState();

            // reset delta
            lastSerializedPosition = Vector3Long.zero;
            lastDeserializedPosition = Vector3Long.zero;

            lastSerializedRotation = Vector4Long.zero;
            lastDeserializedRotation = Vector4Long.zero;

            lastSerializedScale = Vector3Long.zero;
            lastDeserializedScale = Vector3Long.zero;

            // reset 'last' for delta too
            last = new TransformSnapshot(0, 0, Vector3.zero, Quaternion.identity, Vector3.zero);
        }
    }
}
