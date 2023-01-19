// NetworkTransform V3 (reliable) by mischa (2022-10)
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Reliable)")]
    public class NetworkTransformReliable : NetworkTransformBase
    {
        [Header("Sync Only If Changed")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;
        [Tooltip("If we only sync on change, then we need to correct old snapshots if more time than sendInterval * multiplier has elapsed.\n\nOtherwise the first move will always start interpolating from the last move sequence's time, which will make it stutter when starting every time.")]
        public float onlySyncOnChangeCorrectionMultiplier = 2;

        [Header("Rotation")]
        [Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float rotationSensitivity = 0.01f;
        [Tooltip("Apply smallest-three quaternion compression. This is lossy, you can disable it if the small rotation inaccuracies are noticeable in your project.")]
        public bool compressRotation = false;

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
        public float scalePrecision    = 0.01f; // 1 cm

        // delta compression needs to remember 'last' to compress against
        protected Vector3Long lastSerializedPosition   = Vector3Long.zero;
        protected Vector3Long lastDeserializedPosition = Vector3Long.zero;

        protected Vector3Long lastSerializedScale      = Vector3Long.zero;
        protected Vector3Long lastDeserializedScale    = Vector3Long.zero;

        // Used to store last sent snapshots
        protected TransformSnapshot last;

        int lastClientCount = 0;

        // update //////////////////////////////////////////////////////////////
        void Update()
        {
            // if server then always sync to others.
            if      (isServer) UpdateServer();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClient();
        }

        void UpdateServer()
        {
            // set dirty to trigger OnSerialize. either always, or only if changed.
            // technically snapshot interpolation requires constant sending.
            // however, with reliable it should be fine without constant sends.
            if (!onlySyncOnChange || Changed(Construct()))
                SetDirty();

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
                    Apply(computed);
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

                // set dirty to trigger OnSerialize. either always, or only if changed.
                // technically snapshot interpolation requires constant sending.
                // however, with reliable it should be fine without constant sends.
                if (!onlySyncOnChange || Changed(Construct()))
                    SetDirty();
            }
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer
            else
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
                    Apply(computed);

                }

                // 'only sync if moved'
                // explain..
                // from 1 snap to next snap..
                // it'll be old...
                if (lastClientCount > 1 && clientSnapshots.Count == 1)
                {
                    // this is it. snapshots are down to '1'.
                    // does this cause stuck?
                }

                lastClientCount = clientSnapshots.Count;
            }
        }

        // check if position / rotation / scale changed since last sync
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

        // NT may be used on client/server/host to Owner/Observers with
        // ServerToClient or ClientToServer.
        // however, OnSerialize should always delta against last.
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

            // initial
            if (initialState)
            {
                if (syncPosition) writer.WriteVector3(snapshot.position);
                if (syncRotation)
                {
                    // (optional) smallest three compression for now. no delta.
                    if (compressRotation)
                        writer.WriteUInt(Compression.CompressQuaternion(snapshot.rotation));
                    else
                        writer.WriteQuaternion(snapshot.rotation);
                }
                if (syncScale)    writer.WriteVector3(snapshot.scale);
            }
            // delta
            else
            {
                // int before = writer.Position;

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
                        writer.WriteUInt(Compression.CompressQuaternion(snapshot.rotation));
                    else
                        writer.WriteQuaternion(snapshot.rotation);
                }
                if (syncScale)
                {
                    // quantize -> delta -> varint
                    Compression.ScaleToLong(snapshot.scale, scalePrecision, out Vector3Long quantized);
                    DeltaCompression.Compress(writer, lastSerializedScale, quantized);
                }

                // int written = writer.Position - before;
                // Debug.Log($"{name} compressed to {written} bytes");
            }

            // save serialized as 'last' for next delta compression
            if (syncPosition) Compression.ScaleToLong(snapshot.position, positionPrecision, out lastSerializedPosition);
            if (syncScale)    Compression.ScaleToLong(snapshot.scale, scalePrecision,    out lastSerializedScale);

            // set 'last'
            last = snapshot;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            Vector3?    position = null;
            Quaternion? rotation = null;
            Vector3?    scale = null;

            // initial
            if (initialState)
            {
                if (syncPosition) position = reader.ReadVector3();
                if (syncRotation)
                {
                    // (optional) smallest three compression for now. no delta.
                    if (compressRotation)
                        rotation = Compression.DecompressQuaternion(reader.ReadUInt());
                    else
                        rotation = reader.ReadQuaternion();
                }
                if (syncScale)    scale    = reader.ReadVector3();
            }
            // delta
            else
            {
                // varint -> delta -> quantize
                if (syncPosition)
                {
                    Vector3Long quantized = DeltaCompression.Decompress(reader, lastDeserializedPosition);
                    position = Compression.ScaleToFloat(quantized, positionPrecision);
                }
                if (syncRotation)
                {
                    // (optional) smallest three compression for now. no delta.
                    if (compressRotation)
                        rotation = Compression.DecompressQuaternion(reader.ReadUInt());
                    else
                        rotation = reader.ReadQuaternion();
                }
                if (syncScale)
                {
                    Vector3Long quantized = DeltaCompression.Decompress(reader, lastDeserializedScale);
                    scale = Compression.ScaleToFloat(quantized, scalePrecision);
                }
            }

            // handle depending on server / client / host.
            // server has priority for host mode.
            if      (isServer) OnClientToServerSync(position, rotation, scale);
            else if (isClient) OnServerToClientSync(position, rotation, scale);

            // save deserialized as 'last' for next delta compression
            if (syncPosition) Compression.ScaleToLong(position.Value, positionPrecision, out lastDeserializedPosition);
            if (syncScale)    Compression.ScaleToLong(scale.Value,    scalePrecision,    out lastDeserializedScale);
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
                NeedsCorrection(serverSnapshots, connectionToClient.remoteTimeStamp, NetworkServer.sendInterval, onlySyncOnChangeCorrectionMultiplier))
            {
                RewriteHistory(
                    serverSnapshots,
                    connectionToClient.remoteTimeStamp,
                    NetworkTime.localTime,      // arrival remote timestamp. NOT remote timeline.
                    NetworkServer.sendInterval, // Unity 2019 doesn't have timeAsDouble yet
                    target.localPosition,
                    target.localRotation,
                    target.localScale);
                // Debug.Log($"{name}: corrected history on server to fix initial stutter after not sending for a while.");
            }

            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp, position, rotation, scale);
        }

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // don't apply for local player with authority
            if (IsClientWithAuthority) return;

            // 'only sync on change' needs a correction on every new move sequence.
            if (onlySyncOnChange &&
                NeedsCorrection(clientSnapshots, NetworkClient.connection.remoteTimeStamp, NetworkClient.sendInterval, onlySyncOnChangeCorrectionMultiplier))
            {
                RewriteHistory(
                    clientSnapshots,
                    NetworkClient.connection.remoteTimeStamp, // arrival remote timestamp. NOT remote timeline.
                    NetworkTime.localTime,                    // Unity 2019 doesn't have timeAsDouble yet
                    NetworkClient.sendInterval,
                    target.localPosition,
                    target.localRotation,
                    target.localScale);
                // Debug.Log($"{name}: corrected history on client to fix initial stutter after not sending for a while.");
            }

            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp, position, rotation, scale);
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
            SnapshotInterpolation.InsertIfNotExists(snapshots, new TransformSnapshot(
                remoteTimeStamp - sendInterval, // arrival remote timestamp. NOT remote time.
                localTime - sendInterval,       // Unity 2019 doesn't have timeAsDouble yet
                position,
                rotation,
                scale
            ));
        }

        public override void Reset()
        {
            base.Reset();

            // reset delta
            lastSerializedPosition   = Vector3Long.zero;
            lastDeserializedPosition = Vector3Long.zero;

            lastSerializedScale   = Vector3Long.zero;
            lastDeserializedScale = Vector3Long.zero;
        }
    }
}
