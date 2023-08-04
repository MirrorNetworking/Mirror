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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public enum CoordinateSpace { Local, World }

    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        // target transform to sync. can be on a child.
        // TODO this field is kind of unnecessary since we now support child NetworkBehaviours
        [Header("Target")]
        [Tooltip("The Transform component to sync. May be on on this GameObject, or on a child.")]
        public Transform target;

        // TODO SyncDirection { ClientToServer, ServerToClient } is easier?
        // Deprecated 2022-10-25
        [Obsolete("NetworkTransform clientAuthority was replaced with syncDirection. To enable client authority, set SyncDirection to ClientToServer in the Inspector.")]
        [Header("[Obsolete]")] // Unity doesn't show obsolete warning for fields. do it manually.
        [Tooltip("Obsolete: NetworkTransform clientAuthority was replaced with syncDirection. To enable client authority, set SyncDirection to ClientToServer in the Inspector.")]
        public bool clientAuthority;
        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        protected bool IsClientWithAuthority => isClient && authority;

        // snapshots with initial capacity to avoid early resizing & allocations: see NetworkRigidbodyBenchmark example.
        public readonly SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>(16);
        public readonly SortedList<double, TransformSnapshot> serverSnapshots = new SortedList<double, TransformSnapshot>(16);

        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync\nDon't change these at Runtime")]
        public bool syncPosition = true;  // do not change at runtime!
        public bool syncRotation = true;  // do not change at runtime!
        public bool syncScale = false; // do not change at runtime! rare. off by default.

        // interpolation is on by default, but can be disabled to jump to
        // the destination immediately. some projects need this.
        [Header("Interpolation")]
        [Tooltip("Set to false to have a snap-like effect on position movement.")]
        public bool interpolatePosition = true;
        [Tooltip("Set to false to have a snap-like effect on rotations.")]
        public bool interpolateRotation = true;
        [Tooltip("Set to false to remove scale smoothing. Example use-case: Instant flipping of sprites that use -X and +X for direction.")]
        public bool interpolateScale = true;

        // CoordinateSpace ///////////////////////////////////////////////////////////
        [Header("Coordinate Space")]
        [Tooltip("Local by default. World may be better when changing hierarchy, or non-NetworkTransforms root position/rotation/scale values.")]
        public CoordinateSpace coordinateSpace = CoordinateSpace.Local;

        [Header("Send Interval Multiplier")]
        [Tooltip("Check/Sync every multiple of Network Manager send interval (= 1 / NM Send Rate), instead of every send interval.\n(30 NM send rate, and 3 interval, is a send every 0.1 seconds)\nA larger interval means less network sends, which has a variety of upsides. The drawbacks are delays and lower accuracy, you should find a nice balance between not sending too much, but the results looking good for your particular scenario.")]
        [Range(1, 120)]
        public uint sendIntervalMultiplier = 1;

        [Header("Timeline Offset")]
        [Tooltip("Add a small timeline offset to account for decoupled arrival of NetworkTime and NetworkTransform snapshots.\nfixes: https://github.com/MirrorNetworking/Mirror/issues/3427")]
        public bool timelineOffset = false;

        // Ninja's Notes on offset & mulitplier:
        //
        // In a no multiplier scenario:
        // 1. Snapshots are sent every frame (frame being 1 NM send interval).
        // 2. Time Interpolation is set to be 'behind' by 2 frames times.
        // In theory where everything works, we probably have around 2 snapshots before we need to interpolate snapshots. From NT perspective, we should always have around 2 snapshots ready, so no stutter.
        //
        // In a multiplier scenario:
        // 1. Snapshots are sent every 10 frames.
        // 2. Time Interpolation remains 'behind by 2 frames'.
        // When everything works, we are receiving NT snapshots every 10 frames, but start interpolating after 2.
        // Even if I assume we had 2 snapshots to begin with to start interpolating (which we don't), by the time we reach 13th frame, we are out of snapshots, and have to wait 7 frames for next snapshot to come. This is the reason why we absolutely need the timestamp adjustment. We are starting way too early to interpolate.
        //
        protected double timeStampAdjustment => NetworkServer.sendInterval * (sendIntervalMultiplier - 1);
        protected double offset => timelineOffset ? NetworkServer.sendInterval * sendIntervalMultiplier : 0;

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public bool showGizmos;
        public bool showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // initialization //////////////////////////////////////////////////////
        // make sure to call this when inheriting too!
        protected virtual void Awake() { }

        protected override void OnValidate()
        {
            base.OnValidate();

            // set target to self if none yet
            if (target == null) target = transform;

            // time snapshot interpolation happens globally.
            // value (transform) happens in here.
            // both always need to be on the same send interval.
            // force the setting to '0' in OnValidate to make it obvious that we
            // actually use NetworkServer.sendInterval.
            syncInterval = 0;

            // Unity doesn't support setting world scale.
            // OnValidate force disables syncScale in world mode.
            if (coordinateSpace == CoordinateSpace.World) syncScale = false;

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
        // get local/world position
        protected Vector3 GetPosition() =>
            coordinateSpace == CoordinateSpace.Local ? target.localPosition : target.position;

        // get local/world rotation
        protected Quaternion GetRotation() =>
            coordinateSpace == CoordinateSpace.Local ? target.localRotation : target.rotation;

        // get local/world scale
        protected Vector3 GetScale() =>
            coordinateSpace == CoordinateSpace.Local ? target.localScale    : target.lossyScale;

        // set local/world position
        protected void SetPosition(Vector3 position)
        {
            if (coordinateSpace == CoordinateSpace.Local)
                target.localPosition = position;
            else
                target.position = position;
        }

        // set local/world rotation
        protected void SetRotation(Quaternion rotation)
        {
            if (coordinateSpace == CoordinateSpace.Local)
                target.localRotation = rotation;
            else
                target.rotation = rotation;
        }

        // set local/world position
        protected void SetScale(Vector3 scale)
        {
            if (coordinateSpace == CoordinateSpace.Local)
                target.localScale = scale;
            // Unity doesn't support setting world scale.
            // OnValidate disables syncScale in world mode.
            // else
                // target.lossyScale = scale; // TODO
        }

        // construct a snapshot of the current state
        // => internal for testing
        protected virtual TransformSnapshot Construct()
        {
            // NetworkTime.localTime for double precision until Unity has it too
            return new TransformSnapshot(
                // our local time is what the other end uses as remote time
                NetworkTime.localTime, // Unity 2019 doesn't have timeAsDouble yet
                0,                     // the other end fills out local time itself
                GetPosition(),
                GetRotation(),
                GetScale()
            );
        }

        protected void AddSnapshot(SortedList<double, TransformSnapshot> snapshots, double timeStamp, Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.

            if (!position.HasValue) position = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].position : GetPosition();
            if (!rotation.HasValue) rotation = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].rotation : GetRotation();
            if (!scale.HasValue)    scale    = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].scale    : GetScale();

            // insert transform snapshot
            SnapshotInterpolation.InsertIfNotExists(
                snapshots,
                NetworkClient.snapshotSettings.bufferLimit,
                new TransformSnapshot(
                    timeStamp, // arrival remote timestamp. NOT remote time.
                    NetworkTime.localTime, // Unity 2019 doesn't have timeAsDouble yet
                    position.Value,
                    rotation.Value,
                    scale.Value
                )
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
        protected virtual void Apply(TransformSnapshot interpolated, TransformSnapshot endGoal)
        {
            // local position/rotation for VR support
            //
            // if syncPosition/Rotation/Scale is disabled then we received nulls
            // -> current position/rotation/scale would've been added as snapshot
            // -> we still interpolated
            // -> but simply don't apply it. if the user doesn't want to sync
            //    scale, then we should not touch scale etc.

            // interpolate parts
            if (syncPosition) SetPosition(interpolatePosition ? interpolated.position : endGoal.position);
            if (syncRotation) SetRotation(interpolateRotation ? interpolated.rotation : endGoal.rotation);
            if (syncScale)       SetScale(interpolateScale    ? interpolated.scale    : endGoal.scale);
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

        [ClientRpc]
        void RpcReset()
        {
            Reset();
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

        public virtual void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();
        }

        protected virtual void OnEnable()
        {
            Reset();

            if (NetworkServer.active)
                NetworkIdentity.clientAuthorityCallback += OnClientAuthorityChanged;
        }

        protected virtual void OnDisable()
        {
            Reset();

            if (NetworkServer.active)
                NetworkIdentity.clientAuthorityCallback -= OnClientAuthorityChanged;
        }

        [ServerCallback]
        void OnClientAuthorityChanged(NetworkConnectionToClient conn, NetworkIdentity identity, bool authorityState)
        {
            if (identity != netIdentity) return;

            // If server gets authority or syncdirection is server to client,
            // we don't reset buffers.
            // This is because if syncdirection is S to C, we will never have
            // snapshot issues since there is only ever 1 source.

            if (syncDirection == SyncDirection.ClientToServer)
            {
                Reset();
                RpcReset();
            }
        }

        // OnGUI allocates even if it does nothing. avoid in release.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // debug ///////////////////////////////////////////////////////////////
        protected virtual void OnGUI()
        {
            if (!showOverlay) return;
            if (!Camera.main) return;

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
                Gizmos.DrawWireCube(entry.position, Vector3.one);
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
