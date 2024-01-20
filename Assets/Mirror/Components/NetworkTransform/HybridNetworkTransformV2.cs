using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor.UIElements;
using UnityEditor;

namespace Mirror
{
    [AddComponentMenu("Network/Hybrid NetworkTransform V2")]
    public class HybridNetworkTransformV2 : NetworkBehaviour
    {
        // target transform to sync. can be on a child.
        // TODO this field is kind of unnecessary since we now support child NetworkBehaviours
        [Header("Target")]
        [Tooltip("The Transform component to sync. May be on on this GameObject, or on a child.")]
        public Transform target;
        
        [Header("Sync Settings")]
        [Tooltip("Select the attributes to sync")]
        [SerializeField] protected SyncSettings syncSettings = SyncSettings.SyncPosX | SyncSettings.SyncPosY | SyncSettings.SyncPosZ | SyncSettings.SyncRot;
        protected FullHeader fullHeader;   

        [Header("Rotation")]
        [Tooltip("Send Rotation data as uncompressed Quaternion, compressed Quaternion (smallest 3) or by Euler Angles")]
        [SerializeField] protected RotationSettings rotationSettings = RotationSettings.Compressed;
        [Tooltip("Sensitivity of changes needed before an updated state is sent over the network. This will be used for precision if Euler Angles is chosen")]        
        public float rotationSensitivity = 0.01f;

        [Header("Precision")]
        [Tooltip("Position is rounded in order to drastically minimize bandwidth.\n\nFor example, a precision of 0.01 rounds to a centimeter. In other words, sub-centimeter movements aren't synced until they eventually exceeded an actual centimeter.\n\nDepending on how important the object is, a precision of 0.01-0.10 (1-10 cm) is recommended.\n\nFor example, even a 1cm precision combined with delta compression cuts the Benchmark demo's bandwidth in half, compared to sending every tiny change.")]
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float positionPrecision = 0.01f; // 1 cm
        [Range(0.00_01f, 1f)]                   // disallow 0 division. 1mm to 1m precision is enough range.
        public float scalePrecision = 0.01f; // 1 cm        

     

        /*[Header("Full Send Interval Multiplier")]
        [Tooltip("Check/Sync every multiple of Network Manager send interval (= 1 / NM Send Rate), instead of every send interval.\n(30 NM send rate, and 3 interval, is a send every 0.1 seconds)\nA larger interval means less network sends, which has a variety of upsides. The drawbacks are delays and lower accuracy, you should find a nice balance between not sending too much, but the results looking good for your particular scenario.")]
        [Range(1, 120)]*/
        [Header("Full Send Frequency")]
        [Tooltip("Number of full snapshots to send per second.")]
        public float fullSendFrequency = 1;
        private float fullSendInterval => 1 / fullSendFrequency;
        //public uint fullSendIntervalMultiplier = 30;
        //private uint fullSendIntervalCounter = 0;
        double lastFullSendIntervalTime = double.MinValue;
        private byte lastSentFullSyncIndex = 0;
        private SyncDataFull lastSentFullSyncData;
        //private QuantizedSnapshot lastSentFullQuantized;
        private SyncDataFull lastReceivedFullSyncData;
        //private QuantizedSnapshot lastReceivedFullQuantized;

        /*[Header("Delta Send Interval Multiplier")]
        [Tooltip("Check/Sync every multiple of Network Manager send interval (= 1 / NM Send Rate), instead of every send interval.\n(30 NM send rate, and 3 interval, is a send every 0.1 seconds)\nA larger interval means less network sends, which has a variety of upsides. The drawbacks are delays and lower accuracy, you should find a nice balance between not sending too much, but the results looking good for your particular scenario.")]
        [Range(1, 120)]*/
        [Header("Delta Send Frequency")]
        [Tooltip("Number of delta snapshots to send per second.")]        
        public float deltaSendFrequency = 30;
        private float deltaSendInterval => 1 / deltaSendFrequency;
        //public uint deltaSendIntervalMultiplier = 1;    
        //private uint deltaSendIntervalCounter = 0;
        double lastDeltaSendIntervalTime = double.MinValue;

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
        //protected double timeStampAdjustment => NetworkServer.sendInterval * (deltaSendIntervalMultiplier - 1);
        protected double timeStampAdjustment => Mathf.Max(0, deltaSendInterval - NetworkServer.sendInterval);
        protected double offset => timelineOffset ? deltaSendInterval : 0;            

