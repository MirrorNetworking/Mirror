// NetworkTransform V2 by mischa (2021-07)
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Unreliable)")]
    public class NetworkTransformUnreliable : NetworkTransformBase
    {
        [Header("Bandwidth Savings")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;
        [Tooltip("Apply smallest-three quaternion compression. This is lossy, you can disable it if the small rotation inaccuracies are noticeable in your project.")]
        public bool compressRotation = true;

        uint sendIntervalCounter = 0;
        double lastSendIntervalTime = double.MinValue;

        // Testing under really bad network conditions, 2%-5% packet loss and 250-1200ms ping, 5 proved to eliminate any twitching, however this should not be the default as it is a rare case Developers may want to cover.
        [Tooltip("How much time, as a multiple of send interval, has passed before clearing buffers.\nA larger buffer means more delay, but results in smoother movement.\nExample: 1 for faster responses minimal smoothing, 5 covers bad pings but has noticable delay, 3 is recommended for balanced results,.")]
        public float bufferResetMultiplier = 3;

        [Header("Sensitivity"), Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float positionSensitivity = 0.01f;
        public float rotationSensitivity = 0.01f;
        public float scaleSensitivity = 0.01f;

        protected bool positionChanged;
        protected bool rotationChanged;
        protected bool scaleChanged;

        // Used to store last sent snapshots
        protected TransformSnapshot lastSnapshot;
        protected bool cachedSnapshotComparison;
        protected bool hasSentUnchangedPosition;

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

            if (sendIntervalCounter == sendIntervalMultiplier)
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
                cachedSnapshotComparison = CompareSnapshots(snapshot);
                if (cachedSnapshotComparison && hasSentUnchangedPosition && onlySyncOnChange) { return; }

                if (compressRotation)
                {
                    RpcServerToClientSyncCompressRotation(
                        // only sync what the user wants to sync
                        syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                        syncRotation && rotationChanged ? Compression.CompressQuaternion(snapshot.rotation) : default(uint?),
                        syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                    );
                }
                else
                {
                    RpcServerToClientSync(
                    // only sync what the user wants to sync
                    syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                    syncRotation && rotationChanged ? snapshot.rotation : default(Quaternion?),
                    syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                    );
                }

                if (cachedSnapshotComparison)
                {
                    hasSentUnchangedPosition = true;
                }
                else
                {
                    hasSentUnchangedPosition = false;
                    lastSnapshot = snapshot;
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
                cachedSnapshotComparison = CompareSnapshots(snapshot);
                if (cachedSnapshotComparison && hasSentUnchangedPosition && onlySyncOnChange) { return; }

                if (compressRotation)
                {
                    CmdClientToServerSyncCompressRotation(
                        // only sync what the user wants to sync
                        syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                        syncRotation && rotationChanged ? Compression.CompressQuaternion(snapshot.rotation) : default(uint?),
                        syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                    );
                }
                else
                {
                    CmdClientToServerSync(
                        // only sync what the user wants to sync
                        syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                        syncRotation && rotationChanged ? snapshot.rotation : default(Quaternion?),
                        syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                    );
                }

                if (cachedSnapshotComparison)
                {
                    hasSentUnchangedPosition = true;
                }
                else
                {
                    hasSentUnchangedPosition = false;
                    lastSnapshot = snapshot;
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

        // Returns true if position, rotation AND scale are unchanged, within given sensitivity range.
        protected virtual bool CompareSnapshots(TransformSnapshot currentSnapshot)
        {
            positionChanged = Vector3.SqrMagnitude(lastSnapshot.position - currentSnapshot.position) > positionSensitivity * positionSensitivity;
            rotationChanged = Quaternion.Angle(lastSnapshot.rotation, currentSnapshot.rotation) > rotationSensitivity;
            scaleChanged = Vector3.SqrMagnitude(lastSnapshot.scale - currentSnapshot.scale) > scaleSensitivity * scaleSensitivity;

            return (!positionChanged && !rotationChanged && !scaleChanged);
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
            OnClientToServerSync(position, rotation.HasValue ? Compression.DecompressQuaternion((uint)rotation) : GetRotation(), scale);
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
                    Reset();
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
        void RpcServerToClientSyncCompressRotation(Vector3? position, uint? rotation, Vector3? scale) =>
            OnServerToClientSync(position, rotation.HasValue ? Compression.DecompressQuaternion((uint)rotation) : GetRotation(), scale);

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
                    Reset();
            }

            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }
    }
}
