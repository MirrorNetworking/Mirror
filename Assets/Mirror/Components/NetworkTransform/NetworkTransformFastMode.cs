// NetworkTransform V3 based on NetworkTransformUnreliable, using Mirror's new
// FastMode quake style networking model. by mischa (2024-07)
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Fast Mode)")]
    public class NetworkTransformFastMode : NetworkTransformBase
    {
        uint sendIntervalCounter = 0;
        double lastSendIntervalTime = double.MinValue;

        [Header("Additional Settings")]
        // Testing under really bad network conditions, 2%-5% packet loss and 250-1200ms ping, 5 proved to eliminate any twitching, however this should not be the default as it is a rare case Developers may want to cover.
        [Tooltip("How much time, as a multiple of send interval, has passed before clearing buffers.\nA larger buffer means more delay, but results in smoother movement.\nExample: 1 for faster responses minimal smoothing, 5 covers bad pings but has noticable delay, 3 is recommended for balanced results,.")]
        public float bufferResetMultiplier = 3;

        // update //////////////////////////////////////////////////////////////
        // Update applies interpolation
        void Update()
        {
            if (isServer) UpdateServerInterpolation();
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer.
            // 'else if' because host mode shouldn't interpolate client
            else if (isClient && !IsClientWithAuthority) UpdateClientInterpolation();
        }

        // LateUpdate broadcasts.
        // movement scripts may change positions in Update.
        // use LateUpdate to ensure changes are detected in the same frame.
        // otherwise this may run before user update, delaying detection until next frame.
        // this could cause visible jitter.
        void LateUpdate()
        {
            // if server then always sync to others.
            if (isServer) UpdateServerBroadcast();
            // client authority, and local player (= allowed to move myself)?
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient && IsClientWithAuthority) UpdateClientBroadcast();
        }

        protected virtual void CheckLastSendTime()
        {
            // We check interval every frame, and then send if interval is reached.
            // So by the time sendIntervalCounter == sendIntervalMultiplier, data is sent,
            // thus we reset the counter here.
            // This fixes previous issue of, if sendIntervalMultiplier = 1, we send every frame,
            // because intervalCounter is always = 1 in the previous version.

            // Changing == to >= https://github.com/MirrorNetworking/Mirror/issues/3571

            if (sendIntervalCounter >= sendIntervalMultiplier)
                sendIntervalCounter = 0;

            // timeAsDouble not available in older Unity versions.
            if (AccurateInterval.Elapsed(NetworkTime.localTime, NetworkServer.sendInterval, ref lastSendIntervalTime))
                sendIntervalCounter++;
        }

        void UpdateServerBroadcast()
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
            CheckLastSendTime();

            if (sendIntervalCounter == sendIntervalMultiplier && // same interval as time interpolation!
                (syncDirection == SyncDirection.ServerToClient || IsClientWithAuthority))
            {
                // send snapshot without timestamp.
                // receiver gets it from batch timestamp to save bandwidth.
                TransformSnapshot snapshot = Construct();

                if (compressRotation)
                {
                    RpcServerToClientSyncCompressRotation(
                        // only sync what the user wants to sync
                        syncPosition ? snapshot.position : default(Vector3?),
                        syncRotation ? Compression.CompressQuaternion(snapshot.rotation) : default(uint?),
                        syncScale ? snapshot.scale : default(Vector3?)
                    );
                }
                else
                {
                    RpcServerToClientSync(
                    // only sync what the user wants to sync
                    syncPosition ? snapshot.position : default(Vector3?),
                    syncRotation ? snapshot.rotation : default(Quaternion?),
                    syncScale ? snapshot.scale : default(Vector3?)
                    );
                }
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
            // -> connectionToClient is briefly null after scene changes:
            //    https://github.com/MirrorNetworking/Mirror/issues/3329
            if (syncDirection == SyncDirection.ClientToServer &&
                connectionToClient != null &&
                !isOwned)
            {
                if (serverSnapshots.Count == 0) return;

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

        void UpdateClientBroadcast()
        {
            // https://github.com/vis2k/Mirror/pull/2992/
            if (!NetworkClient.ready) return;

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
            CheckLastSendTime();
            if (sendIntervalCounter == sendIntervalMultiplier) // same interval as time interpolation!
            {
                // send snapshot without timestamp.
                // receiver gets it from batch timestamp to save bandwidth.
                TransformSnapshot snapshot = Construct();

                if (compressRotation)
                {
                    CmdClientToServerSyncCompressRotation(
                        // only sync what the user wants to sync
                        syncPosition ? snapshot.position : default(Vector3?),
                        syncRotation ? Compression.CompressQuaternion(snapshot.rotation) : default(uint?),
                        syncScale ? snapshot.scale : default(Vector3?)
                    );
                }
                else
                {
                    CmdClientToServerSync(
                        // only sync what the user wants to sync
                        syncPosition ? snapshot.position : default(Vector3?),
                        syncRotation ? snapshot.rotation : default(Quaternion?),
                        syncScale ? snapshot.scale : default(Vector3?)
                    );
                }
            }
        }

        void UpdateClientInterpolation()
        {
            // only while we have snapshots
            if (clientSnapshots.Count == 0) return;

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

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                if (syncPosition) writer.WriteVector3(GetPosition());
                if (syncRotation) writer.WriteQuaternion(GetRotation());
                if (syncScale) writer.WriteVector3(GetScale());
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                if (syncPosition) SetPosition(reader.ReadVector3());
                if (syncRotation) SetRotation(reader.ReadQuaternion());
                if (syncScale) SetScale(reader.ReadVector3());
            }
        }

        // cmd /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            OnClientToServerSync(position, rotation, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (syncDirection == SyncDirection.ClientToServer)
                RpcServerToClientSync(position, rotation, scale);
        }

        // cmd /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSyncCompressRotation(Vector3? position, uint? rotation, Vector3? scale)
        {
            // A fix to not apply current interpolated GetRotation when receiving null/unchanged value, instead use last sent snapshot rotation.
            Quaternion newRotation;
            if (rotation.HasValue)
            {
                newRotation = Compression.DecompressQuaternion((uint)rotation);
            }
            else
            {
                newRotation = serverSnapshots.Count > 0 ? serverSnapshots.Values[serverSnapshots.Count - 1].rotation : GetRotation();
            }
            OnClientToServerSync(position, newRotation, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (syncDirection == SyncDirection.ClientToServer)
                RpcServerToClientSyncCompressRotation(position, rotation, scale);
        }

        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // only player owned objects (with a connection) can send to
            // server. we can get the timestamp from the connection.
            double timestamp = connectionToClient.remoteTimeStamp;

            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendIntervalMultiplier * NetworkClient.sendInterval;

                if (serverSnapshots.Count > 0 && serverSnapshots.Values[serverSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                    ResetState();
            }

            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

        // rpc /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale) =>
            OnServerToClientSync(position, rotation, scale);

        // rpc /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSyncCompressRotation(Vector3? position, uint? rotation, Vector3? scale)
        {
            // A fix to not apply current interpolated GetRotation when receiving null/unchanged value, instead use last sent snapshot rotation.
            Quaternion newRotation;
            if (rotation.HasValue)
            {
                newRotation = Compression.DecompressQuaternion((uint)rotation);
            }
            else
            {
                newRotation = clientSnapshots.Count > 0 ? clientSnapshots.Values[clientSnapshots.Count - 1].rotation : GetRotation();
            }
            OnServerToClientSync(position, newRotation, scale);
        }

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

            // on the client, we receive rpcs for all entities.
            // not all of them have a connectionToServer.
            // but all of them go through NetworkClient.connection.
            // we can get the timestamp from there.
            double timestamp = NetworkClient.connection.remoteTimeStamp;

            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendIntervalMultiplier * NetworkServer.sendInterval;

                if (clientSnapshots.Count > 0 && clientSnapshots.Values[clientSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                    ResetState();
            }

            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(SyncData syncData)
        {
            OnClientToServerSync(syncData);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (syncDirection == SyncDirection.ClientToServer)
                RpcServerToClientSync(syncData);
        }

        protected virtual void OnClientToServerSync(SyncData syncData)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // only player owned objects (with a connection) can send to
            // server. we can get the timestamp from the connection.
            double timestamp = connectionToClient.remoteTimeStamp;

            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendIntervalMultiplier * NetworkClient.sendInterval;

                if (serverSnapshots.Count > 0 && serverSnapshots.Values[serverSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                    ResetState();
            }

            UpdateSyncData(ref syncData, serverSnapshots);

            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp + timeStampAdjustment + offset, syncData.position, syncData.quatRotation, syncData.scale);
        }


        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(SyncData syncData) =>
            OnServerToClientSync(syncData);

        protected virtual void OnServerToClientSync(SyncData syncData)
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

            // on the client, we receive rpcs for all entities.
            // not all of them have a connectionToServer.
            // but all of them go through NetworkClient.connection.
            // we can get the timestamp from there.
            double timestamp = NetworkClient.connection.remoteTimeStamp;

            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendIntervalMultiplier * NetworkServer.sendInterval;

                if (clientSnapshots.Count > 0 && clientSnapshots.Values[clientSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                    ResetState();
            }

            UpdateSyncData(ref syncData, clientSnapshots);

            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + timeStampAdjustment + offset, syncData.position, syncData.quatRotation, syncData.scale);
        }

        protected virtual void UpdateSyncData(ref SyncData syncData, SortedList<double, TransformSnapshot> snapshots)
        {
            if (syncData.changedDataByte == Changed.None || syncData.changedDataByte == Changed.CompressRot)
            {
                syncData.position = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].position : GetPosition();
                syncData.quatRotation = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].rotation : GetRotation();
                syncData.scale = snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].scale : GetScale();
            }
            else
            {
                // Just going to update these without checking if syncposition or not,
                // because if not syncing position, NT will not apply any position data
                // to the target during Apply().

                syncData.position.x = (syncData.changedDataByte & Changed.PosX) > 0 ? syncData.position.x : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].position.x : GetPosition().x);
                syncData.position.y = (syncData.changedDataByte & Changed.PosY) > 0 ? syncData.position.y : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].position.y : GetPosition().y);
                syncData.position.z = (syncData.changedDataByte & Changed.PosZ) > 0 ? syncData.position.z : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].position.z : GetPosition().z);

                // If compressRot is true, we already have the Quat in syncdata.
                if ((syncData.changedDataByte & Changed.CompressRot) == 0)
                {
                    syncData.vecRotation.x = (syncData.changedDataByte & Changed.RotX) > 0 ? syncData.vecRotation.x : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].rotation.eulerAngles.x : GetRotation().eulerAngles.x);
                    syncData.vecRotation.y = (syncData.changedDataByte & Changed.RotY) > 0 ? syncData.vecRotation.y : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].rotation.eulerAngles.y : GetRotation().eulerAngles.y); ;
                    syncData.vecRotation.z = (syncData.changedDataByte & Changed.RotZ) > 0 ? syncData.vecRotation.z : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].rotation.eulerAngles.z : GetRotation().eulerAngles.z);

                    syncData.quatRotation = Quaternion.Euler(syncData.vecRotation);
                }
                else
                {
                    syncData.quatRotation = (syncData.changedDataByte & Changed.Rot) > 0 ? syncData.quatRotation : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].rotation : GetRotation());
                }

                syncData.scale = (syncData.changedDataByte & Changed.Scale) > 0 ? syncData.scale : (snapshots.Count > 0 ? snapshots.Values[snapshots.Count - 1].scale : GetScale());
            }
        }

        // This is to extract position/rotation/scale data from payload. Override
        // Construct and Deconstruct if you are implementing a different SyncData logic.
        // Note however that snapshot interpolation still requires the basic 3 data
        // position, rotation and scale, which are computed from here.
        protected virtual void DeconstructSyncData(System.ArraySegment<byte> receivedPayload, out byte? changedFlagData, out Vector3? position, out Quaternion? rotation, out Vector3? scale)
        {
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(receivedPayload))
            {
                SyncData syncData = reader.Read<SyncData>();
                changedFlagData = (byte)syncData.changedDataByte;
                position = syncData.position;
                rotation = syncData.quatRotation;
                scale = syncData.scale;
            }
        }
    }
}
