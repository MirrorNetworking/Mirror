using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    [RequireComponent(typeof(NetworkIdentity))]
    [AddComponentMenu("")]
    public class NetworkBehaviour : MonoBehaviour
    {
        float lastSyncTime;

        // sync interval for OnSerialize (in seconds)
        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        [HideInInspector] public float syncInterval = 0.1f;

        public bool localPlayerAuthority => netIdentity.localPlayerAuthority;
        public bool isServer => netIdentity.isServer;
        public bool isClient => netIdentity.isClient;
        public bool isLocalPlayer => netIdentity.isLocalPlayer;
        public bool isServerOnly => isServer && !isClient;
        public bool isClientOnly => isClient && !isServer;
        public bool hasAuthority => netIdentity.hasAuthority;
        public uint netId => netIdentity.netId;
        public NetworkConnection connectionToServer => netIdentity.connectionToServer;
        public NetworkConnection connectionToClient => netIdentity.connectionToClient;
        protected ulong syncVarDirtyBits { get; private set; }
        protected bool syncVarHookGuard { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use syncObjects instead.")]
        protected List<SyncObject> m_SyncObjects => syncObjects;
        // objects that can synchronize themselves,  such as synclists
        protected readonly List<SyncObject> syncObjects = new List<SyncObject>();

        // NetworkIdentity component caching for easier access
        NetworkIdentity netIdentityCache;
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

        private static int GetMethodHash(Type invokeClass, string methodName)
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
                Debug.LogWarning("Trying to send command for object without authority.");
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

            NetworkServer.SendToReady(netIdentity,message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int eventHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(eventHash, MirrorInvokeType.SyncEvent, reader);
        }
        #endregion

        #region Code Gen Path Helpers
        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);

        protected class Invoker
        {
            public MirrorInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;
        }

        static Dictionary<int, Invoker> cmdHandlerDelegates = new Dictionary<int, Invoker>();

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
        #endregion

        #region Helpers
        // helper function for [SyncVar] GameObjects.
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref uint netIdField)
        {
            if (syncVarHookGuard)
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

            // netId changed?
            if (newNetId != netIdField)
            {
                if (LogFilter.Debug) Debug.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + netIdField + "->" + newNetId);
                SetDirtyBit(dirtyBit);
                gameObjectField = newGameObject; // assign new one on the server, and in case we ever need it on client too
                netIdField = newNetId;
            }
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
                return identity.gameObject;
            return null;
        }

        // helper function for [SyncVar] NetworkIdentities.
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarNetworkIdentity(NetworkIdentity newIdentity, ref NetworkIdentity identityField, ulong dirtyBit, ref uint netIdField)
        {
            if (syncVarHookGuard)
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

            // netId changed?
            if (newNetId != netIdField)
            {
                if (LogFilter.Debug) Debug.Log("SetSyncVarNetworkIdentity NetworkIdentity " + GetType().Name + " bit [" + dirtyBit + "] netIdField:" + netIdField + "->" + newNetId);
                SetDirtyBit(dirtyBit);
                netIdField = newNetId;
                identityField = newIdentity; // assign new one on the server, and in case we ever need it on client too
            }
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
            NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity);
            return identity;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVar<T>(T value, ref T fieldValue, ulong dirtyBit)
        {
            // newly initialized or changed value?
            if (!EqualityComparer<T>.Default.Equals(value, fieldValue))
            {
                if (LogFilter.Debug) Debug.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value);
                SetDirtyBit(dirtyBit);
                fieldValue = value;
            }
        }
        #endregion

        // these are masks, not bit numbers, ie. 0x004 not 2
        public void SetDirtyBit(ulong dirtyBit)
        {
            syncVarDirtyBits |= dirtyBit;
        }

        public void ClearAllDirtyBits()
        {
            lastSyncTime = Time.time;
            syncVarDirtyBits = 0L;

            // flush all unsynchronized changes in syncobjects
            // note: don't use List.ForEach here, this is a hot path
            // List.ForEach: 432b/frame
            // for: 231b/frame
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

        public virtual bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                return SerializeObjectsAll(writer);
            }
            else
            {
                return SerializeObjectsDelta(writer);
            }
        }

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

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void OnNetworkDestroy() {}
        public virtual void OnStartServer() {}
        public virtual void OnStartClient() {}
        public virtual void OnStartLocalPlayer() {}
        public virtual void OnStartAuthority() {}
        public virtual void OnStopAuthority() {}

        // return true when overwriting so that Mirror knows that we wanted to
        // rebuild observers ourselves. otherwise it uses built in rebuild.
        public virtual bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            return false;
        }

        public virtual void OnSetLocalVisibility(bool vis) {}

        public virtual bool OnCheckObserver(NetworkConnection conn)
        {
            return true;
        }
    }
}
