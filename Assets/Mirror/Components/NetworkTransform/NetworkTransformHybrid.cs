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
    public class NetworkTransformHybrid : NetworkBehaviourHybrid
    {
        public bool useFixedUpdate;
        TransformSnapshot? pendingSnapshot;

        // target transform to sync. can be on a child.
        [Header("Target")]
        [Tooltip("The Transform component to sync. May be on this GameObject, or on a child.")]
        public Transform target;

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

        // delta compression needs to remember 'last' to compress against.
        // this is from reliable full state serializations, not from last
        // unreliable delta since that isn't guaranteed to be delivered.
        Vector3 lastSerializedBaselinePosition = Vector3.zero;
        Quaternion lastSerializedBaselineRotation = Quaternion.identity;
        Vector3 lastSerializedBaselineScale = Vector3.one;

        // save last deserialized baseline to delta decompress against
        Vector3 lastDeserializedBaselinePosition = Vector3.zero;                // unused, but keep for delta
        Quaternion lastDeserializedBaselineRotation = Quaternion.identity;      // unused, but keep for delta
        Vector3 lastDeserializedBaselineScale = Vector3.one;                    // unused, but keep for delta

        // sensitivity is for changed-detection,
        // this is != precision, which is for quantization and delta compression.
        [Header("Sensitivity"), Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float positionSensitivity = 0.01f;
        public float rotationSensitivity = 0.01f;
        public float scaleSensitivity    = 0.01f;

        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync & interpolation")]
        public bool syncPosition = true;
        public bool syncRotation = true;
        public bool syncScale    = false;

        // velocity for convenience (animators etc.)
        // this isn't technically NetworkTransforms job, but it's needed by so many projects that we just provide it anyway.
        public Vector3 velocity { get; private set; }
        public Vector3 angularVelocity { get; private set; }

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
            // Skip if Editor is in Play mode
            if (Application.isPlaying) return;

            base.OnValidate();
            Reset();
        }

        void Reset()
        {
            // set target to self if none yet
            if (target == null) target = transform;

            // we use sendRate for convenience.
            // but project it to syncInterval for NetworkTransformHybrid to work properly.
            syncInterval = sendInterval;

            // default to ClientToServer so this works immediately for users
            syncDirection = SyncDirection.ClientToServer;
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

            // calculate the velocity and angular velocity for the object
            // these can be used to drive animations or other behaviours
            if (!isOwned && Time.deltaTime > 0)
            {
                velocity = (transform.localPosition - interpolated.position) / Time.deltaTime;
                angularVelocity = (transform.localRotation.eulerAngles - interpolated.rotation.eulerAngles) / Time.deltaTime;
            }

            if (syncPosition) target.localPosition = interpolated.position;
            if (syncRotation) target.localRotation = interpolated.rotation;
            if (syncScale)    target.localScale    = interpolated.scale;
        }

        // store state after baseline sync
        protected override void StoreState()
        {
            target.GetLocalPositionAndRotation(out lastSerializedBaselinePosition, out lastSerializedBaselineRotation);
            lastSerializedBaselineScale = target.localScale;
        }

        // check if position / rotation / scale changed since last _full reliable_ sync.
        // squared comparisons for performance
        protected override bool StateChanged()
        {
            target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
            Vector3 scale = target.localScale;

            if (syncPosition)
            {
                float positionDelta = Vector3.Distance(position, lastSerializedBaselinePosition);
                if (positionDelta >= positionSensitivity)
                {
                    return true;
                }
            }

            if (syncRotation)
            {
                float rotationDelta = Quaternion.Angle(lastSerializedBaselineRotation, rotation);
                if (rotationDelta >= rotationSensitivity)
                {
                    return true;
                }
            }

            if (syncScale)
            {
                float scaleDelta = Vector3.Distance(scale, lastSerializedBaselineScale);
                if (scaleDelta >= scaleSensitivity)
                {
                    return true;
                }
            }

            return false;
        }

        // serialization ///////////////////////////////////////////////////////
        // called on server and on client, depending on SyncDirection
        protected override void OnSerializeBaseline(NetworkWriter writer)
        {
            // perf: get position/rotation directly. TransformSnapshot is too expensive.
            // TransformSnapshot snapshot = ConstructSnapshot();
            target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
            Vector3 scale = target.localScale;

            if (syncPosition) writer.WriteVector3(position);
            if (syncRotation) writer.WriteQuaternion(rotation);
            if (syncScale)    writer.WriteVector3(scale);
        }

        // called on server and on client, depending on SyncDirection
        protected override void OnDeserializeBaseline(NetworkReader reader, byte baselineTick)
        {
            // deserialize
            Vector3?    position = null;
            Quaternion? rotation = null;
            Vector3?    scale    = null;

            if (syncPosition)
            {
                position = reader.ReadVector3();
                lastDeserializedBaselinePosition = position.Value;
            }
            if (syncRotation)
            {
                rotation = reader.ReadQuaternion();
                lastDeserializedBaselineRotation = rotation.Value;
            }
            if (syncScale)
            {
                scale    = reader.ReadVector3();
                lastDeserializedBaselineScale = scale.Value;
            }

           // debug draw: baseline = yellow
            if (debugDraw && position.HasValue) Debug.DrawLine(position.Value, position.Value + Vector3.up, Color.yellow, 10f);

            // if baseline counts as delta, insert it into snapshot buffer too
            if (baselineIsDelta)
            {
                if (isServer)
                {
                    OnClientToServerDeltaSync(position, rotation, scale);
                }
                else if (isClient)
                {
                    OnServerToClientDeltaSync(position, rotation, scale);
                }
            }
        }

        // called on server and on client, depending on SyncDirection
        protected override void OnSerializeDelta(NetworkWriter writer)
        {
            // perf: get position/rotation directly. TransformSnapshot is too expensive.
            // TransformSnapshot snapshot = ConstructSnapshot();
            target.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
            Vector3 scale = target.localScale;

            if (syncPosition) writer.WriteVector3(position);
            if (syncRotation) writer.WriteQuaternion(rotation);
            if (syncScale)    writer.WriteVector3(scale);
        }

        // called on server and on client, depending on SyncDirection
        protected override void OnDeserializeDelta(NetworkReader reader, byte baselineTick)
        {
            Vector3? position = null;
            Quaternion? rotation = null;
            Vector3? scale = null;

            if (syncPosition) position = reader.ReadVector3();
            if (syncRotation) rotation = reader.ReadQuaternion();
            if (syncScale)    scale    = reader.ReadVector3();

            // debug draw: delta = white
            if (debugDraw && position.HasValue) Debug.DrawLine(position.Value, position.Value + Vector3.up, Color.white, 10f);

            if (isServer)
            {
                OnClientToServerDeltaSync(position, rotation, scale);
            }
            else if (isClient)
            {
                OnServerToClientDeltaSync(position, rotation, scale);
            }
        }

        // processing //////////////////////////////////////////////////////////
        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerDeltaSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

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
                position.HasValue ? position.Value : Vector3.zero,
                rotation.HasValue ? rotation.Value : Quaternion.identity,
                scale.HasValue ? scale.Value : Vector3.one
            ));
        }

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientDeltaSync(Vector3? position, Quaternion? rotation, Vector3? scale)
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
                position.HasValue ? position.Value : Vector3.zero,
                rotation.HasValue ? rotation.Value : Quaternion.identity,
                scale.HasValue ? scale.Value : Vector3.one
            ));
        }

        // update server ///////////////////////////////////////////////////////
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
                    if (useFixedUpdate)
                        pendingSnapshot = computed;
                    else
                        ApplySnapshot(computed);
                }
            }
        }

        // update client ///////////////////////////////////////////////////////
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
                if (useFixedUpdate)
                    pendingSnapshot = computed;
                else
                    ApplySnapshot(computed);
            }
        }

        // Update() without LateUpdate() split: otherwise perf. is cut in half!
        protected override void Update()
        {
            base.Update(); // NetworkBehaviourHybrid

            if (isServer)
            {
                // interpolate remote clients
                UpdateServerInterpolation();
            }
            // 'else if' because host mode shouldn't update both.
            else if (isClient)
            {
                // interpolate remote client (and local player if no authority)
                if (!IsClientWithAuthority) UpdateClientInterpolation();
            }
        }

        void FixedUpdate()
        {
            if (!useFixedUpdate) return;

            if (pendingSnapshot.HasValue)
            {
                ApplySnapshot(pendingSnapshot.Value);
                pendingSnapshot = null;
            }
        }

        // common Teleport code for client->server and server->client
        protected virtual void OnTeleport(Vector3 destination)
        {
            // reset any in-progress interpolation & buffers
            ResetState();

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
            ResetState();

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

        public override void ResetState()
        {
            base.ResetState(); // NetworkBehaviourHybrid

            // disabled objects aren't updated anymore so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();

            // reset baseline
            lastSerializedBaselinePosition = Vector3.zero;
            lastSerializedBaselineRotation = Quaternion.identity;
            lastSerializedBaselineScale    = Vector3.one;

            lastDeserializedBaselinePosition = Vector3.zero;
            lastDeserializedBaselineRotation = Quaternion.identity;
            lastDeserializedBaselineScale    = Vector3.one;

            // Prevent resistance from CharacterController
            // or non-knematic Rigidbodies when teleporting.
            Physics.SyncTransforms();

            // Debug.Log($"[{name}] ResetState to baselineTick=0");
        }

        protected virtual void OnDisable() => ResetState();
        protected virtual void OnEnable() => ResetState();

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // OnSerialize(initial) is called every time when a player starts observing us.
            // note this is _not_ called just once on spawn.

            base.OnSerialize(writer, initialState); // NetworkBehaviourHybrid

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

                if (syncPosition) writer.WriteVector3(position);
                if (syncRotation) writer.WriteQuaternion(rotation);
                if (syncScale)    writer.WriteVector3(scale);
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState); // NetworkBehaviourHybrid

            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                // save last deserialized baseline tick number to compare deltas against
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
                    OnServerToClientDeltaSync(position, rotation, scale);
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
