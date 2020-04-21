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
        public bool IsServer => NetIdentity.IsServer;

        /// <summary>
        /// Returns true if running as a client and this object was spawned by a server.
        /// </summary>
        public bool IsClient => NetIdentity.IsClient;

        /// <summary>
        /// Returns true if we're on host mode.
        /// </summary>
        public bool IsLocalClient => NetIdentity.IsLocalClient;

        /// <summary>
        /// This returns true if this object is the one that represents the player on the local machine.
        /// <para>In multiplayer games, there are multiple instances of the Player object. The client needs to know which one is for "themselves" so that only that player processes input and potentially has a camera attached. The IsLocalPlayer function will return true only for the player instance that belongs to the player on the local machine, so it can be used to filter out input for non-local players.</para>
        /// </summary>
        public bool IsLocalPlayer => NetIdentity.IsLocalPlayer;

        /// <summary>
        /// True if this object only exists on the server
        /// </summary>
        public bool IsServerOnly => IsServer && !IsClient;

        /// <summary>
        /// True if this object exists on a client that is not also acting as a server
        /// </summary>
        public bool IsClientOnly => IsClient && !IsServer;

        /// <summary>
        /// This returns true if this object is the authoritative version of the object in the distributed network application.
        /// <para>The <see cref="NetworkIdentity.HasAuthority">NetworkIdentity.hasAuthority</see> value on the NetworkIdentity determines how authority is determined. For most objects, authority is held by the server. For objects with <see cref="NetworkIdentity.HasAuthority">NetworkIdentity.hasAuthority</see> set, authority is held by the client of that player.</para>
        /// </summary>
        public bool HasAuthority => NetIdentity.HasAuthority;

        /// <summary>
        /// The unique network Id of this object.
        /// <para>This is assigned at runtime by the network server and will be unique for all objects for that network session.</para>
        /// </summary>
        public uint NetId => NetIdentity.NetId;

        /// <summary>
        /// The <see cref="NetworkServer">NetworkClient</see> associated to this object.
        /// </summary>
        public NetworkServer Server => NetIdentity.Server;

        /// <summary>
        /// The <see cref="NetworkClient">NetworkClient</see> associated to this object.
        /// </summary>
        public NetworkClient Client => NetIdentity.Client;

        /// <summary>
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the server.
        /// </summary>
        public INetworkConnection ConnectionToServer => NetIdentity.ConnectionToServer;

        /// <summary>
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the server.
        /// </summary>
        public INetworkConnection ConnectionToClient => NetIdentity.ConnectionToClient;

        public NetworkTime NetworkTime => IsClient ? Client.Time : Server.Time;

        protected ulong SyncVarDirtyBits { get; private set; }
        ulong syncVarHookGuard;

        protected bool GetSyncVarHookGuard(ulong dirtyBit)
        {
            return (syncVarHookGuard & dirtyBit) != 0UL;
        }

        protected void SetSyncVarHookGuard(ulong dirtyBit, bool value)
        {
            if (value)
                syncVarHookGuard |= dirtyBit;
            else
                syncVarHookGuard &= ~dirtyBit;
        }

        /// <summary>
        /// objects that can synchronize themselves, such as synclists
        /// </summary>
        protected readonly List<ISyncObject> syncObjects = new List<ISyncObject>();

        /// <summary>
        /// NetworkIdentity component caching for easier access
        /// </summary>
        NetworkIdentity netIdentityCache;

        /// <summary>
        /// Returns the NetworkIdentity of this object
        /// </summary>
        public NetworkIdentity NetIdentity
        {
            get
            {
                // in this specific case,  we want to know if we have set it before
                // so we can compare if the reference is null
                // instead of calling unity's MonoBehaviour == operator
                if (((object)netIdentityCache) == null)
                {
                    netIdentityCache = GetComponent<NetworkIdentity>();
                }
                if (((object)netIdentityCache) == null)
                {
                    logger.LogError("There is no NetworkIdentity on " + name + ". Please add one.");
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
                for (int i = 0; i < NetIdentity.NetworkBehaviours.Length; i++)
                {
                    NetworkBehaviour component = NetIdentity.NetworkBehaviours[i];
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
        protected void InitSyncObject(ISyncObject syncObject)
        {
            syncObjects.Add(syncObject);
        }

        #region Commands

        internal static int GetMethodHash(Type invokeClass, string methodName)
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
            if (!Client.Active)
            {
                throw new InvalidOperationException("Command Function " + cmdName + " called on server without an active client.");
            }

            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(IsLocalPlayer || HasAuthority))
            {
                throw new UnauthorizedAccessException($"Trying to send command for object without authority. {invokeClass.ToString()}.{cmdName}");
            }

            if (Client.Connection == null)
            {
                throw new InvalidOperationException("Send command attempted with no client running [client=" + ConnectionToServer + "].");
            }

            // construct the message
            var message = new CommandMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = GetMethodHash(invokeClass, cmdName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            _ = Client.SendAsync(message, channelId);
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
        protected void SendRpcInternal(Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            if (!Server.Active)
            {
                throw new InvalidOperationException("RPC Function " + rpcName + " called on Client.");
            }
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!IsServer)
            {
                logger.LogWarning("ClientRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            var message = new RpcMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            Server.SendToReady(NetIdentity, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRpcInternal(INetworkConnection conn, Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            if (!Server.Active)
            {
                throw new InvalidOperationException("TargetRPC Function " + rpcName + " called on client.");
            }

            // connection parameter is optional. assign if null.
            if (conn == null)
            {
                conn = ConnectionToClient;
            }

            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!IsServer)
            {
                logger.LogWarning("TargetRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            var message = new RpcMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
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
        public virtual bool InvokeRpc(int rpcHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(rpcHash, MirrorInvokeType.ClientRpc, reader);
        }
        #endregion

        #region Sync Events
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendEventInternal(Type invokeClass, string eventName, NetworkWriter writer, int channelId)
        {
            if (!Server.Active)
            {
                logger.LogWarning("SendEvent no server?");
                return;
            }

            // construct the message
            var message = new SyncEventMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = GetMethodHash(invokeClass, eventName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            Server.SendToReady(NetIdentity, message, channelId);
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
            // type+func so Inventory.RpcUse != Equipment.RpcUse
            int cmdHash = GetMethodHash(invokeClass, cmdName);

            if (cmdHandlerDelegates.ContainsKey(cmdHash))
            {
                // something already registered this hash
                Invoker oldInvoker = cmdHandlerDelegates[cmdHash];
                if (oldInvoker.invokeClass == invokeClass &&
                    oldInvoker.invokeType == invokerType &&
                    oldInvoker.invokeFunction == func)
                {
                    // it's all right,  it was the same function
                    return;
                }

                logger.LogError($"Function {oldInvoker.invokeClass}.{oldInvoker.invokeFunction.GetMethodName()} and {invokeClass}.{func.GetMethodName()} have the same hash.  Please rename one of them");
            }
            var invoker = new Invoker
            {
                invokeType = invokerType,
                invokeClass = invokeClass,
                invokeFunction = func
            };
            cmdHandlerDelegates[cmdHash] = invoker;
            if (logger.LogEnabled()) logger.Log("RegisterDelegate hash:" + cmdHash + " invokerType: " + invokerType + " method:" + func.GetMethodName());
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterCommandDelegate(Type invokeClass, string cmdName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, cmdName, MirrorInvokeType.Command, func);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterRpcDelegate(Type invokeClass, string rpcName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, rpcName, MirrorInvokeType.ClientRpc, func);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterEventDelegate(Type invokeClass, string eventName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, eventName, MirrorInvokeType.SyncEvent, func);
        }

        // we need a way to clean up delegates after tests
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static void ClearDelegates()
        {
            cmdHandlerDelegates.Clear();
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
            if (logger.LogEnabled()) logger.Log("GetInvokerForHash hash:" + cmdHash + " not found");
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

        [Obsolete("Use NetworkBehaviour.GetDelegate instead.")]
        public static CmdDelegate GetRpcHandler(int cmdHash) => GetDelegate(cmdHash);

        /// <summary>
        /// Gets the handler function for a given hash
        /// Can be used by profilers and debuggers
        /// </summary>
        /// <param name="cmdHash">rpc function hash</param>
        /// <returns>The function delegate that will handle the command</returns>
        public static CmdDelegate GetDelegate(int cmdHash)
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
                    newNetId = identity.NetId;
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
            if (GetSyncVarHookGuard(dirtyBit))
                return;

            uint newNetId = 0;
            if (newGameObject != null)
            {
                NetworkIdentity identity = newGameObject.GetComponent<NetworkIdentity>();
                if (identity != null)
                {
                    newNetId = identity.NetId;
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
            if (!IsServer && !IsClient)
                return gameObjectField;

            // server always uses the field
            if (IsServer)
            {
                return gameObjectField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            if (Client.Spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
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
                newNetId = newIdentity.NetId;
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
            if (GetSyncVarHookGuard(dirtyBit))
                return;

            uint newNetId = 0;
            if (newIdentity != null)
            {
                newNetId = newIdentity.NetId;
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
            if (IsServer)
            {
                return identityField;
            }

            // client always looks up based on netId because objects might get in and out of range
            // over and over again, which shouldn't null them forever
            Client.Spawned.TryGetValue(netId, out identityField);
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
            SyncVarDirtyBits |= dirtyBit;
        }

        /// <summary>
        /// This clears all the dirty bits that were set on this script by SetDirtyBits();
        /// <para>This is automatically invoked when an update is sent for this object, but can be called manually as well.</para>
        /// </summary>
        public void ClearAllDirtyBits()
        {
            lastSyncTime = Time.time;
            SyncVarDirtyBits = 0L;

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
                return SyncVarDirtyBits != 0L || AnySyncObjectDirty();
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
                ISyncObject syncObject = syncObjects[i];
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
                ISyncObject syncObject = syncObjects[i];
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
                ISyncObject syncObject = syncObjects[i];
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
                ISyncObject syncObject = syncObjects[i];
                syncObject.OnDeserializeAll(reader);
            }
        }

        internal void DeSerializeObjectsDelta(NetworkReader reader)
        {
            ulong dirty = reader.ReadPackedUInt64();
            for (int i = 0; i < syncObjects.Count; i++)
            {
                ISyncObject syncObject = syncObjects[i];
                if ((dirty & (1UL << i)) != 0)
                {
                    syncObject.OnDeserializeDelta(reader);
                }
            }
        }

        internal void ResetSyncObjects()
        {
            foreach (var syncObject in syncObjects)
            {
                syncObject.Reset();
            }
        }
    }
}
