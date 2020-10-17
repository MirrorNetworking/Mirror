using System;
using System.Collections.Generic;
using System.ComponentModel;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Sync to everyone, or only to owner.
    /// </summary>
    public enum SyncMode { Observers, Owner }

    /// <summary>
    /// Base class which should be inherited by scripts which contain networking functionality.
    /// </summary>
    /// <remarks>
    /// <para>This is a MonoBehaviour class so scripts which need to use the networking feature should inherit this class instead of MonoBehaviour. It allows you to invoke networked actions, receive various callbacks, and automatically synchronize state from server-to-client.</para>
    /// <para>The NetworkBehaviour component requires a NetworkIdentity on the game object. There can be multiple NetworkBehaviours on a single game object. For an object with sub-components in a hierarchy, the NetworkIdentity must be on the root object, and NetworkBehaviour scripts must also be on the root object.</para>
    /// <para>Some of the built-in components of the networking system are derived from NetworkBehaviour, including NetworkTransport, NetworkAnimator and NetworkProximityChecker.</para>
    /// </remarks>
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.com/docs/Guides/NetworkBehaviour.html")]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkBehaviour));

        internal float lastSyncTime;

        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        /// <summary>
        /// sync mode for OnSerialize
        /// </summary>
        [HideInInspector] public SyncMode syncMode = SyncMode.Observers;

        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        /// <summary>
        /// sync interval for OnSerialize (in seconds)
        /// </summary>
        [Tooltip("Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)")]
        // [0,2] should be enough. anything >2s is too laggy anyway.
        [Range(0, 2)]
        [HideInInspector] public float syncInterval = 0.1f;

        /// <summary>
        /// Returns true if this object is active on an active server.
        /// <para>This is only true if the object has been spawned. This is different from NetworkServer.active, which is true if the server itself is active rather than this object being active.</para>
        /// </summary>
        public bool isServer => netIdentity.isServer;

        /// <summary>
        /// Returns true if running as a client and this object was spawned by a server.
        /// </summary>
        public bool isClient => netIdentity.isClient;

        /// <summary>
        /// This returns true if this object is the one that represents the player on the local machine.
        /// <para>In multiplayer games, there are multiple instances of the Player object. The client needs to know which one is for "themselves" so that only that player processes input and potentially has a camera attached. The IsLocalPlayer function will return true only for the player instance that belongs to the player on the local machine, so it can be used to filter out input for non-local players.</para>
        /// </summary>
        public bool isLocalPlayer => netIdentity.isLocalPlayer;

        /// <summary>
        /// True if this object only exists on the server
        /// </summary>
        public bool isServerOnly => isServer && !isClient;

        /// <summary>
        /// True if this object exists on a client that is not also acting as a server
        /// </summary>
        public bool isClientOnly => isClient && !isServer;

        /// <summary>
        /// This returns true if this object is the authoritative version of the object in the distributed network application.
        /// <para>The <see cref="NetworkIdentity.hasAuthority">NetworkIdentity.hasAuthority</see> value on the NetworkIdentity determines how authority is determined. For most objects, authority is held by the server. For objects with <see cref="NetworkIdentity.hasAuthority">NetworkIdentity.hasAuthority</see> set, authority is held by the client of that player.</para>
        /// </summary>
        public bool hasAuthority => netIdentity.hasAuthority;

        /// <summary>
        /// The unique network Id of this object.
        /// <para>This is assigned at runtime by the network server and will be unique for all objects for that network session.</para>
        /// </summary>
        public uint netId => netIdentity.netId;

        /// <summary>
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the client.
        /// </summary>
        public NetworkConnection connectionToServer => netIdentity.connectionToServer;

        /// <summary>
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the server.
        /// </summary>
        public NetworkConnection connectionToClient => netIdentity.connectionToClient;

        protected ulong syncVarDirtyBits { get; private set; }
        ulong syncVarHookGuard;

        protected bool getSyncVarHookGuard(ulong dirtyBit)
        {
            return (syncVarHookGuard & dirtyBit) != 0UL;
        }

        protected void setSyncVarHookGuard(ulong dirtyBit, bool value)
        {
            if (value)
                syncVarHookGuard |= dirtyBit;
            else
                syncVarHookGuard &= ~dirtyBit;
        }

        /// <summary>
        /// objects that can synchronize themselves, such as synclists
        /// </summary>
        protected readonly List<SyncObject> syncObjects = new List<SyncObject>();

        /// <summary>
        /// NetworkIdentity component caching for easier access
        /// </summary>
        NetworkIdentity netIdentityCache;

        /// <summary>
        /// Returns the NetworkIdentity of this object
        /// </summary>
        public NetworkIdentity netIdentity
        {
            get
            {
                if (netIdentityCache is null)
                {
                    netIdentityCache = GetComponent<NetworkIdentity>();
                    // do this 2nd check inside first if so that we are not checking == twice on unity Object
                    if (netIdentityCache is null)
                    {
                        logger.LogError("There is no NetworkIdentity on " + name + ". Please add one.");
                    }
                }
                return netIdentityCache;
            }
        }

        /// <summary>
        /// Returns the index of the component on this object
        /// </summary>
        public int ComponentIndex
        {
            get
            {
                // note: FindIndex causes allocations, we search manually instead
                for (int i = 0; i < netIdentity.NetworkBehaviours.Length; i++)
                {
                    NetworkBehaviour component = netIdentity.NetworkBehaviours[i];
                    if (component == this)
                        return i;
                }

                // this should never happen
                logger.LogError("Could not find component in GameObject. You should not add/remove components in networked objects dynamically", this);

                return -1;
            }
        }

        // this gets called in the constructor by the weaver
        // for every SyncObject in the component (e.g. SyncLists).
        // We collect all of them and we synchronize them with OnSerialize/OnDeserialize
        protected void InitSyncObject(SyncObject syncObject)
        {
            if (syncObject == null)
                logger.LogError("Uninitialized SyncObject. Manually call the constructor on your SyncList, SyncSet or SyncDictionary", this);
            else
                syncObjects.Add(syncObject);
        }

        #region Commands
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendCommandInternal(Type invokeClass, string cmdName, NetworkWriter writer, int channelId, bool ignoreAuthority = false)
        {
            // this was in Weaver before
            // NOTE: we could remove this later to allow calling Cmds on Server
            //       to avoid Wrapper functions. a lot of people requested this.
            if (!NetworkClient.active)
            {
                logger.LogError($"Command Function {cmdName} called without an active client.");
                return;
            }
            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(ignoreAuthority || isLocalPlayer || hasAuthority))
            {
                logger.LogWarning($"Trying to send command for object without authority. {invokeClass.ToString()}.{cmdName}");
                return;
            }

            if (ClientScene.readyConnection == null)
            {
                logger.LogError("Send command attempted with no client running [client=" + connectionToServer + "].");
                return;
            }

            // construct the message
            CommandMessage message = new CommandMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, cmdName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            ClientScene.readyConnection.Send(message, channelId);
        }

        #endregion

        #region Client RPCs
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(Type invokeClass, string rpcName, NetworkWriter writer, int channelId, bool excludeOwner)
        {
            // this was in Weaver before
            if (!NetworkServer.active)
            {
                logger.LogError("RPC Function " + rpcName + " called on Client.");
                return;
            }
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                logger.LogWarning("ClientRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            // The public facing parameter is excludeOwner in [ClientRpc]
            // so we negate it here to logically align with SendToReady.
            bool includeOwner = !excludeOwner;
            NetworkServer.SendToReady(netIdentity, message, includeOwner, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            if (!NetworkServer.active)
            {
                logger.LogError("TargetRPC Function " + rpcName + " called on client.");
                return;
            }
            // connection parameter is optional. assign if null.
            if (conn == null)
            {
                conn = connectionToClient;
            }
            // this was in Weaver before
            if (conn is NetworkConnectionToServer)
            {
                logger.LogError("TargetRPC Function " + rpcName + " called on connection to server");
                return;
            }
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                logger.LogWarning("TargetRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            conn.Send(message, channelId);
        }

        #endregion

        #region Helpers

        // helper function for [SyncVar] GameObjects.
        // IMPORTANT: keep as 'protected', not 'internal', otherwise Weaver
        //            can't resolve it
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected bool SyncVarGameObjectEqual(GameObject newGameObject, uint netIdField)
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
                        logger.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?");
                    }
                }
            }

            return newNetId == netIdField;
        }

        // helper function for [SyncVar] GameObjects.
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref uint netIdField)
        {
            if (getSyncVarHookGuard(dirtyBit))
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
                        logger.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?");
                    }
                }
            }

            if (logger.LogEnabled()) logger.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + netIdField + "->" + newNetId);
            SetDirtyBit(dirtyBit);
            // assign new one on the server, and in case we ever need it on client too
            gameObjectField = newGameObject;
            netIdField = newNetId;
        }

        // helper function for [SyncVar] GameObjects.
        // -> ref GameObject as second argument makes OnDeserialize processing easier
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected GameObject GetSyncVarGameObject(uint netId, ref GameObject gameObjectField)
        {
            // server always uses the field
            if (isServer)
            {
                return gameObjectField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                return gameObjectField = identity.gameObject;
            return null;
        }

        // helper function for [SyncVar] NetworkIdentities.
        // IMPORTANT: keep as 'protected', not 'internal', otherwise Weaver
        //            can't resolve it
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected bool SyncVarNetworkIdentityEqual(NetworkIdentity newIdentity, uint netIdField)
        {
            uint newNetId = 0;
            if (newIdentity != null)
            {
                newNetId = newIdentity.netId;
                if (newNetId == 0)
                {
                    logger.LogWarning("SetSyncVarNetworkIdentity NetworkIdentity " + newIdentity + " has a zero netId. Maybe it is not spawned yet?");
                }
            }

            // netId changed?
            return newNetId == netIdField;
        }

        // helper function for [SyncVar] NetworkIdentities.
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarNetworkIdentity(NetworkIdentity newIdentity, ref NetworkIdentity identityField, ulong dirtyBit, ref uint netIdField)
        {
            if (getSyncVarHookGuard(dirtyBit))
                return;

            uint newNetId = 0;
            if (newIdentity != null)
            {
                newNetId = newIdentity.netId;
                if (newNetId == 0)
                {
                    logger.LogWarning("SetSyncVarNetworkIdentity NetworkIdentity " + newIdentity + " has a zero netId. Maybe it is not spawned yet?");
                }
            }

            if (logger.LogEnabled()) logger.Log("SetSyncVarNetworkIdentity NetworkIdentity " + GetType().Name + " bit [" + dirtyBit + "] netIdField:" + netIdField + "->" + newNetId);
            SetDirtyBit(dirtyBit);
            netIdField = newNetId;
            // assign new one on the server, and in case we ever need it on client too
            identityField = newIdentity;
        }

        // helper function for [SyncVar] NetworkIdentities.
        // -> ref GameObject as second argument makes OnDeserialize processing easier
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected NetworkIdentity GetSyncVarNetworkIdentity(uint netId, ref NetworkIdentity identityField)
        {
            // server always uses the field
            if (isServer)
            {
                return identityField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            NetworkIdentity.spawned.TryGetValue(netId, out identityField);
            return identityField;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected bool SyncVarEqual<T>(T value, ref T fieldValue)
        {
            // newly initialized or changed value?
            return EqualityComparer<T>.Default.Equals(value, fieldValue);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVar<T>(T value, ref T fieldValue, ulong dirtyBit)
        {
            if (logger.LogEnabled()) logger.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value);
            SetDirtyBit(dirtyBit);
            fieldValue = value;
        }
        #endregion

        /// <summary>
        /// Used to set the behaviour as dirty, so that a network update will be sent for the object.
        /// these are masks, not bit numbers, ie. 0x004 not 2
        /// </summary>
        /// <param name="dirtyBit">Bit mask to set.</param>
        public void SetDirtyBit(ulong dirtyBit)
        {
            syncVarDirtyBits |= dirtyBit;
        }

        /// <summary>
        /// This clears all the dirty bits that were set on this script by SetDirtyBits();
        /// <para>This is automatically invoked when an update is sent for this object, but can be called manually as well.</para>
        /// </summary>
        public void ClearAllDirtyBits()
        {
            lastSyncTime = Time.time;
            syncVarDirtyBits = 0L;

            // flush all unsynchronized changes in syncobjects
            // note: don't use List.ForEach here, this is a hot path
            //   List.ForEach: 432b/frame
            //   for: 231b/frame
            for (int i = 0; i < syncObjects.Count; ++i)
            {
                syncObjects[i].Flush();
            }
        }

        bool AnySyncObjectDirty()
        {
            // note: don't use Linq here. 1200 networked objects:
            //   Linq: 187KB GC/frame;, 2.66ms time
            //   for: 8KB GC/frame; 1.28ms time
            for (int i = 0; i < syncObjects.Count; ++i)
            {
                if (syncObjects[i].IsDirty)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsDirty()
        {
            if (Time.time - lastSyncTime >= syncInterval)
            {
                return syncVarDirtyBits != 0L || AnySyncObjectDirty();
            }
            return false;
        }

        /// <summary>
        /// Virtual function to override to send custom serialization data. The corresponding function to send serialization data is OnDeserialize().
        /// </summary>
        /// <remarks>
        /// <para>The initialState flag is useful to differentiate between the first time an object is serialized and when incremental updates can be sent. The first time an object is sent to a client, it must include a full state snapshot, but subsequent updates can save on bandwidth by including only incremental changes. Note that SyncVar hook functions are not called when initialState is true, only for incremental updates.</para>
        /// <para>If a class has SyncVars, then an implementation of this function and OnDeserialize() are added automatically to the class. So a class that has SyncVars cannot also have custom serialization functions.</para>
        /// <para>The OnSerialize function should return true to indicate that an update should be sent. If it returns true, then the dirty bits for that script are set to zero, if it returns false then the dirty bits are not changed. This allows multiple changes to a script to be accumulated over time and sent when the system is ready, instead of every frame.</para>
        /// </remarks>
        /// <param name="writer">Writer to use to write to the stream.</param>
        /// <param name="initialState">If this is being called to send initial state.</param>
        /// <returns>True if data was written.</returns>
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


        /// <summary>
        /// Virtual function to override to receive custom serialization data. The corresponding function to send serialization data is OnSerialize().
        /// </summary>
        /// <param name="reader">Reader to read from the stream.</param>
        /// <param name="initialState">True if being sent initial state.</param>
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

        // Don't rename. Weaver uses this exact function name.
        public virtual bool SerializeSyncVars(NetworkWriter writer, bool initialState)
        {
            return false;

            // SyncVar are writen here in subclass

            // if initialState
            //   write all SyncVars
            // else
            //   write syncVarDirtyBits
            //   write dirty SyncVars
        }

        // Don't rename. Weaver uses this exact function name.
        public virtual void DeserializeSyncVars(NetworkReader reader, bool initialState)
        {
            // SyncVars are read here in subclass

            // if initialState
            //   read all SyncVars
            // else
            //   read syncVarDirtyBits
            //   read dirty SyncVars
        }

        internal ulong DirtyObjectBits()
        {
            ulong dirtyObjects = 0;
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                if (syncObject.IsDirty)
                {
                    dirtyObjects |= 1UL << i;
                }
            }
            return dirtyObjects;
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
            writer.WritePackedUInt64(DirtyObjectBits());
            // serializable objects, such as synclists
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                if (syncObject.IsDirty)
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
            ulong dirty = reader.ReadPackedUInt64();
            for (int i = 0; i < syncObjects.Count; i++)
            {
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

        // Deprecated 04/20/2020
        /// <summary>
        /// Obsolete: Use <see cref="OnStopClient()">OnStopClient()</see> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Override OnStopClient() instead")]
        public virtual void OnNetworkDestroy() { }

        /// <summary>
        /// This is invoked on clients when the server has caused this object to be destroyed.
        /// <para>This can be used as a hook to invoke effects or do client specific cleanup.</para>
        /// </summary>
        public virtual void OnStopClient()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // backwards compatibility
            OnNetworkDestroy();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// This is invoked for NetworkBehaviour objects when they become active on the server.
        /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
        /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
        /// </summary>
        public virtual void OnStartServer() { }

        /// <summary>
        /// Invoked on the server when the object is unspawned
        /// <para>Useful for saving object data in persistant storage</para>
        /// </summary>
        public virtual void OnStopServer() { }

        /// <summary>
        /// Called on every NetworkBehaviour when it is activated on a client.
        /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
        /// </summary>
        public virtual void OnStartClient() { }

        /// <summary>
        /// Called when the local player object has been set up.
        /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
        /// </summary>
        public virtual void OnStartLocalPlayer() { }

        /// <summary>
        /// This is invoked on behaviours that have authority, based on context and <see cref="NetworkIdentity.hasAuthority">NetworkIdentity.hasAuthority</see>.
        /// <para>This is called after <see cref="OnStartServer">OnStartServer</see> and before <see cref="OnStartClient">OnStartClient.</see></para>
        /// <para>When <see cref="NetworkIdentity.AssignClientAuthority">AssignClientAuthority</see> is called on the server, this will be called on the client that owns the object. When an object is spawned with <see cref="NetworkServer.Spawn">NetworkServer.Spawn</see> with a NetworkConnection parameter included, this will be called on the client that owns the object.</para>
        /// </summary>
        public virtual void OnStartAuthority() { }

        /// <summary>
        /// This is invoked on behaviours when authority is removed.
        /// <para>When NetworkIdentity.RemoveClientAuthority is called on the server, this will be called on the client that owns the object.</para>
        /// </summary>
        public virtual void OnStopAuthority() { }
    }
}
