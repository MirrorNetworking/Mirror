using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public class NetworkBehaviour : MonoBehaviour
    {
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
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the server.
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
        /// Obsolete: Use <see cref="syncObjects"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use syncObjects instead.")]
        protected List<SyncObject> m_SyncObjects => syncObjects;

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
                if (netIdentityCache == null)
                {
                    netIdentityCache = GetComponent<NetworkIdentity>();
                }
                if (netIdentityCache == null)
                {
                    Debug.LogError("There is no NetworkIdentity on " + name + ". Please add one.");
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
                Debug.LogError("Could not find component in GameObject. You should not add/remove components in networked objects dynamically", this);

                return -1;
            }
        }

        // this gets called in the constructor by the weaver
        // for every SyncObject in the component (e.g. SyncLists).
        // We collect all of them and we synchronize them with OnSerialize/OnDeserialize
        protected void InitSyncObject(SyncObject syncObject)
        {
            syncObjects.Add(syncObject);
        }

        #region Commands

        static int GetMethodHash(Type invokeClass, string methodName)
        {
            // (invokeClass + ":" + cmdName).GetStableHashCode() would cause allocations.
            // so hash1 + hash2 is better.
            unchecked
            {
                int hash = invokeClass.FullName.GetStableHashCode();
                return hash * 503 + methodName.GetStableHashCode();
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendCommandInternal(Type invokeClass, string cmdName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            // NOTE: we could remove this later to allow calling Cmds on Server
            //       to avoid Wrapper functions. a lot of people requested this.
            if (!NetworkClient.active)
            {
                Debug.LogError("Command Function " + cmdName + " called on server without an active client.");
                return;
            }
            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(isLocalPlayer || hasAuthority))
            {
                Debug.LogWarning($"Trying to send command for object without authority. {invokeClass.ToString()}.{cmdName}");
                return;
            }

            if (ClientScene.readyConnection == null)
            {
                Debug.LogError("Send command attempted with no client running [client=" + connectionToServer + "].");
                return;
            }

            // construct the message
            CommandMessage message = new CommandMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                functionHash = GetMethodHash(invokeClass, cmdName), // type+func so Inventory.RpcUse != Equipment.RpcUse
                payload = writer.ToArraySegment() // segment to avoid reader allocations
            };

            ClientScene.readyConnection.Send(message, channelId);
        }

        /// <summary>
        /// Manually invoke a Command.
        /// </summary>
        /// <param name="cmdHash">Hash of the Command name.</param>
        /// <param name="reader">Parameters to pass to the command.</param>
        /// <returns>Returns true if successful.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeCommand(int cmdHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(cmdHash, MirrorInvokeType.Command, reader);
        }
        #endregion

        #region Client RPCs
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            if (!NetworkServer.active)
            {
                Debug.LogError("RPC Function " + rpcName + " called on Client.");
                return;
            }
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                Debug.LogWarning("ClientRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                functionHash = GetMethodHash(invokeClass, rpcName), // type+func so Inventory.RpcUse != Equipment.RpcUse
                payload = writer.ToArraySegment() // segment to avoid reader allocations
            };

            NetworkServer.SendToReady(netIdentity, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            if (!NetworkServer.active)
            {
                Debug.LogError("TargetRPC Function " + rpcName + " called on client.");
                return;
            }
            // connection parameter is optional. assign if null.
            if (conn == null)
            {
                conn = connectionToClient;
            }
            // this was in Weaver before
            if (conn is ULocalConnectionToServer)
            {
                Debug.LogError("TargetRPC Function " + rpcName + " called on connection to server");
                return;
            }
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                Debug.LogWarning("TargetRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                functionHash = GetMethodHash(invokeClass, rpcName), // type+func so Inventory.RpcUse != Equipment.RpcUse
                payload = writer.ToArraySegment() // segment to avoid reader allocations
            };

            conn.Send(message, channelId);
        }

        /// <summary>
        /// Manually invoke an RPC function.
        /// </summary>
        /// <param name="rpcHash">Hash of the RPC name.</param>
        /// <param name="reader">Parameters to pass to the RPC function.</param>
        /// <returns>Returns true if successful.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeRPC(int rpcHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(rpcHash, MirrorInvokeType.ClientRpc, reader);
        }
        #endregion

        #region Sync Events
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendEventInternal(Type invokeClass, string eventName, NetworkWriter writer, int channelId)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("SendEvent no server?");
                return;
            }

            // construct the message
            SyncEventMessage message = new SyncEventMessage
            {
                netId = netId,
                componentIndex = ComponentIndex,
                functionHash = GetMethodHash(invokeClass, eventName), // type+func so Inventory.RpcUse != Equipment.RpcUse
                payload = writer.ToArraySegment() // segment to avoid reader allocations
            };

            NetworkServer.SendToReady(netIdentity, message, channelId);
        }

        /// <summary>
        /// Manually invoke a SyncEvent.
        /// </summary>
        /// <param name="eventHash">Hash of the SyncEvent name.</param>
        /// <param name="reader">Parameters to pass to the SyncEvent.</param>
        /// <returns>Returns true if successful.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int eventHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(eventHash, MirrorInvokeType.SyncEvent, reader);
        }
        #endregion

        #region Code Gen Path Helpers
        /// <summary>
        /// Delegate for Command functions.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="reader"></param>
        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);

        protected class Invoker
        {
            public MirrorInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;
        }

        static readonly Dictionary<int, Invoker> cmdHandlerDelegates = new Dictionary<int, Invoker>();

        // helper function register a Command/Rpc/SyncEvent delegate
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterDelegate(Type invokeClass, string cmdName, MirrorInvokeType invokerType, CmdDelegate func)
        {
            int cmdHash = GetMethodHash(invokeClass, cmdName); // type+func so Inventory.RpcUse != Equipment.RpcUse

            if (cmdHandlerDelegates.ContainsKey(cmdHash))
            {
                // something already registered this hash
                Invoker oldInvoker = cmdHandlerDelegates[cmdHash];
                if (oldInvoker.invokeClass == invokeClass && oldInvoker.invokeType == invokerType && oldInvoker.invokeFunction == func)
                {
                    // it's all right,  it was the same function
                    return;
                }

                Debug.LogError($"Function {oldInvoker.invokeClass}.{oldInvoker.invokeFunction.GetMethodName()} and {invokeClass}.{oldInvoker.invokeFunction.GetMethodName()} have the same hash.  Please rename one of them");
            }
            Invoker invoker = new Invoker
            {
                invokeType = invokerType,
                invokeClass = invokeClass,
                invokeFunction = func
            };
            cmdHandlerDelegates[cmdHash] = invoker;
            if (LogFilter.Debug) Debug.Log("RegisterDelegate hash:" + cmdHash + " invokerType: " + invokerType + " method:" + func.GetMethodName());
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterCommandDelegate(Type invokeClass, string cmdName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, cmdName, MirrorInvokeType.Command, func);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterRpcDelegate(Type invokeClass, string rpcName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, rpcName, MirrorInvokeType.ClientRpc, func);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterEventDelegate(Type invokeClass, string eventName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, eventName, MirrorInvokeType.SyncEvent, func);
        }

        static bool GetInvokerForHash(int cmdHash, MirrorInvokeType invokeType, out Invoker invoker)
        {
            if (cmdHandlerDelegates.TryGetValue(cmdHash, out invoker) &&
                invoker != null &&
                invoker.invokeType == invokeType)
            {
                return true;
            }

            // debug message if not found, or null, or mismatched type
            // (no need to throw an error, an attacker might just be trying to
            //  call an cmd with an rpc's hash)
            if (LogFilter.Debug) Debug.Log("GetInvokerForHash hash:" + cmdHash + " not found");
            return false;
        }

        // InvokeCmd/Rpc/SyncEventDelegate can all use the same function here
        internal bool InvokeHandlerDelegate(int cmdHash, MirrorInvokeType invokeType, NetworkReader reader)
        {
            if (GetInvokerForHash(cmdHash, invokeType, out Invoker invoker) &&
                invoker.invokeClass.IsInstanceOfType(this))
            {
                invoker.invokeFunction(this, reader);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the handler function for a given hash
        /// Can be used by profilers and debuggers
        /// </summary>
        /// <param name="cmdHash">rpc function hash</param>
        /// <returns>The function delegate that will handle the command</returns>
        public static CmdDelegate GetRpcHandler(int cmdHash)
        {
            if (cmdHandlerDelegates.TryGetValue(cmdHash, out Invoker invoker))
            {
                return invoker.invokeFunction;
            }
            return null;
        }

        #endregion

        #region Helpers

        // helper function for [SyncVar] GameObjects.
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
                        Debug.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?");
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
                        Debug.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?");
                    }
                }
            }

            if (LogFilter.Debug) Debug.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + netIdField + "->" + newNetId);
            SetDirtyBit(dirtyBit);
            gameObjectField = newGameObject; // assign new one on the server, and in case we ever need it on client too
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
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected bool SyncVarNetworkIdentityEqual(NetworkIdentity newIdentity, uint netIdField)
        {
            uint newNetId = 0;
            if (newIdentity != null)
            {
                newNetId = newIdentity.netId;
                if (newNetId == 0)
                {
                    Debug.LogWarning("SetSyncVarNetworkIdentity NetworkIdentity " + newIdentity + " has a zero netId. Maybe it is not spawned yet?");
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
                    Debug.LogWarning("SetSyncVarNetworkIdentity NetworkIdentity " + newIdentity + " has a zero netId. Maybe it is not spawned yet?");
                }
            }

            if (LogFilter.Debug) Debug.Log("SetSyncVarNetworkIdentity NetworkIdentity " + GetType().Name + " bit [" + dirtyBit + "] netIdField:" + netIdField + "->" + newNetId);
            SetDirtyBit(dirtyBit);
            netIdField = newNetId;
            identityField = newIdentity; // assign new one on the server, and in case we ever need it on client too
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
            if (LogFilter.Debug) Debug.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value);
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

        internal bool IsDirty()
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
            if (initialState)
            {
                return SerializeObjectsAll(writer);
            }
            return SerializeObjectsDelta(writer);
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
        }

        ulong DirtyObjectBits()
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

        void DeSerializeObjectsAll(NetworkReader reader)
        {
            for (int i = 0; i < syncObjects.Count; i++)
            {
                SyncObject syncObject = syncObjects[i];
                syncObject.OnDeserializeAll(reader);
            }
        }

        void DeSerializeObjectsDelta(NetworkReader reader)
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

        /// <summary>
        /// This is invoked on clients when the server has caused this object to be destroyed.
        /// <para>This can be used as a hook to invoke effects or do client specific cleanup.</para>
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void OnNetworkDestroy() {}

        /// <summary>
        /// This is invoked for NetworkBehaviour objects when they become active on the server.
        /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
        /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
        /// </summary>
        public virtual void OnStartServer() {}

        /// <summary>
        /// Called on every NetworkBehaviour when it is activated on a client.
        /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
        /// </summary>
        public virtual void OnStartClient() {}

        /// <summary>
        /// Called when the local player object has been set up.
        /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
        /// </summary>
        public virtual void OnStartLocalPlayer() {}

        /// <summary>
        /// This is invoked on behaviours that have authority, based on context and <see cref="NetworkIdentity.hasAuthority">NetworkIdentity.hasAuthority</see>.
        /// <para>This is called after <see cref="OnStartServer">OnStartServer</see> and <see cref="OnStartClient">OnStartClient.</see></para>
        /// <para>When <see cref="NetworkIdentity.AssignClientAuthority"/> is called on the server, this will be called on the client that owns the object. When an object is spawned with <see cref="NetworkServer.Spawn">NetworkServer.Spawn</see> with a NetworkConnection parameter included, this will be called on the client that owns the object.</para>
        /// </summary>
        public virtual void OnStartAuthority() {}

        /// <summary>
        /// This is invoked on behaviours when authority is removed.
        /// <para>When NetworkIdentity.RemoveClientAuthority is called on the server, this will be called on the client that owns the object.</para>
        /// </summary>
        public virtual void OnStopAuthority() {}

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// <para>Implementations of this callback should add network connections of players that can see this object to the observers set.</para>
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        /// <returns>true when overwriting so that Mirror knows that we wanted to rebuild observers ourselves. otherwise it uses built in rebuild.</returns>
        public virtual bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            return false;
        }

        [Obsolete("Rename to OnSetHostVisibility instead.")]
        public virtual void OnSetLocalVisibility(bool visible) {}

        /// <summary>
        /// Callback used by the visibility system for objects on a host.
        /// <para>Objects on a host (with a local client) cannot be disabled or destroyed when they are not visibile to the local client. So this function is called to allow custom code to hide these objects. A typical implementation will disable renderer components on the object. This is only called on local clients on a host.</para>
        /// </summary>
        /// <param name="visible">New visibility state.</param>
        public virtual void OnSetHostVisibility(bool visible) {}

        /// <summary>
        /// Callback used by the visibility system to determine if an observer (player) can see this object.
        /// <para>If this function returns true, the network connection will be added as an observer.</para>
        /// </summary>
        /// <param name="conn">Network connection of a player.</param>
        /// <returns>True if the player can see this object.</returns>
        public virtual bool OnCheckObserver(NetworkConnection conn)
        {
            return true;
        }
    }
}
