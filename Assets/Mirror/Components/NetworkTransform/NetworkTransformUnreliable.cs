// NetworkTransform V2 by mischa (2021-07)
// comment out the below line to quickly revert the onlySyncOnChange feature
#define onlySyncOnChange_BANDWIDTH_SAVING
using System;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Transform (Unreliable)")]
    public class NetworkTransformUnreliable : NetworkTransformBase
    {
        // only sync when changed hack /////////////////////////////////////////
#if onlySyncOnChange_BANDWIDTH_SAVING
        [Header("Sync Only If Changed")]
        [Tooltip("When true, changes are not sent unless greater than sensitivity values below.")]
        public bool onlySyncOnChange = true;

        uint sendIntervalCounter = 0;
        double lastSendIntervalTime = double.MinValue;

        // Testing under really bad network conditions, 2%-5% packet loss and 250-1200ms ping, 5 proved to eliminate any twitching, however this should not be the default as it is a rare case Developers may want to cover.
        [Tooltip("How much time, as a multiple of send interval, has passed before clearing buffers.\nA larger buffer means more delay, but results in smoother movement.\nExample: 1 for faster responses minimal smoothing, 5 covers bad pings but has noticable delay, 3 is recommended for balanced results,.")]
        public float bufferResetMultiplier = 3;

        [Header("Sensitivity"), Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float positionSensitivity = 0.01f;
        public float rotationSensitivity = 0.01f;
        public float scaleSensitivity = 0.01f;

        [Tooltip("Apply smallest-three quaternion compression. This is lossy, you can disable it if the small rotation inaccuracies are noticeable in your project.")]
        public bool compressRotation = true;

        protected bool positionChanged;
        protected bool rotationChanged;
        protected bool scaleChanged;

        // Used to store last sent snapshots
        protected TransformSnapshot lastSnapshot;
        protected bool cachedSnapshotComparison;
        protected bool hasSentUnchangedPosition;
#endif

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
#if onlySyncOnChange_BANDWIDTH_SAVING
                cachedSnapshotComparison = CompareSnapshots(snapshot);
                if (cachedSnapshotComparison && hasSentUnchangedPosition && onlySyncOnChange) { return; }
#endif

#if onlySyncOnChange_BANDWIDTH_SAVING
                ConstructSyncData(true);
                //RpcServerToClientSync(
                //    // only sync what the user wants to sync
                //    syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                //    syncRotation && rotationChanged ? snapshot.rotation : default(Quaternion?),
                //    syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                //);
#else
                RpcServerToClientSync(
                    // only sync what the user wants to sync
                    syncPosition ? snapshot.position : default(Vector3?),
                    syncRotation ? snapshot.rotation : default(Quaternion?),
                    syncScale ? snapshot.scale : default(Vector3?)
                );
#endif

#if onlySyncOnChange_BANDWIDTH_SAVING
                if (cachedSnapshotComparison)
                {
                    hasSentUnchangedPosition = true;
                }
                else
                {
                    hasSentUnchangedPosition = false;
                    lastSnapshot = snapshot;
                }
#endif
            }
        }

        protected virtual void SerializeAndSend<T>(T syncData, bool fromServer)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.Write<T>(syncData);
                if (fromServer)
                {
                    RpcServerToClientSync(writer.ToArraySegment());
                }
                else
                {
                    CmdClientToServerSync(writer.ToArraySegment());
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
#if onlySyncOnChange_BANDWIDTH_SAVING
                cachedSnapshotComparison = CompareSnapshots(snapshot);
                if (cachedSnapshotComparison && hasSentUnchangedPosition && onlySyncOnChange) { return; }
#endif

#if onlySyncOnChange_BANDWIDTH_SAVING
                ConstructSyncData(false);
                //CmdClientToServerSync(
                //    // only sync what the user wants to sync
                //    syncPosition && positionChanged ? snapshot.position : default(Vector3?),
                //    syncRotation && rotationChanged ? snapshot.rotation : default(Quaternion?),
                //    syncScale && scaleChanged ? snapshot.scale : default(Vector3?)
                //);
#else
                CmdClientToServerSync(
                    // only sync what the user wants to sync
                    syncPosition ? snapshot.position : default(Vector3?),
                    syncRotation ? snapshot.rotation : default(Quaternion?),
                    syncScale    ? snapshot.scale    : default(Vector3?)
                );
#endif

#if onlySyncOnChange_BANDWIDTH_SAVING
                if (cachedSnapshotComparison)
                {
                    hasSentUnchangedPosition = true;
                }
                else
                {
                    hasSentUnchangedPosition = false;
                    lastSnapshot = snapshot;
                }
#endif
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
                if (syncScale)    writer.WriteVector3(GetScale());
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
                if (syncScale)       SetScale(reader.ReadVector3());
            }
        }

