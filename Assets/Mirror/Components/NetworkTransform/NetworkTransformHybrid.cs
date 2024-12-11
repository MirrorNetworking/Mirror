// Quake NetworkTransform based on 2022 NetworkTransformUnreliable.
// Snapshot Interpolation: https://gafferongames.com/post/snapshot_interpolation/
// Quake: https://www.jfedor.org/quake3/
//
// Base class for NetworkTransform and NetworkTransformChild.
// => simple unreliable sync without any interpolation for now.
// => which means we don't need teleport detection either
//
// several functions are virtual in case someone needs to modify a part.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform Hybrid")]
    public class NetworkTransformHybrid : NetworkBehaviour
    {
        // target transform to sync. can be on a child.
        [Header("Target")]
        [Tooltip("The Transform component to sync. May be on this GameObject, or on a child.")]
        public Transform target;

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

        [Tooltip("Occasionally send a full reliable state to delta compress against. This only applies to Components with SyncMethod=Unreliable.")]
        public int baselineRate = 1;
        public float baselineInterval => baselineRate < int.MaxValue ? 1f / baselineRate : 0; // for 1 Hz, that's 1000ms
        double lastBaselineTime;
        double lastDeltaTime;

        // delta compression needs to remember 'last' to compress against.
        // this is from reliable full state serializations, not from last
        // unreliable delta since that isn't guaranteed to be delivered.
        byte lastSerializedBaselineTick = 0;
        Vector3 lastSerializedBaselinePosition = Vector3.zero;
        Quaternion lastSerializedBaselineRotation = Quaternion.identity;
        Vector3 lastSerializedBaselineScale = Vector3.one;

        // save last deserialized baseline to delta decompress against
        byte lastDeserializedBaselineTick = 0;
        Vector3 lastDeserializedBaselinePosition = Vector3.zero;                // unused, but keep for delta
        Quaternion lastDeserializedBaselineRotation = Quaternion.identity;      // unused, but keep for delta
        Vector3 lastDeserializedBaselineScale = Vector3.one;                    // unused, but keep for delta

        // only sync when changed hack /////////////////////////////////////////
        [Header("Sync Only If Changed")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;

        // change detection: we need to do this carefully in order to get it right.
        //
        // DONT just check changes in UpdateBaseline(). this would introduce MrG's grid issue:
        //   server start in A1, reliable baseline sent to client
        //   server moves to A2, unreliabe delta sent to client
        //   server moves to A1, nothing is sent to client becuase last baseline position == position
        //   => client wouldn't know we moved back to A1
        //
        // INSTEAD: every update() check for changes since baseline:
        //   UpdateDelta() keeps sending only if changed since _baseline_
        //   UpdateBaseline() resends if there was any change in the period since last baseline.
        //   => this avoids the A1->A2->A1 grid issue above
        bool changedSinceBaseline = false;

        // sensitivity is for changed-detection,
        // this is != precision, which is for quantization and delta compression.
        [Header("Sensitivity"), Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float positionSensitivity = 0.01f;
        public float rotationSensitivity = 0.01f;
        public float scaleSensitivity    = 0.01f;

        [Tooltip("Enable to send all unreliable messages twice. Only useful for extremely fast-paced games since it doubles bandwidth costs.")]
        public bool unreliableRedundancy = false;

        [Tooltip("When sending a reliable baseline, should we also send an unreliable delta or rely on the reliable baseline to arrive in a similar time?")]
        public bool baselineIsDelta = true;

        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync & interpolation")]
        public bool syncPosition = true;
        public bool syncRotation = true;
        public bool syncScale    = false;

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public bool debugDraw;
        public bool showGizmos;
        public bool  showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // initialization //////////////////////////////////////////////////////
        // make sure to call this when inheriting too!
        protected virtual void Awake() {}

        protected override void OnValidate()
        {
            base.OnValidate();

            // set target to self if none yet
            if (target == null) target = transform;

            // use sendRate instead of syncInterval for now
            syncInterval = 0;
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
            if (syncScale)    target.localScale    = interpolated.scale;
        }

        // check if position / rotation / scale changed since last _full reliable_ sync.
        // squared comparisons for performance
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Changed(Vector3 currentPosition, Quaternion currentRotation, Vector3 currentScale)
        {
            if (syncPosition)
            {
                float positionDelta = Vector3.Distance(currentPosition, lastSerializedBaselinePosition);
                if (positionDelta >= positionSensitivity)
                {
                    return true;
                }
            }

            if (syncRotation)
            {
                float rotationDelta = Quaternion.Angle(lastSerializedBaselineRotation, currentRotation);
                if (rotationDelta >= rotationSensitivity)
                {
                    return true;
                }
            }

            if (syncScale)
            {
                float scaleDelta = Vector3.Distance(currentScale, lastSerializedBaselineScale);
                if (scaleDelta >= scaleSensitivity)
                {
                    return true;
                }
            }

            return false;
        }

        // cmd baseline ////////////////////////////////////////////////////////
        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_PositionRotationScale(byte baselineTick, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;
            lastDeserializedBaselineRotation = rotation;
            lastDeserializedBaselineScale    = scale;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, position, rotation, scale);
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_PositionRotation(byte baselineTick, Vector3 position, Quaternion rotation)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;
            lastDeserializedBaselineRotation = rotation;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, position, rotation, Vector3.one);
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_PositionScale(byte baselineTick, Vector3 position, Vector3 scale)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;
            lastDeserializedBaselineScale    = scale;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, position, Quaternion.identity, scale);
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_RotationScale(byte baselineTick, Quaternion rotation, Vector3 scale)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselineRotation = rotation;
            lastDeserializedBaselineScale    = scale;

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, Vector3.zero, rotation, scale);
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_Position(byte baselineTick, Vector3 position)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, position, Quaternion.identity, Vector3.one);
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_Rotation(byte baselineTick, Quaternion rotation)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselineRotation = rotation;

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, Vector3.zero, rotation, Vector3.one);
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline_Scale(byte baselineTick, Vector3 scale)
        {
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselineScale = scale;

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnClientToServerDeltaSync(baselineTick, Vector3.zero, Quaternion.identity, scale);
        }

        // cmd delta ///////////////////////////////////////////////////////////
        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_Position(byte baselineTick, Vector3 position)
        {
            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, position, Quaternion.identity, Vector3.one);
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_Rotation(byte baselineTick, Quaternion rotation)
        {
            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, Vector3.zero, rotation, Vector3.one);
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_Scale(byte baselineTick, Vector3 scale)
        {
            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, Vector3.zero, Quaternion.identity, scale);
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_PositionRotationScale(byte baselineTick, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, position, rotation, scale);
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_PositionRotation(byte baselineTick, Vector3 position, Quaternion rotation)
        {
            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, position, rotation, Vector3.one);
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_PositionScale(byte baselineTick, Vector3 position, Vector3 scale)
        {
            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, position, Quaternion.identity, scale);
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta_RotationScale(byte baselineTick, Quaternion rotation, Vector3 scale)
        {
            // Debug.Log($"[{name}] server received delta for baseline #{lastDeserializedBaselineTick}");
            OnClientToServerDeltaSync(baselineTick, Vector3.zero, rotation, scale);
        }

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerDeltaSync(byte baselineTick, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // ensure this delta is for our last known baseline.
            // we should never apply a delta on top of a wrong baseline.
            if (baselineTick != lastDeserializedBaselineTick)
            {
                // debug draw: drop
                if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.red, 10f);

                // this can happen if unreliable arrives before reliable etc.
                // no need to log this except when debugging.
                // Debug.Log($"[{name}] Server: received delta for wrong baseline #{baselineTick} from: {connectionToClient}. Last was {lastDeserializedBaselineTick}. Ignoring.");
                return;
            }

            // protect against ever-growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // only player owned objects (with a connection) can send to
            // server. we can get the timestamp from the connection.
            double timestamp = connectionToClient.remoteTimeStamp;

            // insert transform snapshot
            SnapshotInterpolation.InsertIfNotExists(
                serverSnapshots,
                bufferSizeLimit,
                new TransformSnapshot(
                timestamp,         // arrival remote timestamp. NOT remote time.
                NetworkTime.localTime, // Unity 2019 doesn't have Time.timeAsDouble yet
                position,
                rotation,
                scale
            ));
        }

        // rpc baseline ////////////////////////////////////////////////////////
        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_PositionRotationScale(byte baselineTick, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;
            lastDeserializedBaselineRotation = rotation;
            lastDeserializedBaselineScale = scale;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, position, rotation, scale);
        }

        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_PositionRotation(byte baselineTick, Vector3 position, Quaternion rotation)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;
            lastDeserializedBaselineRotation = rotation;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, position, rotation, Vector3.one);
        }

        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_PositionScale(byte baselineTick, Vector3 position, Vector3 scale)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;
            lastDeserializedBaselineScale    = scale;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, position, Quaternion.identity, scale);
        }

        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_RotationScale(byte baselineTick, Quaternion rotation, Vector3 scale)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselineRotation = rotation;
            lastDeserializedBaselineScale    = scale;

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, Vector3.zero, rotation, scale);
        }

        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_Position(byte baselineTick, Vector3 position)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselinePosition = position;

            // debug draw: baseline
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, position, Quaternion.identity, Vector3.one);
        }

        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_Rotation(byte baselineTick, Quaternion rotation)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselineRotation = rotation;

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, Vector3.zero, rotation, Vector3.one);
        }

        [ClientRpc(channel = Channels.Reliable)] // reliable baseline
        void RpcServerToClientBaseline_Scale(byte baselineTick, Vector3 scale)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // save last deserialized baseline tick number to compare deltas against
            lastDeserializedBaselineTick = baselineTick;
            lastDeserializedBaselineScale = scale;

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
                OnServerToClientDeltaSync(baselineTick, Vector3.zero, Quaternion.identity, scale);
        }

        // rpc delta ///////////////////////////////////////////////////////////
        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_PositionRotationScale(byte baselineTick, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            OnServerToClientDeltaSync(baselineTick, position, rotation, scale);
        }

        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_PositionRotation(byte baselineTick, Vector3 position, Quaternion rotation)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            OnServerToClientDeltaSync(baselineTick, position, rotation, Vector3.one);
        }

        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_PositionScale(byte baselineTick, Vector3 position, Vector3 scale)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            OnServerToClientDeltaSync(baselineTick, position, Quaternion.identity, scale);
        }

        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_RotationScale(byte baselineTick, Quaternion rotation, Vector3 scale)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            OnServerToClientDeltaSync(baselineTick, Vector3.zero, rotation, scale);
        }

        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_Position(byte baselineTick, Vector3 position)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // debug draw: delta
            if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.white, 10f);

            OnServerToClientDeltaSync(baselineTick, position, Quaternion.identity, Vector3.one);
        }

        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_Rotation(byte baselineTick, Quaternion rotation)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            OnServerToClientDeltaSync(baselineTick, Vector3.zero, rotation, Vector3.one);
        }

        [ClientRpc(channel = Channels.Unreliable)] // unreliable delta
        void RpcServerToClientDelta_Scale(byte baselineTick, Vector3 scale)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            OnServerToClientDeltaSync(baselineTick, Vector3.zero, Quaternion.identity, scale);
        }

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientDeltaSync(byte baselineTick, Vector3 position, Quaternion rotation, Vector3 scale)
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

            // ensure this delta is for our last known baseline.
            // we should never apply a delta on top of a wrong baseline.
            if (baselineTick != lastDeserializedBaselineTick)
            {
                // debug draw: drop
                if (debugDraw) Debug.DrawLine(position, position + Vector3.up, Color.red, 10f);

                // this can happen if unreliable arrives before reliable etc.
                // no need to log this except when debugging.
                // Debug.Log($"[{name}] Client: received delta for wrong baseline #{baselineTick}. Last was {lastDeserializedBaselineTick}. Ignoring.");
                return;
            }

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
            // if (!syncPosition) position = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].position : target.localPosition;
            // if (!syncRotation) rotation = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].rotation : target.localRotation;
            // if (!syncScale)    scale    = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].scale : target.localScale;

            // insert snapshot
            SnapshotInterpolation.InsertIfNotExists(
                clientSnapshots,
                bufferSizeLimit,
                new TransformSnapshot(
                timestamp,             // arrival remote timestamp. NOT remote time.
                NetworkTime.localTime, // Unity 2019 doesn't have Time.timeAsDouble yet
                position,
                rotation,
                scale
            ));
        }

        // update server ///////////////////////////////////////////////////////
        void UpdateServerBaseline(double localTime)
        {
            // only sync on change: only resend baseline if changed since last.
            if (onlySyncOnChange && !changedSinceBaseline) return;

            // send a reliable baseline every 1 Hz
            if (localTime >= lastBaselineTime + baselineInterval)
            {
                // Debug.Log($"UpdateServerBaseline for {name}");

                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
                Vector3 scale = target.localScale;

                // save bandwidth by only transmitting what is needed.
                // -> ArraySegment with random data is slower since byte[] copying
                // -> Vector3? and Quaternion? nullables takes more bandwidth
                byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!

                if (syncPosition && syncRotation && syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_PositionRotationScale(frameCount, position, rotation, scale);
                }
                else if (syncPosition && syncRotation)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_PositionRotation(frameCount, position, rotation);
                }
                else if (syncPosition && syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_PositionScale(frameCount, position, scale);
                }
                else if (syncRotation && syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_RotationScale(frameCount, rotation, scale);
                }
                else if (syncPosition)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_Position(frameCount, position);
                }
                else if (syncRotation)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_Rotation(frameCount, rotation);
                }
                else if (syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    RpcServerToClientBaseline_Scale(frameCount, scale);
                }

                // position, rotation, scale
                // position, rotation, !scale
                // position,

                // save the last baseline's tick number.
                // included in baseline to identify which one it was on client
                // included in deltas to ensure they are on top of the correct baseline
                lastSerializedBaselineTick = frameCount;
                lastBaselineTime = NetworkTime.localTime;
                lastSerializedBaselinePosition = position;
                lastSerializedBaselineRotation = rotation;
                lastSerializedBaselineScale = scale;

                // baseline was just sent after a change. reset change detection.
                changedSinceBaseline = false;

                // perf. & bandwidth optimization:
                // send a delta right after baseline to avoid potential head of
                // line blocking, or skip the delta whenever we sent reliable?
                // for example:
                //    1 Hz baseline
                //   10 Hz delta
                //   => 11 Hz total if we still send delta after reliable
                //   => 10 Hz total if we skip delta after reliable
                // in that case, skip next delta by simply resetting last delta sync's time.
                if (baselineIsDelta) lastDeltaTime = localTime;
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

            if (localTime >= lastDeltaTime + sendInterval) // CUSTOM CHANGE: allow custom sendRate + sendInterval again
            {
                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
                Vector3 scale = target.localScale;

                // look for changes every unreliable sendInterval!
                // every reliable interval isn't enough, this would cause MrG's grid issue:
                //   server start in A1, reliable baseline sent to client
                //   server moves to A2, unreliabe delta sent to client
                //   server moves to A1, nothing is sent to client becuase last baseline position == position
                //   => client wouldn't know we moved back to A1
                // every update works, but it's unnecessary overhead since sends only happen every sendInterval
                // every unreliable sendInterval is the perfect place to look for changes.
                if (onlySyncOnChange && Changed(position, rotation, scale))
                    changedSinceBaseline = true;

                // only sync on change:
                // unreliable isn't guaranteed to be delivered so this depends on reliable baseline.
                if (onlySyncOnChange && !changedSinceBaseline) return;

                // save bandwidth by only transmitting what is needed.
                // -> ArraySegment with random data is slower since byte[] copying
                // -> Vector3? and Quaternion? nullables takes more bandwidth

                if (syncPosition && syncRotation && syncScale)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_PositionRotationScale(lastSerializedBaselineTick, position, rotation, scale);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_PositionRotationScale(lastSerializedBaselineTick, position, rotation, scale);
                }
                else if (syncPosition && syncRotation)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_PositionRotation(lastSerializedBaselineTick, position, rotation);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_PositionRotation(lastSerializedBaselineTick, position, rotation);
                }
                else if (syncPosition && syncScale)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_PositionScale(lastSerializedBaselineTick, position, scale);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_PositionScale(lastSerializedBaselineTick, position, scale);
                }
                else if (syncRotation && syncScale)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_RotationScale(lastSerializedBaselineTick, rotation, scale);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_RotationScale(lastSerializedBaselineTick, rotation, scale);
                }
                else if (syncPosition)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_Position(lastSerializedBaselineTick, position);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_Position(lastSerializedBaselineTick, position);
                }
                else if (syncRotation)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_Rotation(lastSerializedBaselineTick, rotation);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_Rotation(lastSerializedBaselineTick, rotation);
                }
                else if (syncScale)
                {
                    // unreliable redundancy to make up for potential message drops
                    RpcServerToClientDelta_Scale(lastSerializedBaselineTick, scale);
                    if (unreliableRedundancy)
                        RpcServerToClientDelta_Scale(lastSerializedBaselineTick, scale);
                }

                lastDeltaTime = localTime;
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
            // server broadcasts all objects all the time.
            // -> not just ServerToClient: ClientToServer need to be broadcast to others too

            // perf: only grab NetworkTime.localTime property once.
            double localTime = NetworkTime.localTime;

            // broadcast
            UpdateServerBaseline(localTime);
            UpdateServerDelta(localTime);

            // interpolate remote clients
            UpdateServerInterpolation();
        }

        // update client ///////////////////////////////////////////////////////
        void UpdateClientBaseline(double localTime)
        {
            // only sync on change: only resend baseline if changed since last.
            if (onlySyncOnChange && !changedSinceBaseline) return;

            // send a reliable baseline every 1 Hz
            if (localTime >= lastBaselineTime + baselineInterval)
            {
                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
                Vector3 scale = target.localScale;

                // save bandwidth by only transmitting what is needed.
                // -> ArraySegment with random data is slower since byte[] copying
                // -> Vector3? and Quaternion? nullables takes more bandwidth
                byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!

                if (syncPosition && syncRotation && syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_PositionRotationScale(frameCount, position, rotation, scale);
                }
                else if (syncPosition && syncRotation)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_PositionRotation(frameCount, position, rotation);
                }
                else if (syncPosition && syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_PositionScale(frameCount, position, scale);
                }
                else if (syncRotation && syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_RotationScale(frameCount, rotation, scale);
                }
                else if (syncPosition)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_Position(frameCount, position);
                }
                else if (syncRotation)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_Rotation(frameCount, rotation);
                }
                else if (syncScale)
                {
                    // no unreliable redundancy: baseline is reliable
                    CmdClientToServerBaseline_Scale(frameCount, scale);
                }

                // save the last baseline's tick number.
                // included in baseline to identify which one it was on client
                // included in deltas to ensure they are on top of the correct baseline
                lastSerializedBaselineTick = frameCount;
                lastBaselineTime = NetworkTime.localTime;
                lastSerializedBaselinePosition = position;
                lastSerializedBaselineRotation = rotation;
                lastSerializedBaselineScale = scale;

                // baseline was just sent after a change. reset change detection.
                changedSinceBaseline = false;

                // perf. & bandwidth optimization:
                // send a delta right after baseline to avoid potential head of
                // line blocking, or skip the delta whenever we sent reliable?
                // for example:
                //    1 Hz baseline
                //   10 Hz delta
                //   => 11 Hz total if we still send delta after reliable
                //   => 10 Hz total if we skip delta after reliable
                // in that case, skip next delta by simply resetting last delta sync's time.
                if (baselineIsDelta) lastDeltaTime = localTime;
            }
        }

        void UpdateClientDelta(double localTime)
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
            if (localTime >= lastDeltaTime + sendInterval) // CUSTOM CHANGE: allow custom sendRate + sendInterval again
            {
                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
                Vector3 scale = target.localScale;

                // look for changes every unreliable sendInterval!
                //
                // every reliable interval isn't enough, this would cause MrG's grid issue:
                //   client start in A1, reliable baseline sent to server
                //   client moves to A2, unreliabe delta sent to server
                //   client moves to A1, nothing is sent to server becuase last baseline position == position
                //   => server wouldn't know we moved back to A1
                // every update works, but it's unnecessary overhead since sends only happen every sendInterval
                // every unreliable sendInterval is the perfect place to look for changes.
                if (onlySyncOnChange && Changed(position, rotation, scale))
                    changedSinceBaseline = true;

                // only sync on change:
                // unreliable isn't guaranteed to be delivered so this depends on reliable baseline.
                if (onlySyncOnChange && !changedSinceBaseline) return;

                // save bandwidth by only transmitting what is needed.
                // -> ArraySegment with random data is slower since byte[] copying
                // -> Vector3? and Quaternion? nullables takes more bandwidth

                if (syncPosition && syncRotation && syncScale)
                {
                    CmdClientToServerDelta_PositionRotationScale(lastSerializedBaselineTick, position, rotation, scale);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_PositionRotationScale(lastSerializedBaselineTick, position, rotation, scale);
                }
                else if (syncPosition && syncRotation)
                {
                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    // unreliable redundancy to make up for potential message drops
                    CmdClientToServerDelta_PositionRotation(lastSerializedBaselineTick, position, rotation);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_PositionRotation(lastSerializedBaselineTick, position, rotation);

                }
                else if (syncPosition && syncScale)
                {
                    CmdClientToServerDelta_PositionScale(lastSerializedBaselineTick, position, scale);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_PositionScale(lastSerializedBaselineTick, position, scale);
                }
                else if (syncRotation && syncScale)
                {
                    CmdClientToServerDelta_RotationScale(lastSerializedBaselineTick, rotation, scale);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_RotationScale(lastSerializedBaselineTick, rotation, scale);
                }
                else if (syncPosition)
                {
                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    // unreliable redundancy to make up for potential message drops
                    CmdClientToServerDelta_Position(lastSerializedBaselineTick, position);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_Position(lastSerializedBaselineTick, position);
                }
                else if (syncRotation)
                {
                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    // unreliable redundancy to make up for potential message drops
                    CmdClientToServerDelta_Rotation(lastSerializedBaselineTick, rotation);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_Rotation(lastSerializedBaselineTick, rotation);
                }
                else if (syncScale)
                {
                    CmdClientToServerDelta_Scale(lastSerializedBaselineTick, scale);
                    if (unreliableRedundancy)
                        CmdClientToServerDelta_Scale(lastSerializedBaselineTick, scale);
                }

                lastDeltaTime = localTime;
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

                // perf: only grab NetworkTime.localTime property once.
                double localTime = NetworkTime.localTime;

                UpdateClientBaseline(localTime);
                UpdateClientDelta(localTime);
            }
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer
            else
            {
                UpdateClientInterpolation();
            }
        }

        // Update() without LateUpdate() split: otherwise perf. is cut in half!
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
            // default to ClientToServer so this works immediately for users
            syncDirection = SyncDirection.ClientToServer;

            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();

            // reset baseline
            lastSerializedBaselineTick = 0;
            lastSerializedBaselinePosition = Vector3.zero;
            lastSerializedBaselineRotation = Quaternion.identity;
            lastSerializedBaselineScale    = Vector3.one;
            changedSinceBaseline = false;

            lastDeserializedBaselineTick = 0;
            lastDeserializedBaselinePosition = Vector3.zero;
            lastDeserializedBaselineRotation = Quaternion.identity;
            lastDeserializedBaselineScale    = Vector3.one;

            // Debug.Log($"[{name}] Reset to baselineTick=0");
        }

        protected virtual void OnDisable() => Reset();
        protected virtual void OnEnable() => Reset();

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // OnSerialize(initial) is called every time when a player starts observing us.
            // note this is _not_ called just once on spawn.

            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                // spawn message is used as first baseline.
                // perf: get position/rotation directly. TransformSnapshot is too expensive.
                // TransformSnapshot snapshot = ConstructSnapshot();
                target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
                Vector3 scale = target.localScale;

                // always include the tick for deltas to compare against.
                byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!
                writer.WriteByte(frameCount);

                if (syncPosition) writer.WriteVector3(position);
                if (syncRotation) writer.WriteQuaternion(rotation);
                if (syncScale)    writer.WriteVector3(scale);

                // IMPORTANT
                // OnSerialize(initial) is called for the spawn payload whenever
                // someone starts observing this object. we always must make
                // this the new baseline, otherwise this happens:
                //   - server broadcasts baseline @ t=1
                //   - server broadcasts delta for baseline @ t=1
                //   - ... time passes ...
                //   - new observer -> OnSerialize sends current position @ t=2
                //   - server broadcasts delta for baseline @ t=1
                //   => client's baseline is t=2 but receives delta for t=1 _!_
                lastSerializedBaselineTick = frameCount;
                lastBaselineTime = NetworkTime.localTime;
                lastSerializedBaselinePosition = position;
                lastSerializedBaselineRotation = rotation;
                lastSerializedBaselineScale    = scale;
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                // save last deserialized baseline tick number to compare deltas against
                lastDeserializedBaselineTick = reader.ReadByte();
                Vector3 position = Vector3.zero;
                Quaternion rotation = Quaternion.identity;
                Vector3 scale = Vector3.one;

                if (syncPosition)
                {
                    position = reader.ReadVector3();
                    lastDeserializedBaselinePosition = position;
                }
                if (syncRotation)
                {
                    rotation = reader.ReadQuaternion();
                    lastDeserializedBaselineRotation = rotation;
                }
                if (syncScale)
                {
                    scale = reader.ReadVector3();
                    lastDeserializedBaselineScale = scale;
                }

                // if baseline counts as delta, insert it into snapshot buffer too
                if (baselineIsDelta)
                    OnServerToClientDeltaSync(lastDeserializedBaselineTick, position, rotation, scale);
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