        // interpolation is on by default, but can be disabled to jump to
        // the destination immediately. some projects need this.
        [Header("Interpolation")]
        [Tooltip("Set to false to have a snap-like effect.")]
        [SerializeField]
        public InterpolateSettings interpolateSettings = InterpolateSettings.Position | InterpolateSettings.Rotation | InterpolateSettings.Scale;    
        
        [Header("Reference Space")]
        [Tooltip("Local by default. World may be better when changing hierarchy, or non-NetworkTransforms root position/rotation/scale values.")]
        public ReferenceSpace coordinateSpace = ReferenceSpace.Local;        

        protected bool IsClientWithAuthority => isClient && authority;
        public readonly SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>(16);
        public readonly SortedList<double, TransformSnapshot> serverSnapshots = new SortedList<double, TransformSnapshot>(16);        

        protected bool syncPosition => (fullHeader & FullHeader.SyncPosX) > 0 
                                    || (fullHeader & FullHeader.SyncPosY) > 0
                                    || (fullHeader & FullHeader.SyncPosZ) > 0;
        
        protected bool syncRotation => (fullHeader & FullHeader.SyncRot) > 0;
        protected bool syncScale => (fullHeader & FullHeader.SyncScale) > 0;

        // Static bool to indicate if that connection has registered handlers.
        private static bool registeredHandlers = false;
        // Register with the server if the handlers have been registered to begin syncing.
        // Else it causes message ID unknown errors.
        // Note, the hashset does not remove connections when they are disconnected. This
        // should be fixed at some point. There should not be an issue because when sending
        // this list is checked against the identity observers list which is updated.
        protected static HashSet<NetworkConnectionToClient> registeredConnections = new HashSet<NetworkConnectionToClient>();

    #region Register Message Handlers
        public override void OnStartServer()
        {
            if (!registeredHandlers) RegisterServerHandlers();
        }

        public override void OnStartClient()
        {
            if (!registeredHandlers) RegisterClientHandlers();
                        
            CmdRegisteredHandler();
        }

        protected virtual void RegisterServerHandlers()
        {
            NetworkServer.RegisterHandler<SyncDataFullMsg>(ClientToServerSyncFullHandler);
            NetworkServer.RegisterHandler<SyncDataDeltaMsg>(ClientToServerSyncDeltaHandler);
            registeredHandlers = true;
        }

        protected virtual void RegisterClientHandlers()
        {
            if (isServer) return;
            NetworkClient.RegisterHandler<SyncDataFullMsg>(ServerToClientSyncFullHandler);
            NetworkClient.RegisterHandler<SyncDataDeltaMsg>(ServerToClientSyncDeltaHandler);  
            registeredHandlers = true;          
        }

        [Command(requiresAuthority = false)]
        private void CmdRegisteredHandler()
        {
            if (!registeredConnections.Contains(connectionToClient))
                registeredConnections.Add(connectionToClient);
        }
    #endregion

        private void Awake()
        {
            InitFullHeader();
        }

