// NetworkTransform V2 aka project Oumuamua by vis2k (2021-07)
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
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        // TODO SyncDirection { CLIENT_TO_SERVER, SERVER_TO_CLIENT } is easier?
        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        protected bool IsClientWithAuthority => hasAuthority && clientAuthority;

        // target transform to sync. can be on a child.
        protected abstract Transform targetComponent { get; }

        [Header("Synchronization")]
        [Range(0, 1)] public float sendInterval = 0.050f;
        public bool syncPosition = true;
        public bool syncRotation = true;
        // scale sync is rare. off by default.
        public bool syncScale = false;

        double lastClientSendTime;
        double lastServerSendTime;

        // not all games need to interpolate. a board game might jump to the
        // final position immediately.
        [Header("Interpolation")]
        public bool interpolatePosition = true;
        public bool interpolateRotation = true;
        public bool interpolateScale = false;

        // "Experimentally I’ve found that the amount of delay that works best
        //  at 2-5% packet loss is 3X the packet send rate"
        // NOTE: we do NOT use a dyanmically changing buffer size.
        //       it would come with a lot of complications, e.g. buffer time
        //       advantages/disadvantages for different connections.
        //       Glenn Fiedler's recommendation seems solid, and should cover
        //       the vast majority of connections.
        //       (a player with 2000ms latency will have issues no matter what)
        [Header("Buffering")]
        [Tooltip("Snapshots are buffered for sendInterval * multiplier seconds. If your expected client base is to run at non-ideal connection quality (2-5% packet loss), 3x supposedly works best.")]
        public int bufferTimeMultiplier = 1;
        public float bufferTime => sendInterval * bufferTimeMultiplier;
        [Tooltip("Buffer size limit to avoid ever growing list memory consumption attacks.")]
        public int bufferSizeLimit = 64;

        [Tooltip("Start to accelerate interpolation if buffer size is >= threshold. Needs to be larger than bufferTimeMultiplier.")]
        public int catchupThreshold = 4;

        [Tooltip("Once buffer is larger catchupThreshold, accelerate by multiplier % per excess entry.")]
        [Range(0, 1)] public float catchupMultiplier = 0.10f;

        // snapshots sorted by timestamp
        // in the original article, glenn fiedler drops any snapshots older than
        // the last received snapshot.
        // -> instead, we insert into a sorted buffer
        // -> the higher the buffer information density, the better
        // -> we still drop anything older than the first element in the buffer
        // => internal for testing
        //
        // IMPORTANT: of explicit 'NTSnapshot' type instead of 'Snapshot'
        //            interface because List<interface> allocates through boxing
        internal SortedList<double, NTSnapshot> serverBuffer = new SortedList<double, NTSnapshot>();
        internal SortedList<double, NTSnapshot> clientBuffer = new SortedList<double, NTSnapshot>();

        // absolute interpolation time, moved along with deltaTime
        // (roughly between [0, delta] where delta is snapshot B - A timestamp)
        // (can be bigger than delta when overshooting)
        double serverInterpolationTime;
        double clientInterpolationTime;

        // only convert the static Interpolation function to Func<T> once to
        // avoid allocations
        Func<NTSnapshot, NTSnapshot, double, NTSnapshot> Interpolate = NTSnapshot.Interpolate;

        [Header("Debug")]
        public bool showGizmos;
        public bool showOverlay;
        public Color overlayColor = new Color(0, 0, 0, 0.5f);

        // snapshot functions //////////////////////////////////////////////////
        // construct a snapshot of the current state
        // => internal for testing
        protected virtual NTSnapshot ConstructSnapshot()
        {
            // NetworkTime.localTime for double precision until Unity has it too
            return new NTSnapshot(
                // our local time is what the other end uses as remote time
                NetworkTime.localTime,
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
        protected virtual void ApplySnapshot(NTSnapshot start, NTSnapshot goal, NTSnapshot interpolated)
        {
            // local position/rotation for VR support
            //
            // if syncPosition/Rotation/Scale is disabled then we received nulls
            // -> current position/rotation/scale would've been added as snapshot
            // -> we still interpolated
            // -> but simply don't apply it. if the user doesn't want to sync
            //    scale, then we should not touch scale etc.
            if (syncPosition)
                targetComponent.localPosition = interpolatePosition ? interpolated.position : goal.position;

            if (syncRotation)
                targetComponent.localRotation = interpolateRotation ? interpolated.rotation : goal.rotation;

            if (syncScale)
                targetComponent.localScale = interpolateScale ? interpolated.scale : goal.scale;
        }

        // cmd /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            OnClientToServerSync(position, rotation, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (clientAuthority)
            {
                RpcServerToClientSync(position, rotation, scale);
            }
        }

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (!clientAuthority) return;

            // protect against ever growing buffer size attacks
            if (serverBuffer.Count >= bufferSizeLimit) return;

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
            if (!position.HasValue) position = targetComponent.localPosition;
            if (!rotation.HasValue) rotation = targetComponent.localRotation;
            if (!scale.HasValue) scale = targetComponent.localScale;

            // construct snapshot with batch timestamp to save bandwidth
            NTSnapshot snapshot = new NTSnapshot(
                timestamp,
                NetworkTime.localTime,
                position.Value, rotation.Value, scale.Value
            );

            // add to buffer (or drop if older than first element)
            SnapshotInterpolation.InsertIfNewEnough(snapshot, serverBuffer);
        }

        // rpc /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale) =>
            OnServerToClientSync(position, rotation, scale);

        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
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

            // protect against ever growing buffer size attacks
            if (clientBuffer.Count >= bufferSizeLimit) return;

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
            if (!position.HasValue) position = targetComponent.localPosition;
            if (!rotation.HasValue) rotation = targetComponent.localRotation;
            if (!scale.HasValue) scale = targetComponent.localScale;

            // construct snapshot with batch timestamp to save bandwidth
            NTSnapshot snapshot = new NTSnapshot(
                timestamp,
                NetworkTime.localTime,
                position.Value, rotation.Value, scale.Value
            );

            // add to buffer (or drop if older than first element)
            SnapshotInterpolation.InsertIfNewEnough(snapshot, clientBuffer);
        }

        // update //////////////////////////////////////////////////////////////
        void UpdateServer()
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
            if (NetworkTime.localTime >= lastServerSendTime + sendInterval &&
                (!clientAuthority || IsClientWithAuthority))
            {
                // send snapshot without timestamp.
                // receiver gets it from batch timestamp to save bandwidth.
                NTSnapshot snapshot = ConstructSnapshot();
                RpcServerToClientSync(
                    // only sync what the user wants to sync
                    syncPosition ? snapshot.position : new Vector3?(),
                    syncRotation? snapshot.rotation : new Quaternion?(),
                    syncScale ? snapshot.scale : new Vector3?()
                );

                lastServerSendTime = NetworkTime.localTime;
            }

            // apply buffered snapshots IF client authority
            // -> in server authority, server moves the object
            //    so no need to apply any snapshots there.
            // -> don't apply for host mode player objects either, even if in
            //    client authority mode. if it doesn't go over the network,
            //    then we don't need to do anything.
            if (clientAuthority && !hasAuthority)
            {
                // compute snapshot interpolation & apply if any was spit out
                // TODO we don't have Time.deltaTime double yet. float is fine.
                if (SnapshotInterpolation.Compute(
                    NetworkTime.localTime, Time.deltaTime,
                    ref serverInterpolationTime,
                    bufferTime, serverBuffer,
                    catchupThreshold, catchupMultiplier,
                    Interpolate,
                    out NTSnapshot computed))
                {
                    NTSnapshot start = serverBuffer.Values[0];
                    NTSnapshot goal = serverBuffer.Values[1];
                    ApplySnapshot(start, goal, computed);
                }
            }
        }

        void UpdateClient()
        {
            // client authority, and local player (= allowed to move myself)?
            if (IsClientWithAuthority)
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
                if (NetworkTime.localTime >= lastClientSendTime + sendInterval)
                {
                    // send snapshot without timestamp.
                    // receiver gets it from batch timestamp to save bandwidth.
                    NTSnapshot snapshot = ConstructSnapshot();
                    CmdClientToServerSync(
                        // only sync what the user wants to sync
                        syncPosition ? snapshot.position : new Vector3?(),
                        syncRotation? snapshot.rotation : new Quaternion?(),
                        syncScale ? snapshot.scale : new Vector3?()
                    );

                    lastClientSendTime = NetworkTime.localTime;
                }
            }
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer
            else
            {
                // compute snapshot interpolation & apply if any was spit out
                // TODO we don't have Time.deltaTime double yet. float is fine.
                if (SnapshotInterpolation.Compute(
                    NetworkTime.localTime, Time.deltaTime,
                    ref clientInterpolationTime,
                    bufferTime, clientBuffer,
                    catchupThreshold, catchupMultiplier,
                    Interpolate,
                    out NTSnapshot computed))
                {
                    NTSnapshot start = clientBuffer.Values[0];
                    NTSnapshot goal = clientBuffer.Values[1];
                    ApplySnapshot(start, goal, computed);
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
            // -> maybe add destionation as first entry?
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

        // client->server teleport to force position without interpolation.
        // otherwise it would interpolate to a (far away) new position.
        // => manually calling Teleport is the only 100% reliable solution.
        [Command]
        public void CmdTeleport(Vector3 destination)
        {
            // client can only teleport objects that it has authority over.
            if (!clientAuthority) return;

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

        protected virtual void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverBuffer.Clear();
            clientBuffer.Clear();

            // reset interpolation time too so we start at t=0 next time
            serverInterpolationTime = 0;
            clientInterpolationTime = 0;
        }

        protected virtual void OnDisable() => Reset();
        protected virtual void OnEnable() => Reset();

        protected virtual void OnValidate()
        {
            // make sure that catchup threshold is > buffer multiplier.
            // for a buffer multiplier of '3', we usually have at _least_ 3
            // buffered snapshots. often 4-5 even.
            //
            // catchUpThreshold should be a minimum of bufferTimeMultiplier + 3,
            // to prevent clashes with SnapshotInterpolation looking for at least
            // 3 old enough buffers, else catch up will be implemented while there
            // is not enough old buffers, and will result in jitter.
            // (validated with several real world tests by ninja & imer)
            catchupThreshold = Mathf.Max(bufferTimeMultiplier + 3, catchupThreshold);

            // buffer limit should be at least multiplier to have enough in there
            bufferSizeLimit = Mathf.Max(bufferTimeMultiplier, bufferSizeLimit);
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
                // catchup is useful to show too
                int serverBufferExcess = Mathf.Max(serverBuffer.Count - catchupThreshold, 0);
                int clientBufferExcess = Mathf.Max(clientBuffer.Count - catchupThreshold, 0);
                float serverCatchup = serverBufferExcess * catchupMultiplier;
                float clientCatchup = clientBufferExcess * catchupMultiplier;

                GUI.color = overlayColor;
                GUILayout.BeginArea(new Rect(point.x, Screen.height - point.y, 200, 100));

                // always show both client & server buffers so it's super
                // obvious if we accidentally populate both.
                GUILayout.Label($"Server Buffer:{serverBuffer.Count}");
                if (serverCatchup > 0)
                    GUILayout.Label($"Server Catchup:{serverCatchup*100:F2}%");

                GUILayout.Label($"Client Buffer:{clientBuffer.Count}");
                if (clientCatchup > 0)
                    GUILayout.Label($"Client Catchup:{clientCatchup*100:F2}%");

                GUILayout.EndArea();
                GUI.color = Color.white;
            }
        }

        protected virtual void DrawGizmos(SortedList<double, NTSnapshot> buffer)
        {
            // only draw if we have at least two entries
            if (buffer.Count < 2) return;

            // calcluate threshold for 'old enough' snapshots
            double threshold = NetworkTime.localTime - bufferTime;
            Color oldEnoughColor = new Color(0, 1, 0, 0.5f);
            Color notOldEnoughColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            // draw the whole buffer for easier debugging.
            // it's worth seeing how much we have buffered ahead already
            for (int i = 0; i < buffer.Count; ++i)
            {
                // color depends on if old enough or not
                NTSnapshot entry = buffer.Values[i];
                bool oldEnough = entry.localTimestamp <= threshold;
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

            if (isServer) DrawGizmos(serverBuffer);
            if (isClient) DrawGizmos(clientBuffer);
        }
#endif
    }
}
