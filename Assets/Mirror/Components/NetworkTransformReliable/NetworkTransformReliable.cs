// NetworkTransform V3 (reliable) by mischa (2022-10)
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Reliable)")]
    public class NetworkTransformReliable : NetworkTransformBase
    {
        [Header("Rotation")]
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
        public float scalePrecision = 0.01f; // 1 cm

        [Header("Snapshot Interpolation")]
        [Tooltip("Add a small timeline offset to account for decoupled arrival of NetworkTime and NetworkTransform snapshots.\nfixes: https://github.com/MirrorNetworking/Mirror/issues/3427")]
        public bool timelineOffset = false;

        // delta compression needs to remember 'last' to compress against
        protected Vector3Long lastSerializedPosition = Vector3Long.zero;
        protected Vector3Long lastDeserializedPosition = Vector3Long.zero;

        protected Vector3Long lastSerializedScale = Vector3Long.zero;
        protected Vector3Long lastDeserializedScale = Vector3Long.zero;

        // update //////////////////////////////////////////////////////////////
        // Update applies interpolation.
        void Update()
        {
            // if server then always sync to others.
            if (isServer) UpdateServer();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClient();
        }

        // LateUpdate sets dirty.
        // movement scripts may change positions in Update.
        // use LateUpdate to ensure changes are detected in the same frame.
        // otherwise this may run before user update, delaying detection until next frame.
        // this would cause visible jitter.
        void LateUpdate()
        {
            // set dirty to trigger OnSerialize. either always, or only if changed.
            if (isServer || (IsClientWithAuthority && NetworkClient.ready)) // is NetworkClient.ready even needed?
            {
                SetDirty();
            }
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
                }
            }
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
                if (syncScale) writer.WriteVector3(snapshot.scale);
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
            if (syncScale) Compression.ScaleToLong(snapshot.scale, scalePrecision, out lastSerializedScale);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            Vector3? position = null;
            Quaternion? rotation = null;
            Vector3? scale = null;

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
                if (syncScale) scale = reader.ReadVector3();
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
            if (isServer) OnClientToServerSync(position, rotation, scale);
            else if (isClient) OnServerToClientSync(position, rotation, scale);

            // save deserialized as 'last' for next delta compression
            if (syncPosition) Compression.ScaleToLong(position.Value, positionPrecision, out lastDeserializedPosition);
            if (syncScale) Compression.ScaleToLong(scale.Value, scalePrecision, out lastDeserializedScale);
        }

        // sync ////////////////////////////////////////////////////////////////

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // add a small timeline offset to account for decoupled arrival of
            // NetworkTime and NetworkTransform snapshots.
            // needs to be sendInterval. half sendInterval doesn't solve it.
            // https://github.com/MirrorNetworking/Mirror/issues/3427
            // remove this after LocalWorldState.
            double offset = timelineOffset ? NetworkServer.sendInterval : 0;
            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp + offset, position, rotation, scale);
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
            double offset = timelineOffset ? NetworkServer.sendInterval : 0;
            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + offset, position, rotation, scale);
        }

        // only sync on change /////////////////////////////////////////////////
        public override void Reset()
        {
            base.Reset();

            // reset delta
            lastSerializedPosition = Vector3Long.zero;
            lastDeserializedPosition = Vector3Long.zero;

            lastSerializedScale = Vector3Long.zero;
            lastDeserializedScale = Vector3Long.zero;
        }
    }
}
