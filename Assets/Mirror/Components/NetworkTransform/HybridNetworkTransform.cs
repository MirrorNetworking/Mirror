using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Mirror
{
    [AddComponentMenu("Network/Hybrid NetworkTransform")]
    public class HybridNetworkTransform : NetworkTransformBase
    {
        [Header("Full Send Interval Multiplier")]
        [Tooltip("Check/Sync every multiple of Network Manager send interval (= 1 / NM Send Rate), instead of every send interval.\n(30 NM send rate, and 3 interval, is a send every 0.1 seconds)\nA larger interval means less network sends, which has a variety of upsides. The drawbacks are delays and lower accuracy, you should find a nice balance between not sending too much, but the results looking good for your particular scenario.")]
        [Range(1, 120)]
        public uint fullSendIntervalMultiplier = 30;
        private uint fullSendIntervalCounter = 0;
        double lastFullSendIntervalTime = double.MinValue;
        private byte lastSentFullSyncIndex = 0;
        private SyncDataFull lastSentFullSyncData;
        private QuantizedSnapshot lastSentFullQuantized;
        private SyncDataFull lastReceivedFullSyncData;
        private QuantizedSnapshot lastReceivedFullQuantized;


        [Header("Delta Send Interval Multiplier")]
        [Tooltip("Check/Sync every multiple of Network Manager send interval (= 1 / NM Send Rate), instead of every send interval.\n(30 NM send rate, and 3 interval, is a send every 0.1 seconds)\nA larger interval means less network sends, which has a variety of upsides. The drawbacks are delays and lower accuracy, you should find a nice balance between not sending too much, but the results looking good for your particular scenario.")]
        [Range(1, 120)]
        public uint deltaSendIntervalMultiplier = 1;    
        private uint deltaSendIntervalCounter = 0;
        double lastDeltaSendIntervalTime = double.MinValue;    

        [Header("Rotation")]
        [Tooltip("Sensitivity of changes needed before an updated state is sent over the network")]
        public float rotationSensitivity = 0.01f;

        [Header("Precision")]
        [Tooltip("Position is rounded in order to drastically minimize bandwidth.\n\nFor example, a precision of 0.01 rounds to a centimeter. In other words, sub-centimeter movements aren't synced until they eventually exceeded an actual centimeter.\n\nDepending on how important the object is, a precision of 0.01-0.10 (1-10 cm) is recommended.\n\nFor example, even a 1cm precision combined with delta compression cuts the Benchmark demo's bandwidth in half, compared to sending every tiny change.")]
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float positionPrecision = 0.01f; // 1 cm
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float scalePrecision = 0.01f; // 1 cm

        [SerializeField] protected SyncSettings syncSettings;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            // NTBase has options to sync pos/rot/scale. Sync Settings has the same ability
            // If sync settings is not set, we use NTBase's settings.
            if (syncSettings == SyncSettings.None)
                syncSettings = InitSyncSettings();
        }

        protected virtual SyncSettings InitSyncSettings()
        {
            SyncSettings syncSettings = SyncSettings.None;

            if (syncPosition) syncSettings |= (SyncSettings.SyncPosX | SyncSettings.SyncPosY | SyncSettings.SyncPosZ);
            if (syncRotation) syncSettings |= SyncSettings.SyncRot;
            if (syncScale) syncSettings |= SyncSettings.SyncScale;
            if (compressRotation) syncSettings |= SyncSettings.CompressRot;

            return syncSettings;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if ((syncSettings & (SyncSettings.CompressRot & SyncSettings.UseEulerAngles)) > 0) syncSettings &= ~SyncSettings.CompressRot;

            deltaSendIntervalMultiplier = Math.Min(deltaSendIntervalMultiplier, fullSendIntervalMultiplier);
            fullSendIntervalMultiplier = Math.Max(deltaSendIntervalMultiplier, fullSendIntervalMultiplier);
        }

        #region Apply Interpolation
        void Update()
        {
            if (isServer) UpdateServerInterpolation();
            // for all other clients (and for local player if !authority),
            // we need to apply snapshots from the buffer.
            // 'else if' because host mode shouldn't interpolate client
            else if (isClient && !IsClientWithAuthority) UpdateClientInterpolation();
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
    #endregion

    #region Initial State Serialization
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
    #endregion

        void LateUpdate()
        {
            // if server then always sync to others.
            if (isServer) UpdateServerBroadcast();
            // client authority, and local player (= allowed to move myself)?
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient && IsClientWithAuthority) UpdateClientBroadcast();
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
            if (syncDirection == SyncDirection.ServerToClient || IsClientWithAuthority)
            {
                if (fullSendIntervalCounter == fullSendIntervalMultiplier) ServerBroadcastFull();
                else if (deltaSendIntervalCounter == deltaSendIntervalMultiplier) ServerBroadcastDelta();
            }
        }
    #region Server Broadcast Full
        protected virtual void ServerBroadcastFull()
        {
            lastSentFullSyncData = ConstructFullSyncData(true);

            lastSentFullQuantized = ConstructQuantizedSnapshot(lastSentFullSyncData.position, lastSentFullSyncData.rotation, lastSentFullSyncData.scale);

            RpcServerToClientSyncFull(lastSentFullSyncData);
        }

        private byte NextFullSyncIndex()
        {
            if (lastSentFullSyncIndex == 255) lastSentFullSyncIndex = 0;
            else lastSentFullSyncIndex += 1;

            return lastSentFullSyncIndex;
        }

        [ClientRpc]
        void RpcServerToClientSyncFull(SyncDataFull syncData) =>
            OnServerToClientSyncFull(syncData);
        
        protected virtual void OnServerToClientSyncFull(SyncDataFull syncData)
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

            double timestamp = NetworkClient.connection.remoteTimeStamp;

            // TODO, if we are syncing full by pos axis, we need to maybe
            // use current non-synced axis instead of giving it a 0.
            lastReceivedFullSyncData = syncData;
            lastReceivedFullQuantized = ConstructQuantizedSnapshot(syncData.position, syncData.rotation, syncData.scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(clientSnapshots, timestamp + timeStampAdjustment + offset, syncData.position, syncData.rotation, syncData.scale);
        }
    #endregion

    #region Server Broadcast Delta
        protected virtual void ServerBroadcastDelta()
        {
            SyncDataFull currentFull = ConstructFullSyncData(false);
            QuantizedSnapshot currentQuantized = ConstructQuantizedSnapshot(currentFull.position, currentFull.rotation, currentFull.scale);

            SyncDataDelta syncDataDelta = DeriveDelta(currentQuantized);

            RpcServerToClientSyncDelta(syncDataDelta);
        }

        [ClientRpc (channel = Channels.Unreliable)]
        void RpcServerToClientSyncDelta(SyncDataDelta syncData) =>
            OnServerToClientSyncDelta(syncData);
        
        protected virtual void OnServerToClientSyncDelta(SyncDataDelta delta)
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

            double timestamp = NetworkClient.connection.remoteTimeStamp;

            // If the delta syncdata is not based on the last received full sync, we discard.
            if (delta.fullSyncDataIndex != lastReceivedFullSyncData.fullSyncDataIndex) return;
            
            ApplyDelta(delta, out Vector3 position, out Quaternion rotation, out Vector3 scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(clientSnapshots, timestamp + timeStampAdjustment + offset, position, rotation, scale);
        }


    #endregion
        /*
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
        }*/

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

            if (syncDirection == SyncDirection.ServerToClient) return;

            if (fullSendIntervalCounter == fullSendIntervalMultiplier) ClientBroadcastFull();
            else if (deltaSendIntervalCounter == deltaSendIntervalMultiplier) ClientBroadcastDelta();
        }

    #region Client Broadcast Full
        protected virtual void ClientBroadcastFull()
        {
            lastSentFullSyncData = ConstructFullSyncData(true);
            
            lastSentFullQuantized = ConstructQuantizedSnapshot(lastSentFullSyncData.position, lastSentFullSyncData.rotation, lastSentFullSyncData.scale);
                
            CmdClientToServerSyncFull(lastSentFullSyncData);
        }

        [Command]
        void CmdClientToServerSyncFull(SyncDataFull syncData) 
        {
            OnClientToServerSyncFull(syncData);

            if (syncDirection == SyncDirection.ClientToServer)
                RpcServerToClientSyncFull(syncData);
        }
        
        protected virtual void OnClientToServerSyncFull(SyncDataFull syncData)
        {
            // in host mode, the server sends rpcs to all clients.
            // the host client itself will receive them too.
            // -> host server is always the source of truth
            // -> we can ignore any rpc on the host client
            // => otherwise host objects would have ever growing clientBuffers
            // (rpc goes to clients. if isServer is true too then we are host)
            if (syncDirection != SyncDirection.ClientToServer) return;

            double timestamp = connectionToClient.remoteTimeStamp;

            // See Server's issue
            lastReceivedFullSyncData = syncData;
            lastReceivedFullQuantized = ConstructQuantizedSnapshot(syncData.position, syncData.rotation, syncData.scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(serverSnapshots, timestamp + timeStampAdjustment + offset, syncData.position, syncData.rotation, syncData.scale);
        }
    #endregion

    #region Client Broadcast Delta
        protected virtual void ClientBroadcastDelta()
        {
            SyncDataFull currentFull = ConstructFullSyncData(false);
            QuantizedSnapshot currentQuantized = ConstructQuantizedSnapshot(currentFull.position, currentFull.rotation, currentFull.scale);

            SyncDataDelta syncDataDelta = DeriveDelta(currentQuantized);
            Debug.Log($"Client sending sync data delta index: {syncDataDelta.fullSyncDataIndex}");
            CmdClientToServerSyncDelta(syncDataDelta);            
        }

        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSyncDelta(SyncDataDelta delta) 
        {
            OnClientToServerSyncDelta(delta);

            if (syncDirection == SyncDirection.ClientToServer)
                RpcServerToClientSyncDelta(delta);
        }

        protected virtual void OnClientToServerSyncDelta(SyncDataDelta delta)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;  

            //if (!isLocalPlayer) Debug.Log($"delta index received: {delta.fullSyncDataIndex}, last received {lastReceivedFullSyncData.fullSyncDataIndex}");
            if (delta.fullSyncDataIndex != lastReceivedFullSyncData.fullSyncDataIndex) return;
            
            double timestamp = connectionToClient.remoteTimeStamp;

            ApplyDelta(delta, out Vector3 position, out Quaternion rotation, out Vector3 scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(serverSnapshots, timestamp + timeStampAdjustment + offset, position, rotation, scale);                      
        }
    #endregion
        protected virtual SyncDataFull ConstructFullSyncData(bool updateIndex)
        {
            return new SyncDataFull(
                updateIndex? NextFullSyncIndex() : lastSentFullSyncIndex, 
                syncSettings,
                GetPosition(),
                GetRotation(),
                GetScale()
            );
        }

        protected virtual QuantizedSnapshot ConstructQuantizedSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Compression.ScaleToLong(position, positionPrecision, out Vector3Long positionQuantized);
            Compression.ScaleToLong(scale, scalePrecision, out Vector3Long scaleQuantized);
            return new QuantizedSnapshot(
                positionQuantized,
                rotation,
                scaleQuantized
            );    
        }

        protected virtual SyncDataDelta DeriveDelta(QuantizedSnapshot current)
        {
            SyncDataDelta syncDataDelta = new SyncDataDelta();
            syncDataDelta.fullSyncDataIndex = lastSentFullSyncIndex;
            syncDataDelta.deltaHeader = DeltaHeader.None;

            syncDataDelta.position = current.position - lastSentFullQuantized.position;

            if ((syncSettings & SyncSettings.SyncPosX) > 0 && syncDataDelta.position.x != 0)
                syncDataDelta.deltaHeader |= DeltaHeader.PosX;

            if ((syncSettings & SyncSettings.SyncPosY) > 0 && syncDataDelta.position.y != 0)
                syncDataDelta.deltaHeader |= DeltaHeader.PosY;
    
            if ((syncSettings & SyncSettings.SyncPosZ) > 0 && syncDataDelta.position.z != 0)
            {
                syncDataDelta.deltaHeader |= DeltaHeader.PosZ;
            }

            // Rotation: We have 3 options:
            // 1) Send compressed Quaternion
            // 2) Send uncompressed Quaternion
            // 3) Send Euler Angles
            // If user only ever rotates 1, 2 axes then option 3 may save more bandwidth since
            // we delta each axis.
            // 1 and 2 prevents gimbal lock etc, and 2 if user requires absolute precision.
            // We use 4 bits to express rotation type and change (NonEulerAngles, RotX, RotY, RotZ):
            // 1) If nothing has changed or < rotationSensitivity, all 4 will be false. Doesn't matter which method
            // we are treating rotation. We are not reading anything on the receiving side.
            // 2) If NonEulerAngles is false, we check the next 3 for each individual axis.
            // 3) If NonEulerAngles is true, we are sending Quaternion. We piggyback on the RotX bit to tell us
            // if it is compressed Quat or uncompressed Quat.
            if ((syncSettings & SyncSettings.SyncRot) > 0)
            {
                if ((syncSettings & SyncSettings.UseEulerAngles) > 0)
                {
                    Compression.ScaleToLong(lastSentFullQuantized.rotation.eulerAngles, rotationSensitivity, out Vector3Long lastRotationEuler);
                    Compression.ScaleToLong(current.rotation.eulerAngles, rotationSensitivity, out Vector3Long currentRotationEuler);
    
                    syncDataDelta.eulRotation = currentRotationEuler - lastRotationEuler;

                    if (syncDataDelta.eulRotation.x != 0) syncDataDelta.deltaHeader |= DeltaHeader.RotX;
                    if (syncDataDelta.eulRotation.y != 0) syncDataDelta.deltaHeader |= DeltaHeader.RotY;
                    if (syncDataDelta.eulRotation.z != 0) syncDataDelta.deltaHeader |= DeltaHeader.RotZ;
                }
                else
                {
                    if (Quaternion.Angle(lastSentFullQuantized.rotation, current.rotation) > rotationSensitivity)
                    {
                        syncDataDelta.quatRotation = current.rotation;
                        syncDataDelta.deltaHeader |= DeltaHeader.SendQuat;
                        if ((syncSettings & SyncSettings.CompressRot) > 0)
                        {
                            syncDataDelta.deltaHeader |= DeltaHeader.RotX;
                        }
                    }
                }                
            }

            if ((syncSettings & SyncSettings.SyncScale) > 0)
            {
                syncDataDelta.scale = current.scale - lastSentFullQuantized.scale;
                if (syncDataDelta.scale != Vector3Long.zero)
                {
                    syncDataDelta.deltaHeader |= DeltaHeader.Scale;
                }
            }

            return syncDataDelta;
        }

        protected virtual void ApplyDelta(SyncDataDelta delta, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Compression.ScaleToFloat(lastReceivedFullQuantized.position + delta.position, positionPrecision);

            if ((lastReceivedFullSyncData.syncSettings & SyncSettings.UseEulerAngles) > 0)
            {
                Vector3 eulRotation = Compression.ScaleToFloat(lastReceivedFullQuantized.rotationEuler + delta.eulRotation, rotationSensitivity);

                rotation = Quaternion.Euler(eulRotation);
            }
            else
            {
                if ((delta.deltaHeader & DeltaHeader.SendQuat) > 0)
                    rotation = delta.quatRotation;
                else
                    rotation = lastReceivedFullSyncData.rotation;
            }

            scale = Compression.ScaleToFloat(lastReceivedFullQuantized.scale + delta.scale, scalePrecision);
        }

        protected virtual void CheckLastSendTime()
        {
            // We check interval every frame, and then send if interval is reached.
            // So by the time sendIntervalCounter == sendIntervalMultiplier, data is sent,
            // thus we reset the counter here.
            // This fixes previous issue of, if sendIntervalMultiplier = 1, we send every frame,
            // because intervalCounter is always = 1 in the previous version.

            if (fullSendIntervalCounter == fullSendIntervalMultiplier) fullSendIntervalCounter = 0;

            if (deltaSendIntervalCounter == deltaSendIntervalMultiplier) deltaSendIntervalCounter = 0;

            // timeAsDouble not available in older Unity versions.
            if (AccurateInterval.Elapsed(NetworkTime.localTime, NetworkServer.sendInterval, ref lastFullSendIntervalTime))
                fullSendIntervalCounter++;
            
            if (AccurateInterval.Elapsed(NetworkTime.localTime, NetworkServer.sendInterval, ref lastDeltaSendIntervalTime))
                deltaSendIntervalCounter++;
        }   
    }
}