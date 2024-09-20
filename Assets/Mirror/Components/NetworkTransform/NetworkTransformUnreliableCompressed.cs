// NetworkTransform V3 based on NetworkTransformUnreliable, using Mirror's new
// Unreliable quake style networking model with delta compression.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Unreliable Compressed)")]
    public class NetworkTransformUnreliableCompressed : NetworkTransformBase
    {
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

        protected Vector4Long lastSerializedRotation = Vector4Long.zero;
        protected Vector4Long lastDeserializedRotation = Vector4Long.zero;

        // Used to store last sent snapshots
        protected TransformSnapshot last;

        // validation //////////////////////////////////////////////////////////
        // Configure is called from OnValidate and Awake
        protected override void Configure()
        {
            base.Configure();

            // force syncMethod to unreliable
            syncMethod = SyncMethod.Unreliable;

            // Unreliable ignores syncInterval. don't need to force anymore:
            // sendIntervalMultiplier = 1;
        }

        // update //////////////////////////////////////////////////////////////
        void Update()
        {
            // if server then always sync to others.
            if (isServer) UpdateServer();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClient();
        }

        void LateUpdate()
        {
            SetDirty();
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
                    Apply(computed, to);
                }
            }
        }

        protected virtual void UpdateClient()
        {
            // client authority, and local player (= allowed to move myself)?
            if (!IsClientWithAuthority)
            {
                float timeSpacing = 10.0f;

                // DEBUG TIME: every update we draw a line to show where Time.time is.
                Vector3 timeVector = new Vector3(Time.time * timeSpacing, 0, 0);
                Debug.DrawLine(timeVector, timeVector + Vector3.up, Color.red, 10000f);

                // DEBUG TIME: every update we draw a line to show where NetworkTime.time is.
                Vector3 networkTimeVector = new Vector3((float)NetworkTime.time * timeSpacing, 0, 4);
                Debug.DrawLine(networkTimeVector, networkTimeVector + Vector3.up, Color.magenta, 10000f);

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
                    Apply(computed, to);

                    if (debugDraw)
                    {
                        Debug.DrawLine(from.position, to.position, Color.white, 10f);
                        Debug.DrawLine(computed.position, computed.position + Vector3.up, Color.white, 10f);
                    }
                }
                else Debug.LogWarning($"{name} HAS NO SNAPSHOTS @ {Time.frameCount}");
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
                if (syncRotation)
                {
                    // if smallest-three quaternion compression is enabled,
                    // then we don't need baseline rotation since delta always
                    // sends an absolute value.
                    if (!compressRotation)
                    {
                        writer.WriteQuaternion(snapshot.rotation);
                    }
                }

                // save serialized as 'last' for next delta compression.
                // only for reliable full sync, since unreliable isn't guaranteed to arrive.
                if (syncRotation && !compressRotation) Compression.ScaleToLong(snapshot.rotation, rotationPrecision, out lastSerializedRotation);

                // set 'last'
                last = snapshot;
            }
            // unreliable delta: compress against last full reliable state
            else
            {
                int startPosition = writer.Position;

                if (syncPosition) writer.WriteVector3(snapshot.position);
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
                if (syncRotation)
                {
                    // if smallest-three quaternion compression is enabled,
                    // then we don't need baseline rotation since delta always
                    // sends an absolute value.
                    if (!compressRotation)
                    {
                        rotation = reader.ReadQuaternion();
                    }
                }
                if (syncScale) scale = reader.ReadVector3();

                // save deserialized as 'last' for next delta compression.
                // only for reliable full sync, since unreliable isn't guaranteed to arrive.
                if (syncRotation && !compressRotation) Compression.ScaleToLong(rotation.Value, rotationPrecision, out lastDeserializedRotation);
            }
            // unreliable delta: decompress against last full reliable state
            else
            {
                if (syncPosition)
                {
                    position = reader.ReadVector3();

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

            // add a small timeline offset to account for decoupled arrival of
            // NetworkTime and NetworkTransform snapshots.
            // needs to be sendInterval. half sendInterval doesn't solve it.
            // https://github.com/MirrorNetworking/Mirror/issues/3427
            // remove this after LocalWorldState.
            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

        // reset state for next session.
        // do not ever call this during a session (i.e. after teleport).
        // calling this will break delta compression.
        public override void ResetState()
        {
            base.ResetState();

            lastSerializedRotation = Vector4Long.zero;
            lastDeserializedRotation = Vector4Long.zero;

            // reset 'last' for delta too
            last = new TransformSnapshot(0, 0, Vector3.zero, Quaternion.identity, Vector3.zero);
        }
    }
}
