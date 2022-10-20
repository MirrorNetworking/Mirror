// NetworkTransform V3 via MMORPG style begin/end/move instead of always sending.
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        // target transform to sync. can be on a child.
        protected abstract Transform targetComponent { get; }

        [Tooltip("Buffer size limit to avoid ever growing list memory consumption attacks.")]
        public int bufferSizeLimit = 64;

        internal SortedList<double, TransformSnapshot> snapshots =
            new SortedList<double, TransformSnapshot>();

        [Header("Sensitivity")]
        [Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float positionSensitivity = 0.01f;
        public float rotationSensitivity = 0.01f;
        public float scaleSensitivity    = 0.01f;

        // store last sent data for comparison
        TransformSnapshot last;

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

        // snapshot functions //////////////////////////////////////////////////
        // construct a snapshot of the current state
        // => internal for testing
        protected virtual TransformSnapshot Construct()
        {
            // NetworkTime.localTime for double precision until Unity has it too
            return new TransformSnapshot(
                // our local time is what the other end uses as remote time
#if !UNITY_2020_3_OR_NEWER
                NetworkTime.localTime, // Unity 2019 doesn't have timeAsDouble yet
#else
                Time.timeAsDouble,
#endif
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
        protected virtual void Apply(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // local position/rotation for VR support
            //
            // if syncPosition/Rotation/Scale is disabled then we received nulls
            // -> current position/rotation/scale would've been added as snapshot
            // -> we still interpolated
            // -> but simply don't apply it. if the user doesn't want to sync
            //    scale, then we should not touch scale etc.
            if (syncPosition) targetComponent.localPosition = position;
            if (syncRotation) targetComponent.localRotation = rotation;
            if (syncScale)    targetComponent.localScale    = scale;
        }

        // check if position / rotation / scale have changed.
        // depending on which we mean to sync.
        protected virtual bool Changed()
        {
            TransformSnapshot current = Construct();

            if (syncPosition && Vector3.SqrMagnitude(last.position - current.position) > positionSensitivity * positionSensitivity)
                return true;

            if (syncRotation && Quaternion.Angle(last.rotation, current.rotation)      > rotationSensitivity)
                return true;

            if (syncScale    && Vector3.SqrMagnitude(last.scale - current.scale)       > scaleSensitivity * scaleSensitivity)
                return true;

            return false;
        }

        // trigger OnSerialize if position / rotation / scale changed
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetDirtyIfChanged()
        {
            // TODO .SetDirty() instead of the dirty bit workaround
            if (Changed())
                SetSyncVarDirtyBit(1);
        }

        // update //////////////////////////////////////////////////////////////
        void UpdateServer()
        {
            // set dirty if changed, no matter which SyncDirection.
            // even for ClientToServer, we still need to broadcast to others.
            SetDirtyIfChanged();

            // TODO interpolate in host mode?
        }

        void UpdateClient()
        {
            // do nothing during scene changes:
            // https://github.com/vis2k/Mirror/pull/2992/
            if (!NetworkClient.ready) return;

            // client authority, and local player (= allowed to move myself)?
            if (authority)
            {
                SetDirtyIfChanged();
            }
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer
            else
            {
                // only while we have snapshots
                if (snapshots.Count > 0)
                {
                    // step the interpolation without touching time.
                    // NetworkClient is responsible for time globally.
                    SnapshotInterpolation.StepInterpolation(
                        snapshots,
                        NetworkTime.time, // == NetworkClient.localTimeline from snapshot interpolation
                        out TransformSnapshot from,
                        out TransformSnapshot to,
                        out double t);

                    // interpolate & apply
                    TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);
                    Apply(computed.position, computed.rotation, computed.scale);
                }
            }
        }

        void Update()
        {
            // if server then always sync to others.
            // server hast highest priority (i.e. host mode).
            if (isServer)      UpdateServer();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClient();
        }

        void SerializeInitial(NetworkWriter writer, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (syncPosition) writer.WriteVector3(position);
            if (syncRotation) writer.WriteQuaternion(rotation);
            if (syncScale)    writer.WriteVector3(scale);
        }

        void SerializeDelta(NetworkWriter writer, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // TODO changed mask, compression, etc.
            if (syncPosition) writer.WriteVector3(position);
            if (syncRotation) writer.WriteQuaternion(rotation);
            if (syncScale)    writer.WriteVector3(scale);
        }

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // get position/rotation/scale from target transform once.
            // multiple .transform access are expensive.
            // local for VR support.
            TransformSnapshot current = Construct();

            // Debug.Log($"{name} OnSerialize initial={initialState} @ {Time.timeAsDouble:F3}");

            // send everything on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                SerializeInitial(writer, current.position, current.rotation, current.scale);
            }
            // otherwise only send what's changed
            else
            {
                SerializeDelta(writer, current.position, current.rotation, current.scale);
            }

            // either way, store last sent data for comparison
            last = current;
        }

        void DeserializeInitial(NetworkReader reader)
        {
            // OnDeserialize with initialState is always called on clients.
            // never on server.
            // don't need to do any validation here.
            Vector3    position = targetComponent.localPosition;
            Quaternion rotation = targetComponent.localRotation;
            Vector3    scale    = targetComponent.localScale;

            if (syncPosition) position = reader.ReadVector3();
            if (syncRotation) rotation = reader.ReadQuaternion();
            if (syncScale)    scale    = reader.ReadVector3();

            Apply(position, rotation, scale);
        }

        // when syncing from server to client, insert for snapshot interp.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DeserializeDeltaClient(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // deserialize is called on all clients, even with client authority.
            // because even with client authority, the server still decides
            // spawn position. otherwise the owner client would have no data to
            // decide where to spawn, resulting in (0,0,0) at all times.
            //
            // for owners with authority, we simply apply spawn position
            // without adding the snapshot.
            //
            // for non owners, we do add the snapshot and also apply the spawn.

            // insert snapshot.
            if (!authority)
            {
                SnapshotInterpolation.InsertIfNotExists(snapshots, new TransformSnapshot(
                    // arrival remote timestamp. NOT remote time.
                    // NetworkClient.conn works for all objects.
                    // .connectionToServer only for player owned objects.
                    NetworkClient.connection.remoteTimeStamp,
#if !UNITY_2020_3_OR_NEWER
                    // Unity 2019 doesn't have timeAsDouble yet
                    NetworkTime.localTime,
#else
                    Time.timeAsDouble,
#endif
                    position,
                    rotation,
                    scale
                ));
            }
        }

        // overwrite this to validate movement.
        // this may check velocity, physics, navmesh, etc.
        // note those are .localPosition etc.
        // make sure to consider selective sync settings like syncPosition.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool Validate(Vector3 localPosition, Quaternion localRotation, Vector3 localScale) =>
            true;

        // when syncing from client to server, validate and apply directly.
        // snapshot interpolation is only ever applied on the client for
        // smooth movement.
        // the server needs immediate results to not have it lag behind.
        // besides, nobody is watching the movement on the server :)
        //
        // no 'initialState' parameter. server always knows initial state.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DeserializeDeltaServer(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (Validate(position, rotation, scale))
                Apply(position, rotation, scale);
        }

        void DeserializeDelta(NetworkReader reader)
        {
            // TODO changed mask, compression, etc.
            Vector3    position = syncPosition ? reader.ReadVector3()    : targetComponent.localPosition;
            Quaternion rotation = syncRotation ? reader.ReadQuaternion() : targetComponent.localRotation;
            Vector3    scale    = syncScale    ? reader.ReadVector3()    : targetComponent.localScale;

            // OnDeserialize with delta may be called on client or server.
            // need to handle both cases separately
            // server hast highest priority (i.e. host mode).
            if      (isServer) DeserializeDeltaServer(position, rotation, scale);
            else if (isClient) DeserializeDeltaClient(position, rotation, scale);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // send everything on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                DeserializeInitial(reader);
            }
            // otherwise only deserialize what's changed
            else
            {
                DeserializeDelta(reader);
            }
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
            snapshots.Clear();
        }

        protected virtual void OnDisable() => Reset();
        protected virtual void OnEnable() => Reset();

        protected virtual void OnValidate()
        {
            // buffer limit should be at least multiplier to have enough in there
            bufferSizeLimit = Mathf.Max((int)NetworkClient.bufferTimeMultiplier, bufferSizeLimit);
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

                if (snapshots.Count > 0)
                    GUILayout.Label($"Client Buffer:{snapshots.Count}");

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
            Gizmos.DrawLine(buffer.Values[0].position, targetComponent.position);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(targetComponent.position, buffer.Values[1].position);
        }

        protected virtual void OnDrawGizmos()
        {
            // This fires in edit mode but that spams NRE's so check isPlaying
            if (!Application.isPlaying) return;
            if (!showGizmos) return;

            if (isClient) DrawGizmos(snapshots);
        }
#endif
    }
}
