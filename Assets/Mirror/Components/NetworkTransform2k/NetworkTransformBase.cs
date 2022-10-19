// NetworkTransform V3 by mischa (2022-10-17)
// Snapshot Interpolation: https://gafferongames.com/post/snapshot_interpolation/
//
// Base class for NetworkTransform and NetworkTransformChild.
// => uses SyncDirection + reliable
// => this time with better bandwidth
//
// NOTE: several functions are virtual in case someone needs to modify a part.
using System;
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

        internal SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>();
        internal SortedList<double, TransformSnapshot> serverSnapshots = new SortedList<double, TransformSnapshot>();

        // selective sync //////////////////////////////////////////////////////
        [Header("Selective Sync & interpolation")]
        public bool syncPosition = true;
        public bool syncRotation = true;
        public bool syncScale    = false; // rare. off by default.

        // round position to int for easier compression
        //
        // for reference, Mirror II delta compression with position precision:
        //   benchmark with 0.01 precision: 130 KB/s => 60 KB/s
        //   benchmark with 0.1  precision: 130 KB/s => 30 KB/s
        [Header("Precision")]
        [Tooltip("Position is rounded in order to drastically minimize bandwidth.\n\nFor example, a precision of 0.01 rounds to a centimeter. In other words, sub-centimeter movements aren't synced until they eventually exceeded an actual centimeter.\n\nDepending on how important the object is, a precision of 0.01-0.10 (1-10 cm) is recommended.\n\nFor example, even a 1cm precision combined with delta compression cuts the Benchmark demo's bandwidth in half, compared to sending every tiny change.")]
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float positionPrecision = 0.01f; // 1 cm

        [Tooltip("Scale is rounded in order to drastically minimize bandwidth.\n\nFor example, a precision of 0.01 rounds to a centimeter. In other words, sub-centimeter movements aren't synced until they eventually exceeded an actual centimeter.\n\nDepending on how important the object is, a precision of 0.01-0.10 (1-10 cm) is recommended.\n\nFor example, even a 1cm precision combined with delta compression cuts the Benchmark demo's bandwidth in half, compared to sending every tiny change.")]
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float scalePrecision    = 0.01f; // 1 cm

        // delta compression requires one writer for current, one for last.
        // keep and swap them to avoid extra memcpy to last.
        NetworkWriter last    = new NetworkWriter();
        NetworkWriter current = new NetworkWriter();

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public bool showGizmos;
        public bool  showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // initialization //////////////////////////////////////////////////////
        // make sure to call this when inheriting too!
        protected virtual void Awake() {}

        public override void OnStartServer()
        {
            // build 'last' serialization once,
            // so OnSerialize can delta against it.
            last.Position = 0;
            SerializeEverything(last);
        }

        // snapshot functions //////////////////////////////////////////////////
        // construct a snapshot of the current state
        // => internal for testing
        protected virtual TransformSnapshot ConstructSnapshot()
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
        protected virtual void ApplySnapshot(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            // local position/rotation for VR support
            //
            // if syncPosition/Rotation/Scale is disabled then we received nulls
            // -> current position/rotation/scale would've been added as snapshot
            // -> we still interpolated
            // -> but simply don't apply it. if the user doesn't want to sync
            //    scale, then we should not touch scale etc.
            if (syncPosition) targetComponent.localPosition = localPosition;
            if (syncRotation) targetComponent.localRotation = localRotation;
            if (syncScale)    targetComponent.localScale    = localScale;
        }

        // update //////////////////////////////////////////////////////////////
        void UpdateServer()
        {
            // set as always dirty to trigger OnSerialize every syncInterval.
            // TODO need SetDirty() for custom OnSerialize
            SetSyncVarDirtyBit(1);

            // TODO interpolate for host?
        }

        void UpdateClient()
        {
            // client authority, and local player (= allowed to move myself)?
            if (authority)
            {
                // https://github.com/vis2k/Mirror/pull/2992/
                if (!NetworkClient.ready) return;

                // set as always dirty to trigger OnSerialize every syncInterval.
                // TODO need SetDirty() for custom OnSerialize
                SetSyncVarDirtyBit(1);

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
                    ApplySnapshot(computed.position, computed.rotation, computed.scale);
                }
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
            // client can only teleport objects that it has authority over.
            if (!authority) return;

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
            // client can only teleport objects that it has authority over.
            if (!authority) return;

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

        // serialization ///////////////////////////////////////////////////////
        // overwrite this to validate movement with ClientToServer sync.
        // this may check velocity, physics, navmesh, etc.
        // note those are .localPosition etc.
        // make sure to consider selective sync settings like syncPosition.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool Validate(Vector3 localPosition, Quaternion localRotation, Vector3 localScale) =>
            true;

        // serialize into writer.
        // it's then either sent fully (initial) or delta compressed.
        void SerializeEverything(NetworkWriter writer)
        {
            // position, quantized to longs
            Compression.ScaleToLong(targetComponent.localPosition, positionPrecision, out long pX, out long pY, out long pZ); // local for VR
            writer.WriteLong(pX);
            writer.WriteLong(pY);
            writer.WriteLong(pZ);

            // rotation:
            // TODO quantization later.
            // TODO ensure it's exactly the same if not rotated.
            writer.WriteQuaternion(targetComponent.localRotation);

            // scale, quantized to longs
            Compression.ScaleToLong(targetComponent.localScale, scalePrecision, out long sX, out long sY, out long sZ); // local for VR
            writer.WriteLong(sX);
            writer.WriteLong(sY);
            writer.WriteLong(sZ);
        }

        // deserialize full data after delta compression
        void DeserializeEverything(NetworkReader reader, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            // position
            long pX = reader.ReadLong();
            long pY = reader.ReadLong();
            long pZ = reader.ReadLong();
            position = Compression.ScaleToFloat(pX, pY, pZ, positionPrecision);

            // rotation
            rotation = reader.ReadQuaternion();

            // scale
            long sX = reader.ReadLong();
            long sY = reader.ReadLong();
            long sZ = reader.ReadLong();
            scale = Compression.ScaleToFloat(sX, sY, sZ, scalePrecision);
        }

        // last serialization for delta compression
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // TODO ClientToServer support.
            // - initial is still only called on server
            // - not initial could just send directly
            if (syncDirection == SyncDirection.ClientToServer)
                throw new NotImplementedException("ClientToServer isn't implemented yet");

            // 'last' is expected to be initialized by the time we get here.
            if (last.Position == 0)
                throw new Exception("'last' has not been initialized.");

            // for new observers, OnSerialize is called with initialState = true.
            // send them the current state of the object, fully.
            // they will save the state and then delta compress against it next time.
            if (initialState)
            {
                // on server, new observers should simply receive 'last' so that
                // later when they receive 'current', they'll know what to delta against.
                // TODO is this right?

                if (last.Position == 0)
                    throw new Exception("'last' has not been initialized.");

                ArraySegment<byte> segment = last.ToArraySegment();
                writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
            }
            // use bit tree delta compression against last if initialState = false.
            else
            {
                // apply delta compression to minimize bandwidth.
                // need to make sure to always write the same amount of data.
                // ideally, multiples of 8 so that the mask bytes are fully used.
                //
                //   3 x long  for position: 24 bytes
                //   4 x float for rotation: 16 bytes
                //   3 x long  for scale:    24 bytes
                //   => total: 64 bytes
                //
                // if unchanged, BitTree will reduce 64 => 8 => 1 byte.

                // serialize into 'current' writer, delta, save as 'last'.
                current.Position = 0;
                SerializeEverything(current);
                BitTree.Compress(last, current, writer);
                (last, current) = (current, last);
            }
        }

        // when syncing from server to client, insert for snapshot interp.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DeserializeClient(Vector3 position, Quaternion rotation, Vector3 scale, bool initialState)
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

            // on the client, we receive rpcs for all entities.
            // not all of them have a connectionToServer.
            // but all of them go through NetworkClient.connection.
            // we can get the timestamp from there.

            // insert snapshot.
            if (!authority)
            {
                SnapshotInterpolation.InsertIfNotExists(clientSnapshots, new TransformSnapshot(
                    NetworkClient.connection.remoteTimeStamp, // arrival remote timestamp. NOT remote time.
#if !UNITY_2020_3_OR_NEWER
                    NetworkTime.localTime,                    // Unity 2019 doesn't have timeAsDouble yet
#else
                    Time.timeAsDouble,
#endif
                    position,
                    rotation,
                    scale
                ));
            }

            // just spawned with the first snapshot?
            // then apply it immediately.
            // otherwise the object would stay at origin for 1 frame.
            // which is noticeable.
            if (initialState) ApplySnapshot(position, rotation, scale);
        }

        // when syncing from client to server, validate and apply directly.
        // snapshot interpolation is only ever applied on the client for
        // smooth movement.
        // the server needs immediate results to not have it lag behind.
        // besides, nobody is watching the movement on the server :)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DeserializeServer(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // TODO interpolation for host mode?
            if (Validate(position, rotation, scale))
                ApplySnapshot(position, rotation, scale);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // make sure to sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)

            // TODO client to server direction support
            if (syncDirection == SyncDirection.ClientToServer)
                throw new NotImplementedException("ClientToServer isn't implemented yet");

            // on first spawn, we get the full oncompressed data.
            if (initialState)
            {
                // save it as 'last' to delta decompress against next time
                // TODO make sure host mode doesn't overwrite server's last
                int start = reader.Position;
                DeserializeEverything(reader, out Vector3 position, out Quaternion rotation, out Vector3 scale);
                int size = reader.Position - start;

                // Debug.LogWarning($"{name}: Deserialize initial {size} bytes: {position}/{rotation}/{scale}");

                DeserializeClient(position, rotation, scale, initialState);

                // get the ArraySegment that was read.
                // reader may have more data, but we don't want to save that.
                int backup = reader.Position;
                reader.Position = start;
                ArraySegment<byte> segment = reader.ReadBytesSegment(size);
                reader.Position = backup; // restore

                // save as 'last' to delta compress against
                // make sure we don't overwrite server's 'last' in host mode!
                // TODO clientToServer handling. there it's fine
                if (isServer) throw new Exception("don't wanna overwrite last on server");
                last.Position = 0;
                last.WriteBytes(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                // delta decompress against 'last', store new 'last'
                current.Position = 0;
                BitTree.Decompress(last.ToArraySegment(), reader, current);

                // parse delta compressed data
                using (NetworkReaderPooled decompressed = NetworkReaderPool.Get(current.ToArraySegment()))
                {
                    DeserializeEverything(decompressed, out Vector3 position, out Quaternion rotation, out Vector3 scale);

                    // store 'current' as new 'last' to decompress against next time
                    DeserializeClient(position, rotation, scale, initialState);
                    (last, current) = (current, last);
                }
            }
        }

        // other ///////////////////////////////////////////////////////////////
        public virtual void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverSnapshots.Clear();
            clientSnapshots.Clear();
            last.Position    = 0;
            current.Position = 0;
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

                // always show both client & server buffers so it's super
                // obvious if we accidentally populate both.
                if (serverSnapshots.Count > 0)
                    GUILayout.Label($"Server Buffer:{serverSnapshots.Count}");

                if (clientSnapshots.Count > 0)
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
            Gizmos.DrawLine(buffer.Values[0].position, targetComponent.position);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(targetComponent.position, buffer.Values[1].position);
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