        private void InitFullHeader()
        {
            fullHeader = (FullHeader)syncSettings;
            if (rotationSettings == RotationSettings.Compressed)
            {
                fullHeader |= FullHeader.CompressRot;
            }
            else if (rotationSettings == RotationSettings.EulerAngles)
            {
                fullHeader |= FullHeader.UseEulerAngles;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if ((fullHeader & (FullHeader.CompressRot & FullHeader.UseEulerAngles)) > 0) fullHeader &= ~FullHeader.CompressRot;

            //deltaSendIntervalMultiplier = Math.Min(deltaSendIntervalMultiplier, fullSendIntervalMultiplier);
            //fullSendIntervalMultiplier = Math.Max(deltaSendIntervalMultiplier, fullSendIntervalMultiplier);
            deltaSendFrequency = Mathf.Max (deltaSendFrequency, fullSendFrequency);
            fullSendFrequency = Mathf.Min (deltaSendFrequency, fullSendFrequency);
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
            if (syncPosition) SetPosition((interpolateSettings & InterpolateSettings.Position) > 0 ? interpolated.position : endGoal.position);
            if (syncRotation) SetRotation((interpolateSettings & InterpolateSettings.Rotation) > 0 ? interpolated.rotation : endGoal.rotation);
            if (syncScale) SetScale((interpolateSettings & InterpolateSettings.Scale) > 0 ? interpolated.scale : endGoal.scale);
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
                if (lastSentFullSyncIndex == 0) lastSentFullSyncData = ConstructFullSyncData(true);
                writer.WriteByte(lastSentFullSyncIndex);
                if (syncPosition) writer.WriteVector3(lastSentFullSyncData.position);
                if (syncRotation) writer.WriteQuaternion(lastSentFullSyncData.rotation);
                if (syncScale) writer.WriteVector3(lastSentFullSyncData.scale);

                //lastSentFullQuantized = ConstructQuantizedSnapshot(lastSentFullSyncData.position, lastSentFullSyncData.rotation, lastSentFullSyncData.scale);
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {          
            // sync target component's position on spawn.
            // fixes https://github.com/vis2k/Mirror/pull/3051/
            // (Spawn message wouldn't sync NTChild positions either)
            if (initialState)
            {
                byte index = reader.ReadByte();

                Vector3 position = GetPosition();
                Quaternion rotation = GetRotation();
                Vector3 scale = GetScale();
                if (syncPosition) position = reader.ReadVector3();
                if (syncRotation) rotation = reader.ReadQuaternion();
                if (syncScale) scale = reader.ReadVector3();

                SyncDataFull syncData = new SyncDataFull(index, fullHeader, position, rotation, scale);
                
                if (isServer) OnClientToServerSyncFull(syncData);
                else if (isClient) OnServerToClientSyncFull(syncData);
            }
        }
    #endregion

    #region Update
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

            //CheckLastSendTime();
            if (syncDirection == SyncDirection.ServerToClient || IsClientWithAuthority)
            {
                if (AccurateInterval.Elapsed(NetworkTime.localTime, fullSendInterval, ref lastFullSendIntervalTime))
                    ServerBroadcastFull();
                else if (AccurateInterval.Elapsed(NetworkTime.localTime, deltaSendInterval, ref lastDeltaSendIntervalTime))
                    ServerBroadcastDelta();
                
                /*if (fullSendIntervalCounter == fullSendIntervalMultiplier) ServerBroadcastFull();
                else if (deltaSendIntervalCounter == deltaSendIntervalMultiplier) ServerBroadcastDelta();*/
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
            
            //CheckLastSendTime();

            if (syncDirection == SyncDirection.ServerToClient) return;

            if (AccurateInterval.Elapsed(NetworkTime.localTime, fullSendInterval, ref lastFullSendIntervalTime))
                ClientBroadcastFull();
            else if (AccurateInterval.Elapsed(NetworkTime.localTime, deltaSendInterval, ref lastDeltaSendIntervalTime))
                ClientBroadcastDelta();
            
            /*if (fullSendIntervalCounter == fullSendIntervalMultiplier) ClientBroadcastFull();
            else if (deltaSendIntervalCounter == deltaSendIntervalMultiplier) ClientBroadcastDelta();*/
        }
    #endregion

    #region Server Broadcast Full
        protected virtual void ServerBroadcastFull()
        {
            lastSentFullSyncData = ConstructFullSyncData(true);

            //lastSentFullQuantized = ConstructQuantizedSnapshot(lastSentFullSyncData.position, lastSentFullSyncData.rotation, lastSentFullSyncData.scale);

            SyncDataFullMsg msg = new SyncDataFullMsg(netId, ComponentIndex, lastSentFullSyncData);
            SendToReadyObservers(netIdentity, msg, false, Channels.Reliable); // will exclude owner be problematic?

            //NetworkServer.SendToReadyObservers(netIdentity, msg, false, Channels.Reliable); // will exclude owner be problematic?
        }

        private byte NextFullSyncIndex()
        {
            if (lastSentFullSyncIndex == 255) lastSentFullSyncIndex = 0;
            else lastSentFullSyncIndex += 1;
            
            return lastSentFullSyncIndex;
        }

        protected static void ServerToClientSyncFullHandler(SyncDataFullMsg msg)
        {
            if (!NetworkClient.spawned.ContainsKey(msg.netId)) return;

            ((HybridNetworkTransformV2)NetworkClient.spawned[msg.netId].NetworkBehaviours[msg.componentId]).OnServerToClientSyncFull(msg.syncData);
        }

        public virtual void OnServerToClientSyncFull(SyncDataFull syncData)
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
            CleanUpFullSyncDataPositionSync(ref lastReceivedFullSyncData);
            //lastReceivedFullQuantized = ConstructQuantizedSnapshot(lastReceivedFullSyncData.position, lastReceivedFullSyncData.rotation, lastReceivedFullSyncData.scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(clientSnapshots, timestamp + timeStampAdjustment + offset, lastReceivedFullSyncData.position, lastReceivedFullSyncData.rotation, lastReceivedFullSyncData.scale);
        }

    #endregion

    #region Server Broadcast Delta
        protected virtual void ServerBroadcastDelta()
        {
            // If we have not sent a full sync, we don't send delta.
            
            if (lastSentFullSyncIndex == 0) return;
            SyncDataFull currentFull = ConstructFullSyncData(false);
            //QuantizedSnapshot currentQuantized = ConstructQuantizedSnapshot(currentFull.position, currentFull.rotation, currentFull.scale);

            SyncDataDelta syncDataDelta = DeriveDelta(currentFull);
            
            SyncDataDeltaMsg msg = new SyncDataDeltaMsg(netId, ComponentIndex, syncDataDelta);
            SendToReadyObservers(netIdentity, msg, false, Channels.Unreliable); // will exclude owner be problematic?

            //NetworkServer.SendToReadyObservers(netIdentity, msg, false, Channels.Unreliable); // will exclude owner be problematic?
            
        }

        protected static void ServerToClientSyncDeltaHandler(SyncDataDeltaMsg msg)
        {
            if (!NetworkClient.spawned.ContainsKey(msg.netId)) return;

            ((HybridNetworkTransformV2)NetworkClient.spawned[msg.netId].NetworkBehaviours[msg.componentId]).OnServerToClientSyncDelta(msg.syncData);            
        }        

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

    #region Client Broadcast Full
        protected virtual void ClientBroadcastFull()
        {
            lastSentFullSyncData = ConstructFullSyncData(true);
            
            //lastSentFullQuantized = ConstructQuantizedSnapshot(lastSentFullSyncData.position, lastSentFullSyncData.rotation, lastSentFullSyncData.scale);

            SyncDataFullMsg msg = new SyncDataFullMsg(netId, ComponentIndex, lastSentFullSyncData);
            NetworkClient.Send(msg, Channels.Reliable);    
        }

        protected static void ClientToServerSyncFullHandler(NetworkConnectionToClient conn, SyncDataFullMsg msg)
        {

            if (!NetworkServer.spawned.ContainsKey(msg.netId)) return;
            NetworkIdentity networkIdentity = NetworkServer.spawned[msg.netId];
            
            if (networkIdentity.NetworkBehaviours[msg.componentId].connectionToClient != conn)
            {
                Debug.LogError($"Received Full Sync Msg from client {conn} who does not own object");
                return;
            }

            HybridNetworkTransformV2 nT =  (HybridNetworkTransformV2)networkIdentity.NetworkBehaviours[msg.componentId];
            nT.OnClientToServerSyncFull(msg.syncData);

            if (networkIdentity.NetworkBehaviours[msg.componentId].syncDirection == SyncDirection.ClientToServer)
                nT.SendToReadyObservers(networkIdentity, msg, false, Channels.Reliable); // will exclude owner be problematic?
                //NetworkServer.SendToReadyObservers(networkIdentity, msg, false, Channels.Reliable); // will exclude owner be problematic?
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
            CleanUpFullSyncDataPositionSync(ref lastReceivedFullSyncData);
            //lastReceivedFullQuantized = ConstructQuantizedSnapshot(lastReceivedFullSyncData.position, lastReceivedFullSyncData.rotation, lastReceivedFullSyncData.scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(serverSnapshots, timestamp + timeStampAdjustment + offset, lastReceivedFullSyncData.position, lastReceivedFullSyncData.rotation, lastReceivedFullSyncData.scale);
        }
    #endregion

    #region Client Broadcast Delta
        protected virtual void ClientBroadcastDelta()
        {
            // If we have not sent a full sync, we don't send delta.
            if (lastSentFullSyncIndex == 0) return;            
            
            SyncDataFull currentFull = ConstructFullSyncData(false);
            //QuantizedSnapshot currentQuantized = ConstructQuantizedSnapshot(currentFull.position, currentFull.rotation, currentFull.scale);
              
            SyncDataDelta syncDataDelta = DeriveDelta(currentFull);
            
            SyncDataDeltaMsg msg = new SyncDataDeltaMsg(netId, ComponentIndex, syncDataDelta);
            NetworkClient.Send(msg, Channels.Unreliable);   
        }

        protected static void ClientToServerSyncDeltaHandler(NetworkConnectionToClient conn, SyncDataDeltaMsg msg)
        {
            if (!NetworkServer.spawned.ContainsKey(msg.netId)) return;
            
            NetworkIdentity networkIdentity = NetworkServer.spawned[msg.netId];
            
            if (networkIdentity.NetworkBehaviours[msg.componentId].connectionToClient != conn)
            {
                Debug.LogError($"Received Full Sync Msg from client {conn} who does not own object");
                return;
            }

            HybridNetworkTransformV2 nT =  (HybridNetworkTransformV2)networkIdentity.NetworkBehaviours[msg.componentId];
            nT.OnClientToServerSyncDelta(msg.syncData);

            if (networkIdentity.NetworkBehaviours[msg.componentId].syncDirection == SyncDirection.ClientToServer)
                nT.SendToReadyObservers(networkIdentity, msg, false, Channels.Unreliable); // will exclude owner be problematic?
                //NetworkServer.SendToReadyObservers(networkIdentity, msg, false, Channels.Unreliable); // will exclude owner be problematic?
        }        

        protected virtual void OnClientToServerSyncDelta(SyncDataDelta delta)
        {
            // only apply if in client authority mode
            if (syncDirection != SyncDirection.ClientToServer) return;

            // protect against ever growing buffer size attacks
            if (serverSnapshots.Count >= connectionToClient.snapshotBufferSizeLimit) return;  

            if (delta.fullSyncDataIndex != lastReceivedFullSyncData.fullSyncDataIndex) return;
            
            double timestamp = connectionToClient.remoteTimeStamp;

            ApplyDelta(delta, out Vector3 position, out Quaternion rotation, out Vector3 scale);

            // We don't care if we are adding 'default' to any field because 
            // syncing is checked again in Apply before applying the changes.
            AddSnapshot(serverSnapshots, timestamp + timeStampAdjustment + offset, position, rotation, scale);                      
        }
    #endregion

    #region SyncData Functions
        protected virtual SyncDataFull ConstructFullSyncData(bool updateIndex)
        {
            return new SyncDataFull(
                updateIndex? NextFullSyncIndex() : lastSentFullSyncIndex, 
                fullHeader,
                GetPosition(),
                GetRotation(),
                GetScale()
            );
        }

        /*protected virtual QuantizedSnapshot ConstructQuantizedSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Compression.ScaleToLong(position, positionPrecision, out Vector3Long positionQuantized);
            Compression.ScaleToLong(rotation.eulerAngles, rotationSensitivity, out Vector3Long eulRotation);
            Compression.ScaleToLong(scale, scalePrecision, out Vector3Long scaleQuantized);
            return new QuantizedSnapshot(
                positionQuantized,
                rotation,
                eulRotation,
                scaleQuantized
            );    
        }*/

        protected virtual SyncDataDelta DeriveDelta(SyncDataFull current)
        {
            SyncDataDelta syncDataDelta = new SyncDataDelta();
            syncDataDelta.fullSyncDataIndex = lastSentFullSyncIndex;
            syncDataDelta.deltaHeader = DeltaHeader.None;

            Compression.ScaleToLong(current.position - lastSentFullSyncData.position, positionPrecision, out syncDataDelta.position);

            if ((fullHeader & FullHeader.SyncPosX) > 0 && syncDataDelta.position.x != 0)
                syncDataDelta.deltaHeader |= DeltaHeader.PosX;

            if ((fullHeader & FullHeader.SyncPosY) > 0 && syncDataDelta.position.y != 0)
                syncDataDelta.deltaHeader |= DeltaHeader.PosY;
    
            if ((fullHeader & FullHeader.SyncPosZ) > 0 && syncDataDelta.position.z != 0)
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
            if ((fullHeader & FullHeader.SyncRot) > 0)
            {
                if ((fullHeader & FullHeader.UseEulerAngles) > 0)
                {
                    Compression.ScaleToLong(lastSentFullSyncData.rotation.eulerAngles, rotationSensitivity, out Vector3Long lastRotationEuler);
                    Compression.ScaleToLong(current.rotation.eulerAngles, rotationSensitivity, out Vector3Long currentRotationEuler);
    
                    syncDataDelta.eulRotation = currentRotationEuler - lastRotationEuler;

                    if (syncDataDelta.eulRotation.x != 0) syncDataDelta.deltaHeader |= DeltaHeader.RotX;
                    if (syncDataDelta.eulRotation.y != 0) syncDataDelta.deltaHeader |= DeltaHeader.RotY;
                    if (syncDataDelta.eulRotation.z != 0) syncDataDelta.deltaHeader |= DeltaHeader.RotZ;
                }
                else
                {
                    if (Quaternion.Angle(lastSentFullSyncData.rotation, current.rotation) > rotationSensitivity)
                    {
                        syncDataDelta.quatRotation = current.rotation;
                        syncDataDelta.deltaHeader |= DeltaHeader.SendQuat;
                        if ((fullHeader & FullHeader.CompressRot) > 0)
                        {
                            syncDataDelta.deltaHeader |= DeltaHeader.SendQuatCompressed;
                        }
                    }
                }                
            }

            if ((fullHeader & FullHeader.SyncScale) > 0)
            {
                Compression.ScaleToLong(current.scale - lastSentFullSyncData.scale, positionPrecision, out syncDataDelta.scale);
                if (syncDataDelta.scale != Vector3Long.zero)
                {
                    syncDataDelta.deltaHeader |= DeltaHeader.Scale;
                }
            }

            return syncDataDelta;
        }

        protected virtual void ApplyDelta(SyncDataDelta delta, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = lastReceivedFullSyncData.position + Compression.ScaleToFloat(delta.position, positionPrecision);

            if ((lastReceivedFullSyncData.fullHeader & FullHeader.UseEulerAngles) > 0)
            {
                Vector3 eulRotation = lastReceivedFullSyncData.rotation.eulerAngles + Compression.ScaleToFloat(delta.eulRotation, rotationSensitivity);

                rotation = Quaternion.Euler(eulRotation);
            }
            else
            {
                if ((delta.deltaHeader & DeltaHeader.SendQuat) > 0)
                    rotation = delta.quatRotation;
                else
                    rotation = lastReceivedFullSyncData.rotation;
            }

            scale = lastReceivedFullSyncData.scale + Compression.ScaleToFloat(delta.scale, scalePrecision);
        }
    #endregion

        public void SendToReadyObservers<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            if (identity == null || identity.observers.Count == 0)
                return;
 
            foreach (NetworkConnectionToClient conn in identity.observers.Values)
            {
                if (conn == NetworkServer.localConnection) continue;

                bool isOwner = conn == identity.connectionToClient;
                if ((!isOwner || includeOwner) && conn.isReady && registeredConnections.Contains(conn))
                {
                    conn.Send(message, channelId);
                }
            }  
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

        /*protected virtual void CheckLastSendTime()
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
        }*/

    #region Snapshot Functions
        // snapshot functions //////////////////////////////////////////////////
        // get local/world position
        protected virtual Vector3 GetPosition() =>
            coordinateSpace == ReferenceSpace.Local ? target.localPosition : target.position;

        // get local/world rotation
        protected virtual Quaternion GetRotation() =>
            coordinateSpace == ReferenceSpace.Local ? target.localRotation : target.rotation;

        // get local/world scale
        protected virtual Vector3 GetScale() =>
            coordinateSpace == ReferenceSpace.Local ? target.localScale : target.lossyScale;

        // set local/world position
        protected virtual void SetPosition(Vector3 position)
        {
            if (coordinateSpace == ReferenceSpace.Local)
                target.localPosition = position;
            else
                target.position = position;
        }     

        // set local/world rotation
        protected virtual void SetRotation(Quaternion rotation)
        {
            if (coordinateSpace == ReferenceSpace.Local)
                target.localRotation = rotation;
            else
                target.rotation = rotation;
        }

        // set local/world position
        protected virtual void SetScale(Vector3 scale)
        {
            if (coordinateSpace == ReferenceSpace.Local)
                target.localScale = scale;
            // Unity doesn't support setting world scale.
            // OnValidate disables syncScale in world mode.
            // else
            // target.lossyScale = scale; // TODO
        }       


        // If we did not sync certain position axis, we need to fill it up in syncData
        // with the current axis value.
        protected virtual void CleanUpFullSyncDataPositionSync(ref SyncDataFull syncData)
        {
            Vector3 currentPosition = GetPosition();

            if ((fullHeader & FullHeader.SyncPosX) == 0) syncData.position.x = currentPosition.x;
            if ((fullHeader & FullHeader.SyncPosY) == 0) syncData.position.y = currentPosition.y;
            if ((fullHeader & FullHeader.SyncPosZ) == 0) syncData.position.z = currentPosition.z;
        }         
    #endregion 


#if UNITY_EDITOR
        void OnGUI()
        {
            fullHeader = (FullHeader)EditorGUILayout.EnumPopup(fullHeader);
        }
#endif         
    }
}