using System;
using System.Collections.Generic;
using System.ComponentModel;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    public enum SyncMode { Observers, Owner }

    /// <summary>Base class for networked components.</summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/networkbehaviour")]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
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

        /// <summary>This returns true if this object is the authoritative version of the object in the distributed network application.</summary>
        // keeping this ridiculous summary as a reminder of a time long gone...
        public bool hasAuthority => netIdentity.hasAuthority;

        /// <summary>The unique network Id of this object (unique at runtime).</summary>
        public uint netId => netIdentity.netId;

        /// <summary>Client's network connection to the server. This is only valid for player objects on the client.</summary>
        public NetworkConnection connectionToServer => netIdentity.connectionToServer;

        /// <summary>Server's network connection to the client. This is only valid for player objects on the server.</summary>
        public NetworkConnection connectionToClient => netIdentity.connectionToClient;

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
        public int ComponentIndex { get; internal set; }

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

        // Deprecated 2021-09-16 (old weavers used it)
        [Obsolete("Renamed to GetSyncVarHookGuard (uppercase)")]
        protected bool getSyncVarHookGuard(ulong dirtyBit) => GetSyncVarHookGuard(dirtyBit);

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

        // Deprecated 2021-09-16 (old weavers used it)
        [Obsolete("Renamed to SetSyncVarHookGuard (uppercase)")]
        protected void setSyncVarHookGuard(ulong dirtyBit, bool value) => SetSyncVarHookGuard(dirtyBit, value);

        /// <summary>Set as dirty so that it's synced to clients again.</summary>
        // these are masks, not bit numbers, ie. 110011b not '2' for 2nd bit.
        public void SetSyncVarDirtyBit(ulong dirtyBit)
        {
            syncVarDirtyBits |= dirtyBit;
        }

        // Deprecated 2021-09-19
        [Obsolete("SetDirtyBit was renamed to SetSyncVarDirtyBit because that's what it does")]
        public void SetDirtyBit(ulong dirtyBit) => SetSyncVarDirtyBit(dirtyBit);

        // true if syncInterval elapsed and any SyncVar or SyncObject is dirty
        public bool IsDirty()
        {
            if (NetworkTime.localTime - lastSyncTime >= syncInterval)
            {
                // OR both bitmasks. != 0 if either was dirty.
                return (syncVarDirtyBits | syncObjectDirtyBits) != 0UL;
            }
            return false;
        }

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
                Debug.LogError("Uninitialized SyncObject. Manually call the constructor on your SyncList, SyncSet or SyncDictionary");
                return;
            }

            // add it, remember the index in list (if Count=0, index=0 etc.)
            int index = syncObjects.Count;
            syncObjects.Add(syncObject);

            // OnDirty needs to set nth bit in our dirty mask
            ulong nthBit = 1UL << index;
            syncObject.OnDirty = () => syncObjectDirtyBits |= nthBit;

            // only record changes while we have observers.
            // prevents ever growing .changes lists:
            //   if a monster has no observers but we keep modifing a SyncObject,
            //   then the changes would never be flushed and keep growing,
            //   because OnSerialize isn't called without observers.
            syncObject.IsRecording = () => netIdentity.observers?.Count > 0;
        }

        protected void SendCommandInternal(Type invokeClass, string cmdName, NetworkWriter writer, int channelId, bool requiresAuthority = true)
        {
            // this was in Weaver before
            // NOTE: we could remove this later to allow calling Cmds on Server
            //       to avoid Wrapper functions. a lot of people requested this.
            if (!NetworkClient.active)
            {
                Debug.LogError($"Command Function {cmdName} called without an active client.");
                return;
            }

            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(!requiresAuthority || isLocalPlayer || hasAuthority))
            {
                Debug.LogWarning($"Trying to send command for object without authority. {invokeClass}.{cmdName}");
                return;
            }

            // previously we used NetworkClient.readyConnection.
            // now we check .ready separately and use .connection instead.
            if (!NetworkClient.ready)
            {
                Debug.LogError("Send command attempted while NetworkClient is not ready.");
                return;
            }

            // IMPORTANT: can't use .connectionToServer here because calling
            // a command on other objects is allowed if requireAuthority is
            // false. other objects don't have a .connectionToServer.
            // => so we always need to use NetworkClient.connection instead.
            // => see also: https://github.com/vis2k/Mirror/issues/2629
            if (NetworkClient.connection == null)
            {
                Debug.LogError("Send command attempted with no client running.");
                return;
            }

            // construct the message
            CommandMessage message = new CommandMessage
            {
                netId = netId,
                componentIndex = (byte)ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, cmdName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            // IMPORTANT: can't use .connectionToServer here because calling
            // a command on other objects is allowed if requireAuthority is
            // false. other objects don't have a .connectionToServer.
            // => so we always need to use NetworkClient.connection instead.
            // => see also: https://github.com/vis2k/Mirror/issues/2629
            NetworkClient.connection.Send(message, channelId);
        }

        protected void SendRPCInternal(Type invokeClass, string rpcName, NetworkWriter writer, int channelId, bool includeOwner)
        {
            // this was in Weaver before
            if (!NetworkServer.active)
            {
                Debug.LogError($"RPC Function {rpcName} called on Client.");
                return;
            }

            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                Debug.LogWarning($"ClientRpc {rpcName} called on un-spawned object: {name}");
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = (byte)ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            NetworkServer.SendToReadyObservers(netIdentity, message, includeOwner, channelId);
        }

        protected void SendTargetRPCInternal(NetworkConnection conn, Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            if (!NetworkServer.active)
            {
                Debug.LogError($"TargetRPC {rpcName} called when server not active");
                return;
            }

            if (!isServer)
            {
                Debug.LogWarning($"TargetRpc {rpcName} called on {name} but that object has not been spawned or has been unspawned");
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
                Debug.LogError($"TargetRPC {rpcName} was given a null connection, make sure the object has an owner or you pass in the target connection");
                return;
            }

            if (!(conn is NetworkConnectionToClient))
            {
                Debug.LogError($"TargetRPC {rpcName} requires a NetworkConnectionToClient but was given {conn.GetType().Name}");
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = (byte)ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            conn.Send(message, channelId);
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
                NetworkIdentity identity = newGameObject.GetComponent<NetworkIdentity>();
                if (identity != null)
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
                NetworkIdentity identity = newGameObject.GetComponent<NetworkIdentity>();
                if (identity != null)
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
            int newComponentIndex = 0;
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
            int componentIndex = 0;
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

        // backing field for sync NetworkBehaviour
        public struct NetworkBehaviourSyncVar : IEquatable<NetworkBehaviourSyncVar>
        {
            public uint netId;
            // limited to 255 behaviours per identity
            public byte componentIndex;

            public NetworkBehaviourSyncVar(uint netId, int componentIndex) : this()
            {
                this.netId = netId;
                this.componentIndex = (byte)componentIndex;
            }

            public bool Equals(NetworkBehaviourSyncVar other)
            {
                return other.netId == netId && other.componentIndex == componentIndex;
            }

            public bool Equals(uint netId, int componentIndex)
            {
                return this.netId == netId && this.componentIndex == componentIndex;
            }

            public override string ToString()
            {
                return $"[netId:{netId} compIndex:{componentIndex}]";
            }
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
        public virtual bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            bool objectWritten = false;
            // if initialState: write all SyncVars.
            // otherwise write dirtyBits+dirty SyncVars
            if (initialState)
            {
                objectWritten = SerializeObjectsAll(writer);
            }
            else
            {
                objectWritten = SerializeObjectsDelta(writer);
            }

            bool syncVarWritten = SerializeSyncVars(writer, initialState);

            return objectWritten || syncVarWritten;
        }

        /// <summary>Override to do custom deserialization (instead of SyncVars/SyncLists). Use OnSerialize too.</summary>
        public virtual void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                DeSerializeObjectsAll(reader);
            }
            else
            {
                DeSerializeObjectsDelta(reader);
            }

            DeserializeSyncVars(reader, initialState);
        }

        // USED BY WEAVER
        protected virtual bool SerializeSyncVars(NetworkWriter writer, bool initialState)
        {
            return false;

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

        public bool SerializeObjectsAll(NetworkWriter writer)
        {
            bool dirty = false;
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                syncObject.OnSerializeAll(writer);
                dirty = true;
            }
            return dirty;
        }

        public bool SerializeObjectsDelta(NetworkWriter writer)
        {
            bool dirty = false;
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
                    dirty = true;
                }
            }
            return dirty;
        }

        internal void DeSerializeObjectsAll(NetworkReader reader)
        {
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                syncObject.OnDeserializeAll(reader);
            }
        }

        internal void DeSerializeObjectsDelta(NetworkReader reader)
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

        /// <summary>Like Start(), but only called for objects the client has authority over.</summary>
        public virtual void OnStartAuthority() {}

        /// <summary>Stop event, only called for objects the client has authority over.</summary>
        public virtual void OnStopAuthority() {}
    }
}