#if onlySyncOnChange_BANDWIDTH_SAVING
        // Returns true if position, rotation AND scale are unchanged, within given sensitivity range.
        protected virtual bool CompareSnapshots(TransformSnapshot currentSnapshot)
        {
            positionChanged = Vector3.SqrMagnitude(lastSnapshot.position - currentSnapshot.position) > positionSensitivity * positionSensitivity;
            rotationChanged = Quaternion.Angle(lastSnapshot.rotation, currentSnapshot.rotation) > rotationSensitivity;
            scaleChanged = Vector3.SqrMagnitude(lastSnapshot.scale - currentSnapshot.scale) > scaleSensitivity * scaleSensitivity;

            return (!positionChanged && !rotationChanged && !scaleChanged);
        }
#endif
        // cmd /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        //[Command(channel = Channels.Unreliable)]
        //void CmdClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        //{
            //OnClientToServerSync(position, rotation, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            //if (syncDirection == SyncDirection.ClientToServer)
            //    RpcServerToClientSync(position, rotation, scale);
       // }

        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(ArraySegment<byte> payload)
        {
            OnClientToServerSync(payload);
            //OnClientToServerSync(position, rotation, scale);
            //For client authority, immediately pass on the client snapshot to all other
            //clients instead of waiting for server to send its snapshots.
            if (syncDirection == SyncDirection.ClientToServer)
                RpcServerToClientSync(payload);
        }

        // temporary kept as Tests rely on it
        protected virtual void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
        }
        // local authority client sends sync message to server for broadcasting
        protected virtual void OnClientToServerSync(ArraySegment<byte> receivedPayload)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;

            // only player owned objects (with a connection) can send to
            // server. we can get the timestamp from the connection.
            double timestamp = connectionToClient.remoteTimeStamp;
#if onlySyncOnChange_BANDWIDTH_SAVING
            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendIntervalMultiplier * NetworkClient.sendInterval;

                if (serverSnapshots.Count > 0 && serverSnapshots.Values[serverSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                    Reset();
            }
#endif
            DeconstructSyncData(receivedPayload, out Vector3? position, out Quaternion? rotation, out Vector3? scale);
            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.
            if (!position.HasValue) position = target.localPosition;
            if (!rotation.HasValue) rotation = target.localRotation;
            if (!scale.HasValue) scale = target.localScale;

            AddSnapshot(serverSnapshots, connectionToClient.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

        // Create data to send. This can be overriden and changing SyncData in 
        // case there is a different implementation like compression, etc. 
        // REMEMBER: SerializeAndSend() must be called within the override.
        protected virtual void ConstructSyncData(bool fromServer)
        {
            if (compressRotation)
            {
                SyncDataCompressed syncData = new SyncDataCompressed(
                    syncPosition ? target.localPosition : new Vector3?(),
                    syncRotation ? Compression.CompressQuaternion(target.localRotation) : new uint?(),
                    syncScale ? target.localScale : new Vector3?()
                );
                SerializeAndSend<SyncDataCompressed>(syncData, fromServer);
            }
            else
            {
                SyncData syncData = new SyncData(
                    syncPosition ? target.localPosition : new Vector3?(),
                    syncRotation ? target.localRotation : new Quaternion?(),
                    syncScale ? target.localScale : new Vector3?()
                );
                SerializeAndSend<SyncData>(syncData, fromServer);
            }
        }

        // This is to extract position/rotation/scale data from payload. Override
        // Construct and Deconstruct if you are implementing a different SyncData logic.
        // Note however that snapshot interpolation still requires the basic 3 data
        // position, rotation and scale, which are computed from here.   
        protected virtual void DeconstructSyncData(ArraySegment<byte> receivedPayload, out Vector3? position, out Quaternion? rotation, out Vector3? scale)
        {
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(receivedPayload))
            {
                if (compressRotation)
                {
                    SyncDataCompressed syncData = reader.Read<SyncDataCompressed>();
                    position = syncData.position;
                    rotation = Compression.DecompressQuaternion((uint)syncData.rotation);
                    scale = syncData.scale;
                }
                else
                {
                    SyncData syncData = reader.Read<SyncData>();
                    position = syncData.position;
                    rotation = syncData.rotation;
                    scale = syncData.scale;
                }
            }
        }

        // rpc /////////////////////////////////////////////////////////////////
        // only unreliable. see comment above of this file.
        //[ClientRpc(channel = Channels.Unreliable)]
        //void RpcServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale) =>
        //    OnServerToClientSync(position, rotation, scale);

        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(ArraySegment<byte> payload)
        {
            OnServerToClientSync(payload);
        }

        // temporary kept as Tests rely on it
        protected virtual void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
        }
        // server broadcasts sync message to all clients
        protected virtual void OnServerToClientSync(ArraySegment<byte> receivedPayload)
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
#if onlySyncOnChange_BANDWIDTH_SAVING
            if (onlySyncOnChange)
            {
                double timeIntervalCheck = bufferResetMultiplier * sendIntervalMultiplier * NetworkServer.sendInterval;

                if (clientSnapshots.Count > 0 && clientSnapshots.Values[clientSnapshots.Count - 1].remoteTime + timeIntervalCheck < timestamp)
                    Reset();
            }
