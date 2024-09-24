// NetworkTransform V3 based on NetworkTransformUnreliable, using Mirror's new
// Unreliable quake style networking model with delta compression.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Unreliable Compressed)")]
    public class NetworkTransformUnreliableCompressed : NetworkBehaviour
    {
        // NT BASE /////////////////////////////////////////////////////////////

        // target transform to sync. can be on a child.
        // TODO this field is kind of unnecessary since we now support child NetworkBehaviours
        [Header("Target")]
        [Tooltip("The Transform component to sync. May be on on this GameObject, or on a child.")]
        public Transform target;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        protected bool IsClientWithAuthority => isClient && authority;

        // snapshots with initial capacity to avoid early resizing & allocations: see NetworkRigidbodyBenchmark example.
        public readonly SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>(16);
        public readonly SortedList<double, TransformSnapshot> serverSnapshots = new SortedList<double, TransformSnapshot>(16);

        // CoordinateSpace ///////////////////////////////////////////////////////////
        [Header("Coordinate Space")]
        [Tooltip("Local by default. World may be better when changing hierarchy, or non-NetworkTransforms root position/rotation/scale values.")]
        public CoordinateSpace coordinateSpace = CoordinateSpace.Local;

        // debugging ///////////////////////////////////////////////////////////
        protected override void OnValidate()
        {
            // Skip if Editor is in Play mode
            if (Application.isPlaying) return;

            base.OnValidate();

            // configure in awake
            Configure();
        }

        // make sure to call this when inheriting too!
        protected virtual void Awake()
        {
            // sometimes OnValidate() doesn't run before launching a project.
            // need to guarantee configuration runs.
            Configure();
        }

        // snapshot functions //////////////////////////////////////////////////
        // get local/world position
        protected virtual Vector3 GetPosition() =>
            coordinateSpace == CoordinateSpace.Local ? target.localPosition : target.position;

        // get local/world rotation
        protected virtual Quaternion GetRotation() =>
            coordinateSpace == CoordinateSpace.Local ? target.localRotation : target.rotation;

        // get local/world scale
        protected virtual Vector3 GetScale() =>
            coordinateSpace == CoordinateSpace.Local ? target.localScale : target.lossyScale;


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
            if (!scale.HasValue) scale = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].scale : GetScale();

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
            if (coordinateSpace == CoordinateSpace.Local)
            {
                target.localPosition = interpolated.position;
                target.localRotation = interpolated.rotation;
            }
            else
            {
                target.position = interpolated.position;
                target.rotation = interpolated.rotation;
                target.localScale = interpolated.scale;
            }
        }

        // NT COMPRESSED ///////////////////////////////////////////////////////
        [Header("Debug")]
        public bool debugDraw = false;

        // Used to store last sent snapshots
        protected TransformSnapshot last;

        // validation //////////////////////////////////////////////////////////
        // Configure is called from OnValidate and Awake
        protected void Configure()
        {
            // set target to self if none yet
            if (target == null) target = transform;

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
            // set dirty to trigger OnSerialize. either always, or only if changed.
            // It has to be checked in LateUpdate() for onlySyncOnChange to avoid
            // the possibility of Update() running first before the object's movement
            // script's Update(), which then causes NT to send every alternate frame
            // instead.
            if (isServer || (IsClientWithAuthority && NetworkClient.ready))
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


        double lastNetworkTime;
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

                    if (debugDraw)
                    {
                        // visual debugging
                        double delta = NetworkTime.time - lastNetworkTime;
                        lastNetworkTime = NetworkTime.time;

                        Debug.DrawLine(from.position, to.position, Color.white, 10f);
                        Debug.DrawLine(computed.position, computed.position + Vector3.up * (float)(delta * 100), Color.white, 10f);
                    }
                }
            }
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

                writer.WriteVector3(snapshot.position);
                writer.WriteQuaternion(snapshot.rotation);
                writer.WriteVector3(snapshot.scale);

                // set 'last'
                last = snapshot;
            }
            // unreliable delta: compress against last full reliable state
            else
            {
                int startPosition = writer.Position;

                writer.WriteVector3(snapshot.position);
                writer.WriteQuaternion(snapshot.rotation);
                writer.WriteVector3(snapshot.scale);
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
                position = reader.ReadVector3();
                rotation = reader.ReadQuaternion();
                scale = reader.ReadVector3();

                if (debugDraw) Debug.DrawLine(position.Value, position.Value + Vector3.up , Color.green, 10.0f);
            }
            // unreliable delta: decompress against last full reliable state
            else
            {
                position = reader.ReadVector3();
                rotation = reader.ReadQuaternion();
                scale = reader.ReadVector3();

                if (debugDraw) Debug.DrawLine(position.Value, position.Value + Vector3.up , Color.yellow, 10.0f);

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

            // add a small timeline offset to account for decoupled arrival of
            // NetworkTime and NetworkTransform snapshots.
            // needs to be sendInterval. half sendInterval doesn't solve it.
            // https://github.com/MirrorNetworking/Mirror/issues/3427
            // remove this after LocalWorldState.
            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp, position, rotation, scale);
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
            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp, position, rotation, scale);
        }
    }
}
