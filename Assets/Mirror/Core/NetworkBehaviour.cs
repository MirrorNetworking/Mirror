using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    // SyncMode decides if a component is synced to all observers, or only owner
    public enum SyncMode { Observers, Owner }

    // SyncDirection decides if a component is synced from:
    //   * server to all clients
    //   * owner client, to server, to all other clients
    //
    // naming: 'ClientToServer' etc. instead of 'ClientAuthority', because
    // that wouldn't be accurate. server's OnDeserialize can still validate
    // client data before applying. it's really about direction, not authority.
    public enum SyncDirection { ServerToClient, ClientToServer }

    /// <summary>Base class for networked components.</summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/networkbehaviour")]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>Sync direction for OnSerialize. ServerToClient by default. ClientToServer for client authority.</summary>
        [Tooltip("Server Authority calls OnSerialize on the server and syncs it to clients.\n\nClient Authority calls OnSerialize on the owning client, syncs it to server, which then broadcasts it to all other clients.\n\nUse server authority for cheat safety.")]
        [HideInInspector] public SyncDirection syncDirection = SyncDirection.ServerToClient;

        /// <summary>sync mode for OnSerialize</summary>
        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        [Tooltip("By default synced data is sent from the server to all Observers of the object.\nChange this to Owner to only have the server update the client that has ownership authority for this object")]
        [HideInInspector] public SyncMode syncMode = SyncMode.Observers;

        /// <summary>sync interval for OnSerialize (in seconds)</summary>
        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        // [0,2] should be enough. anything >2s is too laggy anyway.
        [Tooltip("Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)")]
        [Range(0, 2)]
        [HideInInspector] public float syncInterval = 0.1f;
        internal double lastSyncTime;

        /// <summary>True if this object is on the server and has been spawned.</summary>
        // This is different from NetworkServer.active, which is true if the
        // server itself is active rather than this object being active.
        public bool isServer => netIdentity.isServer;

        /// <summary>True if this object is on the client and has been spawned by the server.</summary>
        public bool isClient => netIdentity.isClient;

        /// <summary>True if this object is the the client's own local player.</summary>
        public bool isLocalPlayer => netIdentity.isLocalPlayer;

        /// <summary>True if this object is on the server-only, not host.</summary>
        public bool isServerOnly => netIdentity.isServerOnly;

        /// <summary>True if this object is on the client-only, not host.</summary>
        public bool isClientOnly => netIdentity.isClientOnly;

        /// <summary>isOwned is true on the client if this NetworkIdentity is one of the .owned entities of our connection on the server.</summary>
        // for example: main player & pets are owned. monsters & npcs aren't.
        public bool isOwned => netIdentity.isOwned;

        // Deprecated 2022-10-13
        [Obsolete(".hasAuthority was renamed to .isOwned. This is easier to understand and prepares for SyncDirection, where there is a difference betwen isOwned and authority.")]
        public bool hasAuthority => isOwned;

        /// <summary>authority is true if we are allowed to modify this component's state. On server, it's true if SyncDirection is ServerToClient. On client, it's true if SyncDirection is ClientToServer and(!) if this object is owned by the client.</summary>
        // on the client: if owned and if clientAuthority sync direction
        // on the server: if serverAuthority sync direction
        //
        // for example, NetworkTransform:
        //   client may modify position if ClientAuthority mode and owned
        //   server may modify position only if server authority
        //
        // note that in original Mirror, hasAuthority only meant 'isOwned'.
        // there was no syncDirection to check.
        //
        // also note that this is a per-NetworkBehaviour flag.
        // another component may not be client authoritative, etc.
        public bool authority =>
            isClient
                ? syncDirection == SyncDirection.ClientToServer && isOwned
                : syncDirection == SyncDirection.ServerToClient;

        /// <summary>The unique network Id of this object (unique at runtime).</summary>
        public uint netId => netIdentity.netId;

        /// <summary>Client's network connection to the server. This is only valid for player objects on the client.</summary>
        // TODO change to NetworkConnectionToServer, but might cause some breaking
        public NetworkConnection connectionToServer => netIdentity.connectionToServer;

        /// <summary>Server's network connection to the client. This is only valid for player objects on the server.</summary>
        public NetworkConnectionToClient connectionToClient => netIdentity.connectionToClient;

        // SyncLists, SyncSets, etc.
        protected readonly List<SyncObject> syncObjects = new List<SyncObject>();

        // NetworkBehaviourInspector needs to know if we have SyncObjects
        internal bool HasSyncObjects() => syncObjects.Count > 0;

        // NetworkIdentity based values set from NetworkIdentity.Awake(),
        // which is way more simple and way faster than trying to figure out
        // component index from in here by searching all NetworkComponents.

        /// <summary>Returns the NetworkIdentity of this object</summary>
        public NetworkIdentity netIdentity { get; internal set; }

        /// <summary>Returns the index of the component on this object</summary>
        public byte ComponentIndex { get; internal set; }

        // to avoid fully serializing entities every time, we have two options:
        // * run a delta compression algorithm
        //   -> for fixed size types this is as easy as varint(b-a) for all
        //   -> for dynamically sized types like strings this is not easy.
        //      algorithms need to detect inserts/deletions, i.e. Myers Diff.
        //      those are very cpu intensive and barely fast enough for large
        //      scale multiplayer games (in Unity)
        // * or we use dirty bits as meta data about which fields have changed
        //   -> spares us from running delta algorithms
        //   -> still supports dynamically sized types
        //
        // 64 bit mask, tracking up to 64 SyncVars.
        protected ulong syncVarDirtyBits { get; private set; }
        // 64 bit mask, tracking up to 64 sync collections (internal for tests).
        // internal for tests, field for faster access (instead of property)
        // TODO 64 SyncLists are too much. consider smaller mask later.
        internal ulong syncObjectDirtyBits;

        // Weaver replaces '[SyncVar] int health' with 'Networkhealth' property.
        // setter calls the hook if value changed.
        // if we then modify the [SyncVar] from inside the setter,
        // the setter would call the hook and we deadlock.
        // hook guard prevents that.
        ulong syncVarHookGuard;

        // USED BY WEAVER to set syncvars in host mode without deadlocking
        protected bool GetSyncVarHookGuard(ulong dirtyBit) =>
            (syncVarHookGuard & dirtyBit) != 0UL;

        // USED BY WEAVER to set syncvars in host mode without deadlocking
        protected void SetSyncVarHookGuard(ulong dirtyBit, bool value)
        {
            // set the bit
            if (value)
                syncVarHookGuard |= dirtyBit;
            // clear the bit
            else
                syncVarHookGuard &= ~dirtyBit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetSyncObjectDirtyBit(ulong dirtyBit)
        {
            syncObjectDirtyBits |= dirtyBit;
        }

        /// <summary>Set as dirty so that it's synced to clients again.</summary>
        // these are masks, not bit numbers, ie. 110011b not '2' for 2nd bit.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSyncVarDirtyBit(ulong dirtyBit)
        {
            syncVarDirtyBits |= dirtyBit;
        }

        /// <summary>Set as dirty to trigger OnSerialize & send. Dirty bits are cleared after the send.</summary>
        // previously one had to use SetSyncVarDirtyBit(1), which is confusing.
        // simply reuse SetSyncVarDirtyBit for now.
        // instead of adding another field.
        // syncVarDirtyBits does trigger OnSerialize as well.
        //
        // it's important to set _all_ bits as dirty.
        // for example, server needs to broadcast ClientToServer components.
        // if we only set the first bit, only that SyncVar would be broadcast.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDirty() => SetSyncVarDirtyBit(ulong.MaxValue);

        // true if syncInterval elapsed and any SyncVar or SyncObject is dirty
        // OR both bitmasks. != 0 if either was dirty.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDirty() =>
            // check bits first. this is basically free.
            (syncVarDirtyBits | syncObjectDirtyBits) != 0UL &&
            // only check time if bits were dirty. this is more expensive.
            NetworkTime.localTime - lastSyncTime >= syncInterval;

        /// <summary>Clears all the dirty bits that were set by SetDirtyBits()</summary>
        // automatically invoked when an update is sent for this object, but can
        // be called manually as well.
        public void ClearAllDirtyBits()
        {
            lastSyncTime = NetworkTime.localTime;
            syncVarDirtyBits = 0L;
            syncObjectDirtyBits = 0L;

            // clear all unsynchronized changes in syncobjects
            // (Linq allocates, use for instead)
            for (int i = 0; i < syncObjects.Count; ++i)
            {
                syncObjects[i].ClearChanges();
            }
        }

        // this gets called in the constructor by the weaver
        // for every SyncObject in the component (e.g. SyncLists).
        // We collect all of them and we synchronize them with OnSerialize/OnDeserialize
        protected void InitSyncObject(SyncObject syncObject)
        {
            if (syncObject == null)
            {
                Debug.LogError("Uninitialized SyncObject. Manually call the constructor on your SyncList, SyncSet, SyncDictionary or SyncField<T>");
                return;
            }

            // add it, remember the index in list (if Count=0, index=0 etc.)
            int index = syncObjects.Count;
            syncObjects.Add(syncObject);

            // OnDirty needs to set nth bit in our dirty mask
            ulong nthBit = 1UL << index;
            syncObject.OnDirty = () => SetSyncObjectDirtyBit(nthBit);

            // who is allowed to modify SyncList/SyncSet/etc.:
            //  on client: only if owned ClientToserver
            //  on server: only if ServerToClient.
            //             but also for initial state when spawning.
            // need to set a lambda because 'isClient' isn't available in
            // InitSyncObject yet, which is called from the constructor.
            syncObject.IsWritable = () =>
            {
                // carefully check each mode separately to ensure correct results.
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3342

                // normally we would check isServer / isClient here.
                // users may add to SyncLists before the object was spawned.
                // isServer / isClient would still be false.
                // so we need to check NetworkServer/Client.active here instead.

                // host mode: any ServerToClient and any local client owned
                if (NetworkServer.active && NetworkClient.active)
                    return syncDirection == SyncDirection.ServerToClient || isOwned;

                // server only: any ServerToClient
                if (NetworkServer.active)
                    return syncDirection == SyncDirection.ServerToClient;

                // client only: only ClientToServer and owned
                if (NetworkClient.active)
                {
                    // spawned: only ClientToServer and owned
                    if (netId != 0) return syncDirection == SyncDirection.ClientToServer && isOwned;

                    // not spawned (character selection previews, etc.): always allow
                    // fixes https://github.com/MirrorNetworking/Mirror/issues/3343
                    return true;
                }

                // undefined behaviour should throw to make it very obvious
                throw new Exception("InitSyncObject: IsWritable: neither NetworkServer nor NetworkClient are active.");
            };

            // when do we record changes:
            //  on client: only if owned ClientToServer
            //  on server: only if we have observers.
            //    prevents ever growing .changes lists:
            //      if a monster has no observers but we keep modifing a SyncObject,
            //      then the changes would never be flushed and keep growing,
            //      because OnSerialize isn't called without observers.
            syncObject.IsRecording = () =>
            {
                // carefully check each mode separately to ensure correct results.
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3342

                // host mode: only if observed
                if (isServer && isClient) return netIdentity.observers.Count > 0;

                // server only: only if observed
                if (isServer) return netIdentity.observers.Count > 0;

                // client only: only ClientToServer and owned
                if (isClient) return syncDirection == SyncDirection.ClientToServer && isOwned;

                // users may add to SyncLists before the object was spawned.
                // isServer / isClient would still be false.
                // in that case, allow modifying but don't record changes yet.
                return false;
            };
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        protected void SendCommandInternal(string functionFullName, NetworkWriter writer, int channelId, bool requiresAuthority = true)
        {
            // this was in Weaver before
            // NOTE: we could remove this later to allow calling Cmds on Server
            //       to avoid Wrapper functions. a lot of people requested this.
            if (!NetworkClient.active)
            {
                Debug.LogError($"Command Function {functionFullName} called on {name} without an active client.", gameObject);
                return;
            }

            // previously we used NetworkClient.readyConnection.
            // now we check .ready separately.
            if (!NetworkClient.ready)
            {
                // Unreliable Cmds from NetworkTransform may be generated,
                // or client may have been set NotReady intentionally, so
                // only warn if on the reliable channel.
                if (channelId == Channels.Reliable)
                    Debug.LogWarning($"Command Function {functionFullName} called on {name} while NetworkClient is not ready.\nThis may be ignored if client intentionally set NotReady.", gameObject);
                return;
            }

            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(!requiresAuthority || isLocalPlayer || isOwned))
            {
                Debug.LogWarning($"Command Function {functionFullName} called on {name} without authority.", gameObject);
                return;
            }

            // IMPORTANT: can't use .connectionToServer here because calling
            // a command on other objects is allowed if requireAuthority is
            // false. other objects don't have a .connectionToServer.
            // => so we always need to use NetworkClient.connection instead.
            // => see also: https://github.com/vis2k/Mirror/issues/2629
            if (NetworkClient.connection == null)
            {
                Debug.LogError($"Command Function {functionFullName} called on {name} with no client running.", gameObject);
                return;
            }

            // construct the message
            CommandMessage message = new CommandMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = (ushort)functionFullName.GetStableHashCode(),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            // IMPORTANT: can't use .connectionToServer here because calling
            // a command on other objects is allowed if requireAuthority is
            // false. other objects don't have a .connectionToServer.
            // => so we always need to use NetworkClient.connection instead.
            // => see also: https://github.com/vis2k/Mirror/issues/2629
            // This bypasses the null check in NetworkClient.Send but we have
            // a null check above with a detailed error log.
            NetworkClient.connection.Send(message, channelId);
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        protected void SendRPCInternal(string functionFullName, NetworkWriter writer, int channelId, bool includeOwner)
        {
            // this was in Weaver before
            if (!NetworkServer.active)
            {
                Debug.LogError($"RPC Function {functionFullName} called on Client.", gameObject);
                return;
            }

            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                Debug.LogWarning($"ClientRpc {functionFullName} called on un-spawned object: {name}", gameObject);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = (ushort)functionFullName.GetStableHashCode(),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            // serialize it to each ready observer's connection's rpc buffer.
            // send them all at once, instead of sending one message per rpc.
            // NetworkServer.SendToReadyObservers(netIdentity, message, includeOwner, channelId);

            // safety check used to be in SendToReadyObservers. keep it for now.
            if (netIdentity.observers != null && netIdentity.observers.Count > 0)
            {
                // serialize the message only once
                using (NetworkWriterPooled serialized = NetworkWriterPool.Get())
                {
                    serialized.Write(message);

                    // add to every observer's connection's rpc buffer
                    foreach (NetworkConnectionToClient conn in netIdentity.observers.Values)
                    {
                        bool isOwner = conn == netIdentity.connectionToClient;
                        if ((!isOwner || includeOwner) && conn.isReady)
                        {
                            conn.BufferRpc(message, channelId);
                        }
                    }
                }
            }
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        protected void SendTargetRPCInternal(NetworkConnection conn, string functionFullName, NetworkWriter writer, int channelId)
        {
            if (!NetworkServer.active)
            {
                Debug.LogError($"TargetRPC {functionFullName} was called on {name} when server not active.", gameObject);
                return;
            }

            if (!isServer)
            {
                Debug.LogWarning($"TargetRpc {functionFullName} called on {name} but that object has not been spawned or has been unspawned.", gameObject);
                return;
            }

            // connection parameter is optional. assign if null.
            if (conn is null)
            {
                conn = connectionToClient;
            }

            // if still null
            if (conn is null)
            {
                Debug.LogError($"TargetRPC {functionFullName} can't be sent because it was given a null connection. Make sure {name} is owned by a connection, or if you pass a connection manually then make sure it's not null. For example, TargetRpcs can be called on Player/Pet which are owned by a connection. However, they can not be called on Monsters/Npcs which don't have an owner connection.", gameObject);
                return;
            }

            // TODO change conn type to NetworkConnectionToClient to begin with.
            if (!(conn is NetworkConnectionToClient connToClient))
            {
                Debug.LogError($"TargetRPC {functionFullName} called on {name} requires a NetworkConnectionToClient but was given {conn.GetType().Name}", gameObject);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = (ushort)functionFullName.GetStableHashCode(),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            // serialize it to the connection's rpc buffer.
            // send them all at once, instead of sending one message per rpc.
            // conn.Send(message, channelId);
            connToClient.BufferRpc(message, channelId);
        }

        // move the [SyncVar] generated property's .set into C# to avoid much IL
        //
        //   public int health = 42;
        //
        //   public int Networkhealth
        //   {
        //       get
        //       {
        //           return health;
        //       }
        //       [param: In]
        //       set
        //       {
        //           if (!NetworkBehaviour.SyncVarEqual(value, ref health))
        //           {
        //               int oldValue = health;
        //               SetSyncVar(value, ref health, 1uL);
        //               if (NetworkServer.activeHost && !GetSyncVarHookGuard(1uL))
        //               {
        //                   SetSyncVarHookGuard(1uL, value: true);
        //                   OnChanged(oldValue, value);
        //                   SetSyncVarHookGuard(1uL, value: false);
        //               }
        //           }
        //       }
        //   }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarSetter<T>(T value, ref T field, ulong dirtyBit, Action<T, T> OnChanged)
        {
            if (!SyncVarEqual(value, ref field))
            {
                T oldValue = field;
                SetSyncVar(value, ref field, dirtyBit);

                // call hook (if any)
                if (OnChanged != null)
                {
                    // in host mode, setting a SyncVar calls the hook directly.
                    // in client-only mode, OnDeserialize would call it.
                    // we use hook guard to protect against deadlock where hook
                    // changes syncvar, calling hook again.
                    if (NetworkServer.activeHost && !GetSyncVarHookGuard(dirtyBit))
                    {
                        SetSyncVarHookGuard(dirtyBit, true);
                        OnChanged(oldValue, value);
                        SetSyncVarHookGuard(dirtyBit, false);
                    }
                }
            }
        }

        // GameObject needs custom handling for persistence via netId.
        // has one extra parameter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarSetter_GameObject(GameObject value, ref GameObject field, ulong dirtyBit, Action<GameObject, GameObject> OnChanged, ref uint netIdField)
        {
            if (!SyncVarGameObjectEqual(value, netIdField))
            {
                GameObject oldValue = field;
                SetSyncVarGameObject(value, ref field, dirtyBit, ref netIdField);

                // call hook (if any)
                if (OnChanged != null)
                {
                    // in host mode, setting a SyncVar calls the hook directly.
                    // in client-only mode, OnDeserialize would call it.
                    // we use hook guard to protect against deadlock where hook
                    // changes syncvar, calling hook again.
                    if (NetworkServer.activeHost && !GetSyncVarHookGuard(dirtyBit))
                    {
                        SetSyncVarHookGuard(dirtyBit, true);
                        OnChanged(oldValue, value);
                        SetSyncVarHookGuard(dirtyBit, false);
                    }
                }
            }
        }

        // NetworkIdentity needs custom handling for persistence via netId.
        // has one extra parameter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarSetter_NetworkIdentity(NetworkIdentity value, ref NetworkIdentity field, ulong dirtyBit, Action<NetworkIdentity, NetworkIdentity> OnChanged, ref uint netIdField)
        {
            if (!SyncVarNetworkIdentityEqual(value, netIdField))
            {
                NetworkIdentity oldValue = field;
                SetSyncVarNetworkIdentity(value, ref field, dirtyBit, ref netIdField);

                // call hook (if any)
                if (OnChanged != null)
                {
                    // in host mode, setting a SyncVar calls the hook directly.
                    // in client-only mode, OnDeserialize would call it.
                    // we use hook guard to protect against deadlock where hook
                    // changes syncvar, calling hook again.
                    if (NetworkServer.activeHost && !GetSyncVarHookGuard(dirtyBit))
                    {
                        SetSyncVarHookGuard(dirtyBit, true);
                        OnChanged(oldValue, value);
                        SetSyncVarHookGuard(dirtyBit, false);
                    }
                }
            }
        }

        // NetworkBehaviour needs custom handling for persistence via netId.
        // has one extra parameter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarSetter_NetworkBehaviour<T>(T value, ref T field, ulong dirtyBit, Action<T, T> OnChanged, ref NetworkBehaviourSyncVar netIdField)
            where T : NetworkBehaviour
        {
            if (!SyncVarNetworkBehaviourEqual(value, netIdField))
            {
                T oldValue = field;
                SetSyncVarNetworkBehaviour(value, ref field, dirtyBit, ref netIdField);

                // call hook (if any)
                if (OnChanged != null)
                {
                    // in host mode, setting a SyncVar calls the hook directly.
                    // in client-only mode, OnDeserialize would call it.
                    // we use hook guard to protect against deadlock where hook
                    // changes syncvar, calling hook again.
                    if (NetworkServer.activeHost && !GetSyncVarHookGuard(dirtyBit))
                    {
                        SetSyncVarHookGuard(dirtyBit, true);
                        OnChanged(oldValue, value);
                        SetSyncVarHookGuard(dirtyBit, false);
                    }
                }
            }
        }

        // helper function for [SyncVar] GameObjects.
        // needs to be public so that tests & NetworkBehaviours from other
        // assemblies both find it
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool SyncVarGameObjectEqual(GameObject newGameObject, uint netIdField)
        {
            uint newNetId = 0;
            if (newGameObject != null)
            {
                if (newGameObject.TryGetComponent(out NetworkIdentity identity))
                {
                    newNetId = identity.netId;
                    if (newNetId == 0)
                    {
                        Debug.LogWarning($"SetSyncVarGameObject GameObject {newGameObject} has a zero netId. Maybe it is not spawned yet?");
                    }
                }
            }

            return newNetId == netIdField;
        }

        // helper function for [SyncVar] GameObjects.
        // dirtyBit is a mask like 00010
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref uint netIdField)
        {
            if (GetSyncVarHookGuard(dirtyBit))
                return;

            uint newNetId = 0;
            if (newGameObject != null)
            {
                if (newGameObject.TryGetComponent(out NetworkIdentity identity))
                {
                    newNetId = identity.netId;
                    if (newNetId == 0)
                    {
                        Debug.LogWarning($"SetSyncVarGameObject GameObject {newGameObject} has a zero netId. Maybe it is not spawned yet?");
                    }
                }
            }

            //Debug.Log($"SetSyncVar GameObject {GetType().Name} bit:{dirtyBit} netfieldId:{netIdField} -> {newNetId}");
            SetSyncVarDirtyBit(dirtyBit);
            // assign new one on the server, and in case we ever need it on client too
            gameObjectField = newGameObject;
            netIdField = newNetId;
        }

        // helper function for [SyncVar] GameObjects.
        // -> ref GameObject as second argument makes OnDeserialize processing easier
        protected GameObject GetSyncVarGameObject(uint netId, ref GameObject gameObjectField)
        {
            // server always uses the field
            if (isServer)
            {
                return gameObjectField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                return gameObjectField = identity.gameObject;
            return null;
        }

        // helper function for [SyncVar] NetworkIdentities.
        // needs to be public so that tests & NetworkBehaviours from other
        // assemblies both find it
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool SyncVarNetworkIdentityEqual(NetworkIdentity newIdentity, uint netIdField)
        {
            uint newNetId = 0;
            if (newIdentity != null)
            {
                newNetId = newIdentity.netId;
                if (newNetId == 0)
                {
                    Debug.LogWarning($"SetSyncVarNetworkIdentity NetworkIdentity {newIdentity} has a zero netId. Maybe it is not spawned yet?");
                }
            }

            // netId changed?
            return newNetId == netIdField;
        }

        // move the [SyncVar] generated OnDeserialize C# to avoid much IL.
        //
        // before:
        //  public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //  {
        //      base.DeserializeSyncVars(reader, initialState);
        //      if (initialState)
        //      {
        //          int num = health;
        //          Networkhealth = reader.ReadInt();
        //          if (!NetworkBehaviour.SyncVarEqual(num, ref health))
        //          {
        //              OnChanged(num, health);
        //          }
        //          return;
        //      }
        //      long num2 = (long)reader.ReadULong();
        //      if ((num2 & 1L) != 0L)
        //      {
        //          int num3 = health;
        //          Networkhealth = reader.ReadInt();
        //          if (!NetworkBehaviour.SyncVarEqual(num3, ref health))
        //          {
        //              OnChanged(num3, health);
        //          }
        //      }
        //  }
        //
        // after:
        //
        //  public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //  {
        //      base.DeserializeSyncVars(reader, initialState);
        //      if (initialState)
        //      {
        //          GeneratedSyncVarDeserialize(reader, ref health, null, reader.ReadInt());
        //          return;
        //      }
        //      long num = (long)reader.ReadULong();
        //      if ((num & 1L) != 0L)
        //      {
        //          GeneratedSyncVarDeserialize(reader, ref health, null, reader.ReadInt());
        //      }
        //  }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarDeserialize<T>(ref T field, Action<T, T> OnChanged, T value)
        {
            T previous = field;
            field = value;

            // any hook? then call if changed.
            if (OnChanged != null && !SyncVarEqual(previous, ref field))
            {
                OnChanged(previous, field);
            }
        }

        // move the [SyncVar] generated OnDeserialize C# to avoid much IL.
        //
        // before:
        //   public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //   {
        //       base.DeserializeSyncVars(reader, initialState);
        //       if (initialState)
        //       {
        //           uint __targetNetId = ___targetNetId;
        //           GameObject networktarget = Networktarget;
        //           ___targetNetId = reader.ReadUInt();
        //           if (!NetworkBehaviour.SyncVarEqual(__targetNetId, ref ___targetNetId))
        //           {
        //               OnChangedNB(networktarget, Networktarget);
        //           }
        //           return;
        //       }
        //       long num = (long)reader.ReadULong();
        //       if ((num & 1L) != 0L)
        //       {
        //           uint __targetNetId2 = ___targetNetId;
        //           GameObject networktarget2 = Networktarget;
        //           ___targetNetId = reader.ReadUInt();
        //           if (!NetworkBehaviour.SyncVarEqual(__targetNetId2, ref ___targetNetId))
        //           {
        //               OnChangedNB(networktarget2, Networktarget);
        //           }
        //       }
        //   }
        //
        // after:
        //   public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //   {
        //       base.DeserializeSyncVars(reader, initialState);
        //       if (initialState)
        //       {
        //           GeneratedSyncVarDeserialize_GameObject(reader, ref target, OnChangedNB, ref ___targetNetId);
        //           return;
        //       }
        //       long num = (long)reader.ReadULong();
        //       if ((num & 1L) != 0L)
        //       {
        //           GeneratedSyncVarDeserialize_GameObject(reader, ref target, OnChangedNB, ref ___targetNetId);
        //       }
        //   }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarDeserialize_GameObject(ref GameObject field, Action<GameObject, GameObject> OnChanged, NetworkReader reader, ref uint netIdField)
        {
            uint previousNetId = netIdField;
            GameObject previousGameObject = field;
            netIdField = reader.ReadUInt();

            // get the new GameObject now that netId field is set
            field = GetSyncVarGameObject(netIdField, ref field);

            // any hook? then call if changed.
            if (OnChanged != null && !SyncVarEqual(previousNetId, ref netIdField))
            {
                OnChanged(previousGameObject, field);
            }
        }

        // move the [SyncVar] generated OnDeserialize C# to avoid much IL.
        //
        // before:
        //   public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //   {
        //       base.DeserializeSyncVars(reader, initialState);
        //       if (initialState)
        //       {
        //           uint __targetNetId = ___targetNetId;
        //           NetworkIdentity networktarget = Networktarget;
        //           ___targetNetId = reader.ReadUInt();
        //           if (!NetworkBehaviour.SyncVarEqual(__targetNetId, ref ___targetNetId))
        //           {
        //               OnChangedNI(networktarget, Networktarget);
        //           }
        //           return;
        //       }
        //       long num = (long)reader.ReadULong();
        //       if ((num & 1L) != 0L)
        //       {
        //           uint __targetNetId2 = ___targetNetId;
        //           NetworkIdentity networktarget2 = Networktarget;
        //           ___targetNetId = reader.ReadUInt();
        //           if (!NetworkBehaviour.SyncVarEqual(__targetNetId2, ref ___targetNetId))
        //           {
        //               OnChangedNI(networktarget2, Networktarget);
        //           }
        //       }
        //   }
        //
        // after:
        //
        //   public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //   {
        //       base.DeserializeSyncVars(reader, initialState);
        //       if (initialState)
        //       {
        //           GeneratedSyncVarDeserialize_NetworkIdentity(reader, ref target, OnChangedNI, ref ___targetNetId);
        //           return;
        //       }
        //       long num = (long)reader.ReadULong();
        //       if ((num & 1L) != 0L)
        //       {
        //           GeneratedSyncVarDeserialize_NetworkIdentity(reader, ref target, OnChangedNI, ref ___targetNetId);
        //       }
        //   }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarDeserialize_NetworkIdentity(ref NetworkIdentity field, Action<NetworkIdentity, NetworkIdentity> OnChanged, NetworkReader reader, ref uint netIdField)
        {
            uint previousNetId = netIdField;
            NetworkIdentity previousIdentity = field;
            netIdField = reader.ReadUInt();

            // get the new NetworkIdentity now that netId field is set
            field = GetSyncVarNetworkIdentity(netIdField, ref field);

            // any hook? then call if changed.
            if (OnChanged != null && !SyncVarEqual(previousNetId, ref netIdField))
            {
                OnChanged(previousIdentity, field);
            }
        }

        // move the [SyncVar] generated OnDeserialize C# to avoid much IL.
        //
        // before:
        //
        //   public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //   {
        //       base.DeserializeSyncVars(reader, initialState);
        //       if (initialState)
        //       {
        //           NetworkBehaviourSyncVar __targetNetId = ___targetNetId;
        //           Tank networktarget = Networktarget;
        //           ___targetNetId = reader.ReadNetworkBehaviourSyncVar();
        //           if (!NetworkBehaviour.SyncVarEqual(__targetNetId, ref ___targetNetId))
        //           {
        //               OnChangedNB(networktarget, Networktarget);
        //           }
        //           return;
        //       }
        //       long num = (long)reader.ReadULong();
        //       if ((num & 1L) != 0L)
        //       {
        //           NetworkBehaviourSyncVar __targetNetId2 = ___targetNetId;
        //           Tank networktarget2 = Networktarget;
        //           ___targetNetId = reader.ReadNetworkBehaviourSyncVar();
        //           if (!NetworkBehaviour.SyncVarEqual(__targetNetId2, ref ___targetNetId))
        //           {
        //               OnChangedNB(networktarget2, Networktarget);
        //           }
        //       }
        //   }
        //
        // after:
        //
        //   public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
        //   {
        //       base.DeserializeSyncVars(reader, initialState);
        //       if (initialState)
        //       {
        //           GeneratedSyncVarDeserialize_NetworkBehaviour(reader, ref target, OnChangedNB, ref ___targetNetId);
        //           return;
        //       }
        //       long num = (long)reader.ReadULong();
        //       if ((num & 1L) != 0L)
        //       {
        //           GeneratedSyncVarDeserialize_NetworkBehaviour(reader, ref target, OnChangedNB, ref ___targetNetId);
        //       }
        //   }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GeneratedSyncVarDeserialize_NetworkBehaviour<T>(ref T field, Action<T, T> OnChanged, NetworkReader reader, ref NetworkBehaviourSyncVar netIdField)
            where T : NetworkBehaviour
        {
            NetworkBehaviourSyncVar previousNetId = netIdField;
            T previousBehaviour = field;
            netIdField = reader.ReadNetworkBehaviourSyncVar();

            // get the new NetworkBehaviour now that netId field is set
            field = GetSyncVarNetworkBehaviour(netIdField, ref field);

            // any hook? then call if changed.
            if (OnChanged != null && !SyncVarEqual(previousNetId, ref netIdField))
            {
                OnChanged(previousBehaviour, field);
            }
        }

        // helper function for [SyncVar] NetworkIdentities.
        // dirtyBit is a mask like 00010
        protected void SetSyncVarNetworkIdentity(NetworkIdentity newIdentity, ref NetworkIdentity identityField, ulong dirtyBit, ref uint netIdField)
        {
            if (GetSyncVarHookGuard(dirtyBit))
                return;

            uint newNetId = 0;
            if (newIdentity != null)
            {
                newNetId = newIdentity.netId;
                if (newNetId == 0)
                {
                    Debug.LogWarning($"SetSyncVarNetworkIdentity NetworkIdentity {newIdentity} has a zero netId. Maybe it is not spawned yet?");
                }
            }

            //Debug.Log($"SetSyncVarNetworkIdentity NetworkIdentity {GetType().Name} bit:{dirtyBit} netIdField:{netIdField} -> {newNetId}");
            SetSyncVarDirtyBit(dirtyBit);
            netIdField = newNetId;
            // assign new one on the server, and in case we ever need it on client too
            identityField = newIdentity;
        }

        // helper function for [SyncVar] NetworkIdentities.
        // -> ref GameObject as second argument makes OnDeserialize processing easier
        protected NetworkIdentity GetSyncVarNetworkIdentity(uint netId, ref NetworkIdentity identityField)
        {
            // server always uses the field
            if (isServer)
            {
                return identityField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            NetworkClient.spawned.TryGetValue(netId, out identityField);
            return identityField;
        }

        protected static bool SyncVarNetworkBehaviourEqual<T>(T newBehaviour, NetworkBehaviourSyncVar syncField) where T : NetworkBehaviour
        {
            uint newNetId = 0;
            byte newComponentIndex = 0;
            if (newBehaviour != null)
            {
                newNetId = newBehaviour.netId;
                newComponentIndex = newBehaviour.ComponentIndex;
                if (newNetId == 0)
                {
                    Debug.LogWarning($"SetSyncVarNetworkIdentity NetworkIdentity {newBehaviour} has a zero netId. Maybe it is not spawned yet?");
                }
            }

            // netId changed?
            return syncField.Equals(newNetId, newComponentIndex);
        }

        // helper function for [SyncVar] NetworkIdentities.
        // dirtyBit is a mask like 00010
        protected void SetSyncVarNetworkBehaviour<T>(T newBehaviour, ref T behaviourField, ulong dirtyBit, ref NetworkBehaviourSyncVar syncField) where T : NetworkBehaviour
        {
            if (GetSyncVarHookGuard(dirtyBit))
                return;

            uint newNetId = 0;
            byte componentIndex = 0;
            if (newBehaviour != null)
            {
                newNetId = newBehaviour.netId;
                componentIndex = newBehaviour.ComponentIndex;
                if (newNetId == 0)
                {
                    Debug.LogWarning($"{nameof(SetSyncVarNetworkBehaviour)} NetworkIdentity {newBehaviour} has a zero netId. Maybe it is not spawned yet?");
                }
            }

            syncField = new NetworkBehaviourSyncVar(newNetId, componentIndex);

            SetSyncVarDirtyBit(dirtyBit);

            // assign new one on the server, and in case we ever need it on client too
            behaviourField = newBehaviour;

            // Debug.Log($"SetSyncVarNetworkBehaviour NetworkIdentity {GetType().Name} bit [{dirtyBit}] netIdField:{oldField}->{syncField}");
        }

        // helper function for [SyncVar] NetworkIdentities.
        // -> ref GameObject as second argument makes OnDeserialize processing easier
        protected T GetSyncVarNetworkBehaviour<T>(NetworkBehaviourSyncVar syncNetBehaviour, ref T behaviourField) where T : NetworkBehaviour
        {
            // server always uses the field
            if (isServer)
            {
                return behaviourField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            if (!NetworkClient.spawned.TryGetValue(syncNetBehaviour.netId, out NetworkIdentity identity))
            {
                return null;
            }

            behaviourField = identity.NetworkBehaviours[syncNetBehaviour.componentIndex] as T;
            return behaviourField;
        }

        protected static bool SyncVarEqual<T>(T value, ref T fieldValue)
        {
            // newly initialized or changed value?
            // value.Equals(fieldValue) allocates without 'where T : IEquatable'
            // seems like we use EqualityComparer to avoid allocations,
            // because not all SyncVars<T> are IEquatable
            return EqualityComparer<T>.Default.Equals(value, fieldValue);
        }

        // dirtyBit is a mask like 00010
        protected void SetSyncVar<T>(T value, ref T fieldValue, ulong dirtyBit)
        {
            //Debug.Log($"SetSyncVar {GetType().Name} bit:{dirtyBit} fieldValue:{value}");
            SetSyncVarDirtyBit(dirtyBit);
            fieldValue = value;
        }

        /// <summary>Override to do custom serialization (instead of SyncVars/SyncLists). Use OnDeserialize too.</summary>
        // if a class has syncvars, then OnSerialize/OnDeserialize are added
        // automatically.
        //
        // initialState is true for full spawns, false for delta syncs.
        //   note: SyncVar hooks are only called when inital=false
        public virtual void OnSerialize(NetworkWriter writer, bool initialState)
        {
            SerializeSyncObjects(writer, initialState);
            SerializeSyncVars(writer, initialState);
        }

        /// <summary>Override to do custom deserialization (instead of SyncVars/SyncLists). Use OnSerialize too.</summary>
        public virtual void OnDeserialize(NetworkReader reader, bool initialState)
        {
            DeserializeSyncObjects(reader, initialState);
            DeserializeSyncVars(reader, initialState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SerializeSyncObjects(NetworkWriter writer, bool initialState)
        {
            // if initialState: write all SyncVars.
            // otherwise write dirtyBits+dirty SyncVars
            if (initialState)
                SerializeObjectsAll(writer);
            else
                SerializeObjectsDelta(writer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DeserializeSyncObjects(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                DeserializeObjectsAll(reader);
            }
            else
            {
                DeserializeObjectsDelta(reader);
            }
        }

        // USED BY WEAVER
        protected virtual void SerializeSyncVars(NetworkWriter writer, bool initialState)
        {
            // SyncVar are written here in subclass

            // if initialState
            //   write all SyncVars
            // else
            //   write syncVarDirtyBits
            //   write dirty SyncVars
        }

        // USED BY WEAVER
        protected virtual void DeserializeSyncVars(NetworkReader reader, bool initialState)
        {
            // SyncVars are read here in subclass

            // if initialState
            //   read all SyncVars
            // else
            //   read syncVarDirtyBits
            //   read dirty SyncVars
        }

        public void SerializeObjectsAll(NetworkWriter writer)
        {
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                syncObject.OnSerializeAll(writer);
            }
        }

        public void SerializeObjectsDelta(NetworkWriter writer)
        {
            // write the mask
            writer.WriteULong(syncObjectDirtyBits);

            // serializable objects, such as synclists
            for (int i = 0; i < syncObjects.Count; i++)
            {
                // check dirty mask at nth bit
                SyncObject syncObject = syncObjects[i];
                if ((syncObjectDirtyBits & (1UL << i)) != 0)
                {
                    syncObject.OnSerializeDelta(writer);
                }
            }
        }

        internal void DeserializeObjectsAll(NetworkReader reader)
        {
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                syncObject.OnDeserializeAll(reader);
            }
        }

        internal void DeserializeObjectsDelta(NetworkReader reader)
        {
            ulong dirty = reader.ReadULong();
            for (int i = 0; i < syncObjects.Count; i++)
            {
                // check dirty mask at nth bit
                SyncObject syncObject = syncObjects[i];
                if ((dirty & (1UL << i)) != 0)
                {
                    syncObject.OnDeserializeDelta(reader);
                }
            }
        }

        // safely serialize each component in a way that one reading too much or
        // too few bytes will show obvious, easy to resolve error messages.
        //
        // prevents the original UNET bug which started Mirror:
        // https://github.com/vis2k/Mirror/issues/2617
        // where one component would read too much, and then all following reads
        // on other entities would be mismatched, causing the weirdest errors.
        //
        // reads <<len, payload, len, payload, ...>> for 100% safety.
        internal void Serialize(NetworkWriter writer, bool initialState)
        {
            // reserve length header to ensure the correct amount will be read.
            // originally we used a 4 byte header (too bandwidth heavy).
            // instead, let's "& 0xFF" the size.
            //
            // this is cleaner than barriers at the end of payload, because:
            // - ensures the correct safety is read _before_ payload.
            // - it's quite hard to break the check.
            //   a component would need to read/write the intented amount
            //   multiplied by 255 in order to miss the check.
            //   with barriers, reading 1 byte too much may still succeed if the
            //   next component's first byte matches the expected barrier.
            // - we can still attempt to correct the invalid position via the
            //   safety length byte (we know that one is correct).
            //
            // it's just overall cleaner, and still low on bandwidth.

            // write placeholder length byte
            // (jumping back later is WAY faster than allocating a temporary
            //  writer for the payload, then writing payload.size, payload)
            int headerPosition = writer.Position;
            writer.WriteByte(0);
            int contentPosition = writer.Position;

            // write payload
            try
            {
                // note this may not write anything if no syncIntervals elapsed
                OnSerialize(writer, initialState);
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError($"OnSerialize failed for: object={name} component={GetType()} sceneId={netIdentity.sceneId:X}\n\n{e}");
            }
            int endPosition = writer.Position;

            // fill in length hash as the last byte of the 4 byte length
            writer.Position = headerPosition;
            int size = endPosition - contentPosition;
            byte safety = (byte)(size & 0xFF);
            writer.WriteByte(safety);
            writer.Position = endPosition;

            //Debug.Log($"OnSerializeSafely written for object {name} component:{GetType()} sceneId:{sceneId:X} header:{headerPosition} content:{contentPosition} end:{endPosition} contentSize:{endPosition - contentPosition}");
        }

        // correct the read size with the 1 byte length hash (by mischa).
        // -> the component most likely read a few too many/few bytes.
        // -> we know the correct last byte of the expected size (=the safety).
        // -> attempt to reconstruct the size via safety byte.
        //    it will be correct unless someone wrote way way too much,
        //    as in > 255 bytes worth too much.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ErrorCorrection(int size, byte safety)
        {
            // clear the last byte which most likely contains the error
            uint cleared = (uint)size & 0xFFFFFF00;

            // insert the safety which we know to be correct
            return (int)(cleared | safety);
        }

        // returns false in case of errors.
        // server needs to know in order to disconnect on error.
        internal bool Deserialize(NetworkReader reader, bool initialState)
        {
            // detect errors, but attempt to correct before returning
            bool result = true;

            // read 1 byte length hash safety & capture beginning for size check
            byte safety = reader.ReadByte();
            int chunkStart = reader.Position;

            // call OnDeserialize and wrap it in a try-catch block so there's no
            // way to mess up another component's deserialization
            try
            {
                //Debug.Log($"OnDeserializeSafely: {name} component:{GetType()} sceneId:{sceneId:X} length:{contentSize}");
                OnDeserialize(reader, initialState);
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError($"OnDeserialize failed Exception={e.GetType()} (see below) object={name} component={GetType()} netId={netId}. Possible Reasons:\n" +
                               $"  * Do {GetType()}'s OnSerialize and OnDeserialize calls write the same amount of data? \n" +
                               $"  * Was there an exception in {GetType()}'s OnSerialize/OnDeserialize code?\n" +
                               $"  * Are the server and client the exact same project?\n" +
                               $"  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n" +
                               $"Exception {e}");
                result = false;
            }

            // compare bytes read with length hash
            int size = reader.Position - chunkStart;
            byte sizeHash = (byte)(size & 0xFF);
            if (sizeHash != safety)
            {
                // warn the user.
                Debug.LogWarning($"{name} (netId={netId}): {GetType()} OnDeserialize size mismatch. It read {size} bytes, which caused a size hash mismatch of {sizeHash:X2} vs. {safety:X2}. Make sure that OnSerialize and OnDeserialize write/read the same amount of data in all cases.");

                // attempt to fix the position, so the following components
                // don't all fail. this is very likely to work, unless the user
                // read more than 255 bytes too many / too few.
                //
                // see test: SerializationSizeMismatch.
                int correctedSize = ErrorCorrection(size, safety);
                reader.Position = chunkStart + correctedSize;
                result = false;
            }

            return result;
        }

        internal void ResetSyncObjects()
        {
            foreach (SyncObject syncObject in syncObjects)
            {
                syncObject.Reset();
            }
        }

        /// <summary>Like Start(), but only called on server and host.</summary>
        public virtual void OnStartServer() {}

        /// <summary>Stop event, only called on server and host.</summary>
        public virtual void OnStopServer() {}

        /// <summary>Like Start(), but only called on client and host.</summary>
        public virtual void OnStartClient() {}

        /// <summary>Stop event, only called on client and host.</summary>
        public virtual void OnStopClient() {}

        /// <summary>Like Start(), but only called on client and host for the local player object.</summary>
        public virtual void OnStartLocalPlayer() {}

        /// <summary>Stop event, but only called on client and host for the local player object.</summary>
        public virtual void OnStopLocalPlayer() {}

        /// <summary>Like Start(), but only called for objects the client has authority over.</summary>
        public virtual void OnStartAuthority() {}

        /// <summary>Stop event, only called for objects the client has authority over.</summary>
        public virtual void OnStopAuthority() {}
    }
}