#endif
            DeconstructSyncData(receivedPayload, out Vector3? position, out Quaternion? rotation, out Vector3? scale);
            // position, rotation, scale can have no value if same as last time.
            // saves bandwidth.
            // but we still need to feed it to snapshot interpolation. we can't
            // just have gaps in there if nothing has changed. for example, if
            //   client sends snapshot at t=0
            //   client sends nothing for 10s because not moved
            //   client sends snapshot at t=10
            // then the server would assume that it's one super slow move and
            // replay it for 10 seconds.
            if (!position.HasValue) position = target.localPosition;
            if (!rotation.HasValue) rotation = target.localRotation;
            if (!scale.HasValue) scale = target.localScale;

            AddSnapshot(clientSnapshots, NetworkClient.connection.remoteTimeStamp + timeStampAdjustment + offset, position, rotation, scale);
        }

       
    }

    //SyncData is the struct used to construct a payload to send to server/clients
    //for use with the NetworkTransform. You can amend this to suit your needs for eg
    //if you are only using delta, or some form of compression, and your payload can be
    //byte[] or anything serializable.
    //Override Construct/Deconstruct methods in VariantNetworkTransformBase to
    //populate payload or retrieve position/rotation/scale data from payload.
    //Feel free to add new data types but remember to include custom reader/writer classes.

    [Serializable]
    public struct SyncData
    {
        public Vector3? position;
        public Quaternion? rotation;
        public Vector3? scale;

        public SyncData(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }

    public struct SyncDataCompressed
    {
        public Vector3? position;
        public uint? rotation;
        public Vector3? scale;

        public SyncDataCompressed(Vector3? position, uint? rotation, Vector3? scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }

    public static class CustomReaderWriter
    {
        public static void WriteSyncData(this NetworkWriter writer, SyncData syncData)
        {
            writer.WriteVector3Nullable(syncData.position);
            writer.WriteQuaternionNullable(syncData.rotation);
            writer.WriteVector3Nullable(syncData.scale);
        }

        public static SyncData ReadSyncData(this NetworkReader reader)
        {
            return new SyncData(
                reader.ReadVector3Nullable(),
                reader.ReadQuaternionNullable(),
                reader.ReadVector3Nullable()
            );
        }



        public static void WriteSyncDataCompressed(this NetworkWriter writer, SyncDataCompressed syncData)
        {
            writer.WriteVector3Nullable(syncData.position);
            writer.WriteUIntNullable(syncData.rotation);
            writer.WriteVector3Nullable(syncData.scale);
        }

        public static SyncDataCompressed ReadSyncDataCompressed(this NetworkReader reader)
        {
            return new SyncDataCompressed(
                reader.ReadVector3Nullable(),
                reader.ReadUIntNullable(),
                reader.ReadVector3Nullable()
            );
        }
    }
    
}
