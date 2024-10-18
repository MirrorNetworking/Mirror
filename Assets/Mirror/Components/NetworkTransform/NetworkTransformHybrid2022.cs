// NetworkTransform V2 by mischa (2021-07)
// Snapshot Interpolation: https://gafferongames.com/post/snapshot_interpolation/
//
// CUSTOM: this is Mirror's NetworkTransform from Oct 25, 2022:
// "breaking: NetworkTransform.clientAuthority flag obsoleted. use SyncDirection instead. automatically sets syncDirection if still used. (#3250)"
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
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform Quake 2022")]
    public class NetworkTransformHybrid2022 : NetworkBehaviour
    {
        // target transform to sync. can be on a child.
        [Header("Target")]
        [Tooltip("The Transform component to sync. May be on on this GameObject, or on a child.")]
        public Transform target;

        // TODO SyncDirection { ClientToServer, ServerToClient } is easier?
        [Obsolete("NetworkTransform clientAuthority was replaced with syncDirection. To enable client authority, set SyncDirection to ClientToServer in the Inspector.")]
        [Header("[Obsolete]")] // Unity doesn't show obsolete warning for fields. do it manually.
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        protected bool IsClientWithAuthority => isClient && authority;

        [Tooltip("Buffer size limit to avoid ever growing list memory consumption attacks.")]
        public int bufferSizeLimit = 64;
        internal SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>();
        internal SortedList<double, TransformSnapshot> serverSnapshots = new SortedList<double, TransformSnapshot>();

        // CUSTOM CHANGE: bring back sendRate. this will probably be ported to Mirror.
        // TODO but use built in syncInterval instead of the extra field here!
        [Header("Synchronization")]
        [Tooltip("Send N snapshots per second. Multiples of frame rate make sense.")]
        public int sendRate = 30; // in Hz. easier to work with as int for EMA. easier to display '30' than '0.333333333'
        public float sendInterval => 1f / sendRate;
        // END CUSTOM CHANGE

        [Tooltip("Ocassionally send a full reliable state to delta compress against. This only applies to Components with SyncMethod=Unreliable.")]
        public int baselineRate = 1;
        public float baselineInterval => baselineRate < int.MaxValue ? 1f / baselineRate : 0; // for 1 Hz, that's 1000ms
        double lastServerBaselineTime;
        double lastClientUnreliableTime;

        // delta compression needs to remember 'last' to compress against.
        // this is from reliable full state serializations, not from last
        // unreliable delta since that isn't guaranteed to be delivered.
        byte lastSerializedBaselineTick = 0;
        byte lastDeserializedBaselineTick = 0;

        protected Vector3Long lastSerializedPosition = Vector3Long.zero;
        protected Vector3Long lastDeserializedPosition = Vector3Long.zero;

        protected Vector4Long lastSerializedRotation = Vector4Long.zero;
        protected Vector4Long lastDeserializedRotation = Vector4Long.zero;

        protected Vector3Long lastSerializedScale = Vector3Long.zero;
        protected Vector3Long lastDeserializedScale = Vector3Long.zero;

        // also keep last serialized original values for 'has changed' check
        Vector3 lastPosition = Vector3.zero;
        Quaternion lastRotation = Quaternion.identity;

        // only sync when changed hack /////////////////////////////////////////
        [Header("Sync Only If Changed")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;

        [Header("Compression")]
        [Tooltip("Position is rounded in order to drastically minimize bandwidth.\n\nFor example, a precision of 0.01 rounds to a centimeter. In other words, sub-centimeter movements aren't synced until they eventually exceeded an actual centimeter.\n\nDepending on how important the object is, a precision of 0.01-0.10 (1-10 cm) is recommended.\n\nFor example, even a 1cm precision combined with delta compression cuts the Benchmark demo's bandwidth in half, compared to sending every tiny change.")]
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float positionPrecision = 0.01f; // 1 cm
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float rotationPrecision = 0.001f; // this is for the quaternion's components, needs to be small
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float scalePrecision = 0.01f; // 1 cm

        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync & interpolation")]
        public bool syncPosition = true;
        public bool syncRotation = true;
        // public bool syncScale    = false; // rarely used. disabled for perf so we can rely on transform.GetPositionAndRotation.

        double lastClientSendTime;
        double lastServerSendTime;

        // BEGIN CUSTOM CHANGE /////////////////////////////////////////////////
        public bool disableSendingThisToClients = false;
        // END CUSTOM CHANGE ///////////////////////////////////////////////////

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public bool showGizmos;
        public bool  showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // caching /////////////////////////////////////////////////////////////
        // squared values for faster distance checks
        float positionPrecisionSqr;
        float scalePrecisionSqr;

        // dedicated writer to avoid Pool.Get calls. NT is in hot path.
        NetworkWriter writer = new NetworkWriter();

        // initialization //////////////////////////////////////////////////////
        // make sure to call this when inheriting too!
        protected virtual void Awake() {}

        protected virtual void OnValidate()
        {
            // set target to self if none yet
            if (target == null) target = transform;

            // use sendRate instead of syncInterval for now
            syncInterval = 0;

            // cache squared precisions
            positionPrecisionSqr = positionPrecision * positionPrecision;
            scalePrecisionSqr = scalePrecision * scalePrecision;

            // obsolete clientAuthority compatibility:
            // if it was used, then set the new SyncDirection automatically.
            // if it wasn't used, then don't touch syncDirection.
 #pragma warning disable CS0618
            if (clientAuthority)
            {
                syncDirection = SyncDirection.ClientToServer;
                Debug.LogWarning($"{name}'s NetworkTransform component has obsolete .clientAuthority enabled. Please disable it and set SyncDirection to ClientToServer instead.");
            }
 #pragma warning restore CS0618
        }

        // snapshot functions //////////////////////////////////////////////////
        // construct a snapshot of the current state
        // => internal for testing
        protected virtual TransformSnapshot ConstructSnapshot()
        {
            // perf
            target.GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localRotation);

            // NetworkTime.localTime for double precision until Unity has it too
            return new TransformSnapshot(
                // our local time is what the other end uses as remote time
                Time.timeAsDouble,
                // the other end fills out local time itself
                0,
                localPosition, // target.localPosition,
                localRotation, // target.localRotation,
                Vector3.zero   // target.localScale
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
        protected virtual void ApplySnapshot(TransformSnapshot interpolated)
        {
            // local position/rotation for VR support
            //
            // if syncPosition/Rotation/Scale is disabled then we received nulls
            // -> current position/rotation/scale would've been added as snapshot
            // -> we still interpolated
            // -> but simply don't apply it. if the user doesn't want to sync
            //    scale, then we should not touch scale etc.
            if (syncPosition) target.localPosition = interpolated.position;
            if (syncRotation) target.localRotation = interpolated.rotation;
            // if (syncScale)    target.localScale    = interpolated.scale;
        }

        // check if position / rotation / scale changed since last _full reliable_ sync.
        // squared comparisons for performance
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Changed(Vector3 currentPosition, Quaternion currentRotation)//, Vector3 currentScale)
        {
            // if (syncPosition && Vector3.Distance(last.position, current.position) >= positionPrecision)
            if (syncPosition && (currentPosition - lastPosition).sqrMagnitude >= positionPrecisionSqr)
                return true;

            if (syncRotation && Quaternion.Angle(lastRotation, currentRotation) >= rotationPrecision)
                return true;

            // if (syncScale && Vector3.Distance(last.scale, current.scale) >= scalePrecision)
            // if (syncScale && (current.scale - last.scale).sqrMagnitude >= scalePrecisionSqr)
            //     return true;

            return false;
        }

        // serialization ///////////////////////////////////////////////////////
        // serialize server->client baseline into a NetworkWriter.
        // for use in RpcSync and OnSerialize for spawn message.
        void SerializeServerBaseline(NetworkWriter writer, Vector3 position, Quaternion rotation)//, Vector3 scale)
        {
            // always include the tick for deltas to compare against.
            byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!
            writer.WriteByte(frameCount);

            if (syncPosition)
            {
                writer.WriteVector3(position);

                // save serialized as 'last' for next delta compression.
                if (syncPosition) Compression.ScaleToLong(position, positionPrecision, out lastSerializedPosition);
            }
            if (syncRotation)
            {
                writer.WriteQuaternion(target.localRotation);

                // save serialized as 'last' for next delta compression.
                if (syncRotation) Compression.ScaleToLong(rotation, rotationPrecision, out lastSerializedRotation);
            }
            // if (syncScale)
            // {
            //     writer.WriteVector3(target.localScale);
            //
            //     // save serialized as 'last' for next delta compression.
            //     if (syncScale) Compression.ScaleToLong(scale, scalePrecision, out lastSerializedScale);
            // }

            // save the last baseline's tick number.
            // included in baseline to identify which one it was on client
            // included in deltas to ensure they are on top of the correct baseline
            lastSerializedBaselineTick = frameCount;
            lastServerBaselineTime = NetworkTime.localTime;

            // set 'last'
            lastPosition = position;
            lastRotation = rotation;
        }

        void DeserializeServerBaseline(NetworkReader reader)
        {
            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = reader.ReadByte();

            if (syncPosition)
            {
                Vector3 position = reader.ReadVector3();
                target.localPosition = position;

                // save deserialized as 'last' for next delta compression.
                Compression.ScaleToLong(position, positionPrecision, out lastDeserializedPosition);
            }
            if (syncRotation)
            {
                Quaternion rotation = reader.ReadQuaternion();
                target.localRotation = rotation;

                // save deserialized as 'last' for next delta compression.
                Compression.ScaleToLong(rotation, rotationPrecision, out lastDeserializedRotation);
            }
            // if (syncScale)
            // {
            //     Vector3 scale = reader.ReadVector3();
            //     target.localScale = scale;
            //
            //     // save deserialized as 'last' for next delta compression.
            //     Compression.ScaleToLong(scale, scalePrecision, out lastDeserializedScale);
            // }
        }

        void SerializeServerDelta(NetworkWriter writer, byte baselineTick, Vector3 position, Quaternion rotation)//, Vector3 scale)
        {
            writer.WriteByte(baselineTick);

            if (syncPosition)
            {
                // quantize -> delta -> varint
                Compression.ScaleToLong(position, positionPrecision, out Vector3Long quantized);
                DeltaCompression.Compress(writer, lastSerializedPosition, quantized);
            }
            if (syncRotation)
            {
                // quantize -> delta -> varint
                // this works for quaternions too, where xyzw are [-1,1]
                // and gradually change as rotation changes.
                Compression.ScaleToLong(rotation, rotationPrecision, out Vector4Long quantized);
                DeltaCompression.Compress(writer, lastSerializedRotation, quantized);
            }
            // if (syncScale)
            // {
            //     // quantize -> delta -> varint
            //     Compression.ScaleToLong(scale, scalePrecision, out Vector3Long quantized);
            //     DeltaCompression.Compress(writer, lastSerializedScale, quantized);
            // }
        }

        bool DeserializeServerDelta(NetworkReader reader, out byte baselineTick, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = default;
            rotation = default;
            scale = default;

            baselineTick = reader.ReadByte();

            // unreliable messages may arrive out of order.
            // ensure this is for the intended baseline.
            // we don't want to put a delta onto an old baseline.
            if (baselineTick != lastDeserializedBaselineTick)
            {
                // Debug.Log($"[{name}] Client discarding unreliable delta for baseline #{baselineTick} because we already received #{lastDeserializedBaselineTick}");
                return false;
            }

            if (syncPosition)
            {
                // varint -> delta -> quantize
                Vector3Long quantized = DeltaCompression.Decompress(reader, lastDeserializedPosition);
                position = Compression.ScaleToFloat(quantized, positionPrecision);
            }
            if (syncRotation)
            {
                // varint -> delta -> quantize
                // this works for quaternions too, where xyzw are [-1,1]
                // and gradually change as rotation changes.
                Vector4Long quantized = DeltaCompression.Decompress(reader, lastDeserializedRotation);
                rotation = Compression.ScaleToFloat(quantized, rotationPrecision);
            }
            // if (syncScale)
            // {
            //     // varint -> delta -> quantize
            //     Vector3Long quantized = DeltaCompression.Decompress(reader, lastDeserializedScale);
            //     scale = Compression.ScaleToFloat(quantized, scalePrecision);
            // }

            return true;
        }

        // cmd /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(Vector3? position, Quaternion? rotation)//, Vector3? scale)
        {
            OnClientToServerSync(position, rotation);//, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (syncDirection == SyncDirection.ClientToServer &&
                connectionToClient != null && // CUSTOM CHANGE: for the drop thing..
                !disableSendingThisToClients) // CUSTOM CHANGE: see comment at definition
            {
                // reuse cached writer for performance
                // using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                writer.Position = 0;
                {
                    // TODO directly forward again later
                    Debug.LogWarning($"[{name}] CmdClientToServerSync: TODO which baseline to pass in Rpc?");
                    // SerializeServerDelta(writer,
                    //     0xFF,
                    //     position.HasValue ? position.Value : default,
                    //     rotation.HasValue ? rotation.Value : default//,
                    //     // scale.HasValue ? scale.Value : default
                    // );
                    // RpcServerToClientDeltaSync(writer);
                }
            }
        }

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation)//, Vector3? scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // only player owned objects (with a connection) can send to
            // server. we can get the timestamp from the connection.
            double timestamp = connectionToClient.remoteTimeStamp;

            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.
            if (!position.HasValue) position = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].position : target.localPosition;
            if (!rotation.HasValue) rotation = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].rotation : target.localRotation;
            // if (!scale.HasValue)    scale    = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].scale    : target.localScale;

            // insert transform snapshot
            SnapshotInterpolation.InsertIfNotExists(
                serverSnapshots,
                bufferSizeLimit,
                new TransformSnapshot(
                timestamp,         // arrival remote timestamp. NOT remote time.
                Time.timeAsDouble,
                position.Value,
                rotation.Value,
                Vector3.zero // scale
            ));
        }

        // rpc /////////////////////////////////////////////////////////////////
        [ClientRpc(channel = Channels.Reliable)]
        void RpcServerToClientBaselineSync(ArraySegment<byte> message)
        {
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(message))
            {
                DeserializeServerBaseline(reader);
                // Debug.Log($"[{name}] client received baseline #{lastDeserializedBaselineTick}");
            }
        }

        // only unreliable. see comment above of this file.
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientDeltaSync(ArraySegment<byte> message)
        {
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(message))
            {
                if (DeserializeServerDelta(reader, out byte baselineTick, out Vector3 position, out Quaternion rotation, out Vector3 scale))
                {
                    OnServerToClientDeltaSync(baselineTick, position, rotation, scale);
                }
            }
        }

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientDeltaSync(byte baselineTick, Vector3? position, Quaternion? rotation, Vector3? scale)
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

            // Debug.Log($"[{name}] Client: received delta for baseline #{baselineTick}");

            // on the client, we receive rpcs for all entities.
            // not all of them have a connectionToServer.
            // but all of them go through NetworkClient.connection.
            // we can get the timestamp from there.
            double timestamp = NetworkClient.connection.remoteTimeStamp;

            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.
            if (!position.HasValue) position = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].position : target.localPosition;
            if (!rotation.HasValue) rotation = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].rotation : target.localRotation;
            if (!scale.HasValue) scale = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].scale : target.localScale;

            // insert snapshot
            SnapshotInterpolation.InsertIfNotExists(
                clientSnapshots,
                bufferSizeLimit,
                new TransformSnapshot(
                timestamp,         // arrival remote timestamp. NOT remote time.
                Time.timeAsDouble,
                position.Value,
                rotation.Value,
                scale.Value
            ));
        }

        // update server ///////////////////////////////////////////////////////
        bool baselineDirty = true;
        void UpdateServerBaseline(double localTime)
        {
            // send a reliable baseline every 1 Hz
            if (localTime >= lastServerBaselineTime + baselineInterval)
            {
                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);

                // only send a new reliable baseline if changed since last time
                // check if changed (unless that feature is disabled).
                // baseline is guaranteed to be delivered over reliable.
                // here is the only place where we can check for changes.
                if (!onlySyncOnChange || Changed(position, rotation)) //snapshot))
                {
                    // reliable just changed. keep sending deltas until it's unchanged again.
                    baselineDirty = true;

                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    // reuse cached writer for performance
                    // using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                    writer.Position = 0;
                    {
                        SerializeServerBaseline(writer, position, rotation);//, scale);
                        RpcServerToClientBaselineSync(writer);
                    }
                }
                // indicate that we should stop sending deltas now
                else baselineDirty = false;
            }
        }

        void UpdateServerDelta(double localTime)
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
            // TODO send same time that NetworkServer sends time snapshot?

            // only sync on change:
            // unreliable isn't guaranteed to be delivered so this depends on reliable baseline.
            // if baseline is dirty, send unreliables every sendInterval until baseline is not dirty anymore.
            if (onlySyncOnChange && !baselineDirty) return;

            if (localTime >= lastServerSendTime + sendInterval) // CUSTOM CHANGE: allow custom sendRate + sendInterval again
            {
                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);

                // reuse cached writer for performance
                // using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                writer.Position = 0;
                {
                    SerializeServerDelta(
                        writer,
                        // include the last reliable baseline tick#.
                        // the unreliable delta is meant to go on top of it that, and no older one.
                        lastSerializedBaselineTick,
                        position,
                        rotation//,
                        // snapshot.scale
                    );
                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    RpcServerToClientDeltaSync(writer);
                }

                lastServerSendTime = localTime;
            }
        }

        void UpdateServerInterpolation()
        {
            // apply buffered snapshots IF client authority
            // -> in server authority, server moves the object
            //    so no need to apply any snapshots there.
            // -> don't apply for host mode player objects either, even if in
            //    client authority mode. if it doesn't go over the network,
            //    then we don't need to do anything.
            if (syncDirection == SyncDirection.ClientToServer && !isOwned)
            {
                if (serverSnapshots.Count > 0)
                {
                    // step the transform interpolation without touching time.
                    // NetworkClient is responsible for time globally.
                    SnapshotInterpolation.StepInterpolation(
                        serverSnapshots,
                        // CUSTOM CHANGE: allow for custom sendRate+sendInterval again.
                        // for example, if the object is moving @ 1 Hz, always put it back by 1s.
                        // that's how we still get smooth movement even with a global timeline.
                        connectionToClient.remoteTimeline - sendInterval,
                        // END CUSTOM CHANGE
                        out TransformSnapshot from,
                        out TransformSnapshot to,
                        out double t);

                    // interpolate & apply
                    TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);
                    ApplySnapshot(computed);
                }
            }
        }

        void UpdateServer()
        {
            // broadcasting
            if (syncDirection == SyncDirection.ServerToClient || IsClientWithAuthority)
            {
                // perf: only grab NetworkTime.localTime property once.
                double localTime = NetworkTime.localTime;
                UpdateServerBaseline(localTime);
                UpdateServerDelta(localTime);
            }

            // interpolate remote clients
            UpdateServerInterpolation();
        }

        // update client ///////////////////////////////////////////////////////
        void UpdateClientBroadcast()
        {
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
            if (NetworkTime.localTime >= lastClientSendTime + sendInterval) // CUSTOM CHANGE: allow custom sendRate + sendInterval again
            {
                // send snapshot without timestamp.
                // receiver gets it from batch timestamp to save bandwidth.
                TransformSnapshot snapshot = ConstructSnapshot();

                CmdClientToServerSync(
                    // only sync what the user wants to sync
                    syncPosition ? snapshot.position : default(Vector3?),
                    syncRotation ? snapshot.rotation : default(Quaternion?)//,
                    // syncScale    ? snapshot.scale    : default(Vector3?)
                );

                lastClientSendTime = NetworkTime.localTime;
            }
        }

        void UpdateClientInterpolation()
        {
            // only while we have snapshots
            if (clientSnapshots.Count > 0)
            {
                // step the interpolation without touching time.
                // NetworkClient is responsible for time globally.
                SnapshotInterpolation.StepInterpolation(
                    clientSnapshots,
                    // CUSTOM CHANGE: allow for custom sendRate+sendInterval again.
                    // for example, if the object is moving @ 1 Hz, always put it back by 1s.
                    // that's how we still get smooth movement even with a global timeline.
                    NetworkTime.time - sendInterval, // == NetworkClient.localTimeline from snapshot interpolation
                    // END CUSTOM CHANGE
                    out TransformSnapshot from,
                    out TransformSnapshot to,
                    out double t);

                // interpolate & apply
                TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);
                ApplySnapshot(computed);
            }
        }

        void UpdateClient()
        {
            // client authority, and local player (= allowed to move myself)?
            if (IsClientWithAuthority)
            {
                // https://github.com/vis2k/Mirror/pull/2992/
                if (!NetworkClient.ready) return;

                UpdateClientBroadcast();
            }
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer
            else
            {
                UpdateClientInterpolation();
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
            target.position = destination;

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
            target.position = destination;
            target.rotation = rotation;

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
            if (syncDirection != SyncDirection.ClientToServer) return;

            // TODO what about host mode?
            OnTeleport(destination);

            // if a client teleports, we need to broadcast to everyone else too
            // TODO the teleported client should ignore the rpc though.
            //      otherwise if it already moved again after teleporting,
            //      the rpc would come a little bit later and reset it once.
            // TODO or not? if client ONLY calls Teleport(pos), the position
            //      would only be set after the rpc. unless the client calls
            //      BOTH Teleport(pos) and target.position=pos
            RpcTeleport(destination);
        }

        // client->server teleport to force position and rotation without interpolation.
        // otherwise it would interpolate to a (far away) new position.
        // => manually calling Teleport is the only 100% reliable solution.
        [Command]
        public void CmdTeleport(Vector3 destination, Quaternion rotation)
        {
            // client can only teleport objects that it has authority over.
            if (syncDirection != SyncDirection.ClientToServer) return;

            // TODO what about host mode?
            OnTeleport(destination, rotation);

            // if a client teleports, we need to broadcast to everyone else too
            // TODO the teleported client should ignore the rpc though.
            //      otherwise if it already moved again after teleporting,
            //      the rpc would come a little bit later and reset it once.
            // TODO or not? if client ONLY calls Teleport(pos), the position
            //      would only be set after the rpc. unless the client calls
            //      BOTH Teleport(pos) and target.position=pos
            RpcTeleport(destination, rotation);
        }

        [Server]
        public void ServerTeleport(Vector3 destination, Quaternion rotation)
        {
            OnTeleport(destination, rotation);
            RpcTeleport(destination, rotation);
        }

        public virtual void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();

            // reset delta compression
            lastSerializedBaselineTick = 0;
            lastDeserializedBaselineTick = 0;

            lastSerializedPosition = Vector3Long.zero;
            lastDeserializedPosition = Vector3Long.zero;

            lastSerializedRotation = Vector4Long.zero;
            lastDeserializedRotation = Vector4Long.zero;

            lastSerializedScale = Vector3Long.zero;
            lastDeserializedScale = Vector3Long.zero;

            // reset 'last' for delta too
            lastPosition = Vector3.zero;
            lastRotation = Quaternion.identity;

            // Debug.Log($"[{name}] Reset to baselineTick=0");
        }

        protected virtual void OnDisable() => Reset();
        protected virtual void OnEnable() => Reset();

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                // spawn message is used as first baseline.
                TransformSnapshot snapshot = ConstructSnapshot();
                SerializeServerBaseline(writer, snapshot.position, snapshot.rotation);//, snapshot.scale);
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                // save spawn message as baseline
                DeserializeServerBaseline(reader);
                // Debug.Log($"[{name}] Spawn is used as first baseline #{lastDeserializedBaselineTick}");
            }
        }
        // CUSTOM CHANGE ///////////////////////////////////////////////////////////
        // Don't run OnGUI or draw gizmos in debug builds.
        // OnGUI allocates even if it does nothing. avoid in release.
        //#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if UNITY_EDITOR
        // debug ///////////////////////////////////////////////////////////////
        // END CUSTOM CHANGE ///////////////////////////////////////////////////////
        protected virtual void OnGUI()
        {
            if (!showOverlay) return;

            // show data next to player for easier debugging. this is very useful!
            // IMPORTANT: this is basically an ESP hack for shooter games.
            //            DO NOT make this available with a hotkey in release builds
            if (!Debug.isDebugBuild) return;

            // project position to screen
            Vector3 point = Camera.main.WorldToScreenPoint(target.position);

            // enough alpha, in front of camera and in screen?
            if (point.z >= 0 && Utils.IsPointInScreen(point))
            {
                GUI.color = overlayColor;
                GUILayout.BeginArea(new Rect(point.x, Screen.height - point.y, 200, 100));

                // always show both client & server buffers so it's super
                // obvious if we accidentally populate both.
                GUILayout.Label($"Server Buffer:{serverSnapshots.Count}");
                GUILayout.Label($"Client Buffer:{clientSnapshots.Count}");

                GUILayout.EndArea();
                GUI.color = Color.white;
            }
        }

        protected virtual void DrawGizmos(SortedList<double, TransformSnapshot> buffer)
        {
            // only draw if we have at least two entries
            if (buffer.Count < 2) return;

            // calculate threshold for 'old enough' snapshots
            double threshold = NetworkTime.localTime - NetworkClient.bufferTime;
            Color oldEnoughColor = new Color(0, 1, 0, 0.5f);
            Color notOldEnoughColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            // draw the whole buffer for easier debugging.
            // it's worth seeing how much we have buffered ahead already
            for (int i = 0; i < buffer.Count; ++i)
            {
                // color depends on if old enough or not
                TransformSnapshot entry = buffer.Values[i];
                bool oldEnough = entry.localTime <= threshold;
                Gizmos.color = oldEnough ? oldEnoughColor : notOldEnoughColor;
                Gizmos.DrawCube(entry.position, Vector3.one);
            }

            // extra: lines between start<->position<->goal
            Gizmos.color = Color.green;
            Gizmos.DrawLine(buffer.Values[0].position, target.position);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(target.position, buffer.Values[1].position);
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
