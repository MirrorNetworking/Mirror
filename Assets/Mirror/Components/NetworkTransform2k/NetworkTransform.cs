// NetworkTransform V3 by mischa (2022-10)
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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform")]
    public class NetworkTransform : NetworkBehaviour
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

        internal SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>();
        internal SortedList<double, TransformSnapshot> serverSnapshots = new SortedList<double, TransformSnapshot>();

        [Header("Sync Only If Changed")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;

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
        public float scalePrecision    = 0.01f; // 1 cm

        // TODO quaterion isn't compressed yet
        public float rotationSensitivity = 0.01f;

        // Used to store last sent snapshots
        protected TransformSnapshot last;

        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync & interpolation")]
        public bool syncPosition = true;
        public bool syncRotation = true;
        public bool syncScale    = false; // rare. off by default.

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public bool showGizmos;
        public bool  showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // initialization //////////////////////////////////////////////////////
        // make sure to call this when inheriting too!
        protected virtual void Awake() {}

        protected virtual void OnValidate()
        {
            // set target to self if none yet
            if (target == null) target = transform;

            // time snapshot interpolation happens globally.
            // value (transform) happens in here.
            // both always need to be on the same send interval.
            // force it in here.
            syncInterval = NetworkServer.sendInterval;

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
        protected virtual TransformSnapshot Construct()
        {
            // NetworkTime.localTime for double precision until Unity has it too
            return new TransformSnapshot(
                // our local time is what the other end uses as remote time
                NetworkTime.localTime, // Unity 2019 doesn't have timeAsDouble yet
                // the other end fills out local time itself
                0,
                target.localPosition,
                target.localRotation,
                target.localScale
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
        protected virtual void Apply(TransformSnapshot interpolated)
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

        // check if position / rotation / scale changed since last sync
        protected virtual bool Changed(TransformSnapshot current)
        {
            // position is quantized.
            // only resync if the quantized representation changed.
            Compression.ScaleToLong(last.position,    positionPrecision, out long px0, out long py0, out long pz0);
            Compression.ScaleToLong(current.position, positionPrecision, out long px1, out long py1, out long pz1);
            if (px0 != px1 || py0 != py1 || pz0 != pz1) return true;

            // quaternion isn't compressed yet.
            if (Quaternion.Angle(last.rotation, current.rotation) > rotationSensitivity)
                return true;

            // scale is quantized.
            // only resync if the quantized representation changed.
            Compression.ScaleToLong(last.scale,    scalePrecision, out long sx0, out long sy0, out long sz0);
            Compression.ScaleToLong(current.scale, scalePrecision, out long sx1, out long sy1, out long sz1);
            if (sx0 != sx1 || sy0 != sy1 || sz0 != sz1) return true;

            return false;
        }

        // sync ////////////////////////////////////////////////////////////////
        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

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
            if (!scale.HasValue)    scale    = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].scale    : target.localScale;

            // insert transform snapshot
            SnapshotInterpolation.InsertIfNotExists(serverSnapshots, new TransformSnapshot(
                connectionToClient.remoteTimeStamp, // arrival remote timestamp. NOT remote time.
                NetworkTime.localTime,              // Unity 2019 doesn't have timeAsDouble yet
                position.Value,
                rotation.Value,
                scale.Value
            ));
        }

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // don't apply for local player with authority
            if (IsClientWithAuthority) return;

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
            if (!scale.HasValue)    scale = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].scale : target.localScale;

            // insert snapshot
            SnapshotInterpolation.InsertIfNotExists(clientSnapshots, new TransformSnapshot(
                NetworkClient.connection.remoteTimeStamp, // arrival remote timestamp. NOT remote time.
                NetworkTime.localTime,                    // Unity 2019 doesn't have timeAsDouble yet
                position.Value,
                rotation.Value,
                scale.Value
            ));
        }

        // delta position //////////////////////////////////////////////////////
        // quantize -> delta -> varint
        // small changes will be sent as just 1 byte.
        internal static void SerializeDeltaPosition(NetworkWriter writer, Vector3 previous, Vector3 current, float precision)
        {
            // quantize 'last' and 'current'.
            // quantized current could be stored as 'last' later if too slow.
            Compression.ScaleToLong(previous, precision, out long x0, out long y0, out long z0);
            Compression.ScaleToLong(current,  precision, out long x1, out long y1, out long z1);

            // compute the difference. usually small.
            long dx = x1 - x0;
            long dy = y1 - y0;
            long dz = z1 - z0;

            // zigzag varint the difference. usually requires very few bytes.
            Compression.CompressVarInt(writer, dx);
            Compression.CompressVarInt(writer, dy);
            Compression.CompressVarInt(writer, dz);
        }

        internal static Vector3 DeserializeDeltaPosition(NetworkReader reader, Vector3 previous, float precision)
        {
            // zigzag varint
            long dx = Compression.DecompressVarInt(reader);
            long dy = Compression.DecompressVarInt(reader);
            long dz = Compression.DecompressVarInt(reader);

            // current := last + delta
            Compression.ScaleToLong(previous, precision, out long x0, out long y0, out long z0);
            long x1 = x0 + dx;
            long y1 = y0 + dy;
            long z1 = z0 + dz;

            // revert quantization
            return Compression.ScaleToFloat(x1, y1, z1, precision);
        }

        // delta rotation //////////////////////////////////////////////////////
        // TODO compress
        internal static void SerializeDeltaRotation(NetworkWriter writer, Quaternion last, Quaternion current) =>
            writer.WriteQuaternion(current);

        internal static Quaternion DeserializeDeltaRotation(NetworkReader reader, Quaternion last) =>
            reader.ReadQuaternion();

        // delta scale /////////////////////////////////////////////////////////
        // quantize -> delta -> varint
        // small changes will be sent as just 1 byte.
        // reuses the delta position code.
        internal static void SerializeDeltaScale(NetworkWriter writer, Vector3 previous, Vector3 current, float precision) =>
            SerializeDeltaPosition(writer, previous, current, precision);

        internal static Vector3 DeserializeDeltaScale(NetworkReader reader, Vector3 previous, float precision) =>
            DeserializeDeltaPosition(reader, previous, precision);

        // OnSerialize /////////////////////////////////////////////////////////
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // TODO for interpolated client owned identities,
            // always broadcast the latest known snapshot so other clients can
            // interpolate immediately instead of catching up too

            TransformSnapshot snapshot = Construct();

            int before = writer.Position;

            // first spawns are serialized in full.
            if (initialState)
            {
                // write original floats.
                // quantization is only worth it for delta.
                if (syncPosition) writer.WriteVector3(snapshot.position);
                if (syncRotation) writer.WriteQuaternion(snapshot.rotation);
                if (syncScale)    writer.WriteVector3(snapshot.scale);
            }
            // delta since last
            else
            {
                // TODO dirty mask so unchanged components aren't sent at all

                if (syncPosition) SerializeDeltaPosition(writer, last.position, snapshot.position, positionPrecision);
                if (syncRotation) SerializeDeltaRotation(writer, last.rotation, snapshot.rotation);
                if (syncScale)    SerializeDeltaScale   (writer, last.scale,    snapshot.scale,    scalePrecision);

                // TODO log compression ratio and see
            }

            // set 'last'
            // TODO this will break if selective sync changes at runtime.
            //      better to store pos/rot/scale separately and only if serialized
            last = snapshot;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            Vector3?    position = null;
            Quaternion? rotation = null;
            Vector3?    scale    = null;

            // first spawns are serialized in full.
            if (initialState)
            {
                if (syncPosition) position = reader.ReadVector3();
                if (syncRotation) rotation = reader.ReadQuaternion();
                if (syncScale)    scale    = reader.ReadVector3();
            }
            // delta since last
            else
            {
                if (syncPosition) position = DeserializeDeltaPosition(reader, last.position, positionPrecision);
                if (syncRotation) rotation = DeserializeDeltaRotation(reader, last.rotation);
                if (syncScale)    scale    = DeserializeDeltaScale   (reader, last.scale,    scalePrecision);
            }

            // handle depending on server / client / host.
            // server has priority for host mode.
            if      (isServer) OnClientToServerSync(position, rotation, scale);
            else if (isClient) OnServerToClientSync(position, rotation, scale);

            // store 'last' for next delta.
            // TODO this will break if selective sync changes at runtime.
            //      better to store pos/rot/scale separately and only if deserialized
            if (position.HasValue) last.position = position.Value;
            if (rotation.HasValue) last.rotation = rotation.Value;
            if (scale.HasValue)    last.scale    = scale.Value;
        }

        // update //////////////////////////////////////////////////////////////
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
            if (syncDirection == SyncDirection.ClientToServer && !isOwned)
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
            }
        }

        void Update()
        {
            // if server then always sync to others.
            if      (isServer) UpdateServer();
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

        public virtual void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();
        }

        protected virtual void OnDisable() => Reset();
        protected virtual void OnEnable()  => Reset();

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
