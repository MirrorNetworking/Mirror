using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using System.Linq;

namespace Mirror
{
    [RequireComponent(typeof(NetworkIdentity))]
    [AddComponentMenu("")]
    public class NetworkBehaviour : MonoBehaviour
    {
        ulong m_SyncVarDirtyBits; // ulong instead of uint for 64 instead of 32 SyncVar limit per component
        float m_LastSendTime;

        // sync interval for OnSerialize (in seconds)
        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        [HideInInspector] public float syncInterval = 0.1f;

        // this prevents recursion when SyncVar hook functions are called.
        bool m_SyncVarGuard;

        ///<summary>True if the object is controlled by the client that owns it.</summary>
        public bool localPlayerAuthority { get { return netIdentity.localPlayerAuthority; } }
        ///<summary>True if this object is running on the server, and has been spawned.</summary>
        public bool isServer { get { return netIdentity.isServer; } }
        ///<summary>True if the object is running on a client.</summary>
        public bool isClient { get { return netIdentity.isClient; } }
        ///<summary>True if the object is the one that represents the player on the local machine.</summary>
        public bool isLocalPlayer { get { return netIdentity.isLocalPlayer; } }
        ///<summary>True if the object is only running on the server, and has been spawned.</summary>
        public bool isServerOnly { get { return isServer && !isClient; } }
        ///<summary>True if the object is only running on the client.</summary>
        public bool isClientOnly { get { return isClient && !isServer; } }
        ///<summary>True if this object is the authoritative version of the object. For more info: https://vis2k.github.io/Mirror/Concepts/Authority</summary>
        public bool hasAuthority { get { return netIdentity.hasAuthority; } }
        ///<summary>A unique identifier for this network object, assigned when spawned.</summary>
        public uint netId { get { return netIdentity.netId; } }
        ///<summary>The NetworkConnection associated with this NetworkIdentity. This is only valid for player objects on a local client.</summary>
        public NetworkConnection connectionToServer { get { return netIdentity.connectionToServer; } }
        ///<summary>The NetworkConnection associated with this NetworkIdentity. This is only valid for player objects on the server.</summary>
        public NetworkConnection connectionToClient { get { return netIdentity.connectionToClient; } }
        protected ulong syncVarDirtyBits { get { return m_SyncVarDirtyBits; } }
        protected bool syncVarHookGuard { get { return m_SyncVarGuard; } set { m_SyncVarGuard = value; }}

        // objects that can synchronize themselves,  such as synclists
        protected readonly List<SyncObject> m_SyncObjects = new List<SyncObject>();

        // NetworkIdentity component caching for easier access
        NetworkIdentity m_netIdentity;
        ///<summary>The NetworkIdentity attached to this object.</summary>
        public NetworkIdentity netIdentity
        {
            get
            {
                m_netIdentity = m_netIdentity ?? GetComponent<NetworkIdentity>();
                if (m_netIdentity == null)
                {
                    Debug.LogError("There is no NetworkIdentity on " + name + ". Please add one.");
                }
                return m_netIdentity;
            }
        }

        public int ComponentIndex
        {
            get
            {
                int index = Array.FindIndex(netIdentity.NetworkBehaviours, component => component == this);
                if (index < 0)
                {
                    // this should never happen
                    Debug.LogError("Could not find component in GameObject. You should not add/remove components in networked objects dynamically", this);
                }

                return index;
            }
        }

        // this gets called in the constructor by the weaver
        // for every SyncObject in the component (e.g. SyncLists).
        // We collect all of them and we synchronize them with OnSerialize/OnDeserialize
        protected void InitSyncObject(SyncObject syncObject)
        {
            m_SyncObjects.Add(syncObject);
        }

        // ----------------------------- Commands --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendCommandInternal(Type invokeClass, string cmdName, NetworkWriter writer, int channelId)
        {
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
            CommandMessage message = new CommandMessage();
            message.netId = netId;
            message.componentIndex = ComponentIndex;
            message.cmdHash = (invokeClass + ":" + cmdName).GetStableHashCode(); // type+func so Inventory.RpcUse != Equipment.RpcUse
            message.payload = writer.ToArray();

            ClientScene.readyConnection.Send((short)MsgType.Command, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeCommand(int cmdHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(cmdHash, UNetInvokeType.Command, reader);
        }

        // ----------------------------- Client RPCs --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                Debug.LogWarning("ClientRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage();
            message.netId = netId;
            message.componentIndex = ComponentIndex;
            message.rpcHash = (invokeClass + ":" + rpcName).GetStableHashCode(); // type+func so Inventory.RpcUse != Equipment.RpcUse
            message.payload = writer.ToArray();

            NetworkServer.SendToReady(gameObject, (short)MsgType.Rpc, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                Debug.LogWarning("TargetRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage();
            message.netId = netId;
            message.componentIndex = ComponentIndex;
            message.rpcHash = (invokeClass + ":" + rpcName).GetStableHashCode(); // type+func so Inventory.RpcUse != Equipment.RpcUse
            message.payload = writer.ToArray();

            conn.Send((short)MsgType.Rpc, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeRPC(int rpcHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(rpcHash, UNetInvokeType.ClientRpc, reader);
        }

        // ----------------------------- Sync Events --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendEventInternal(Type invokeClass, string eventName, NetworkWriter writer, int channelId)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("SendEvent no server?");
                return;
            }

            // construct the message
            SyncEventMessage message = new SyncEventMessage();
            message.netId = netId;
            message.componentIndex = ComponentIndex;
            message.eventHash = (invokeClass + ":" + eventName).GetStableHashCode(); // type+func so Inventory.RpcUse != Equipment.RpcUse
            message.payload = writer.ToArray();

            NetworkServer.SendToReady(gameObject, (short)MsgType.SyncEvent, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int eventHash, NetworkReader reader)
        {
            return InvokeHandlerDelegate(eventHash, UNetInvokeType.SyncEvent, reader);
        }

        // ----------------------------- Code Gen Path Helpers  --------------------------------

        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);

        protected class Invoker
        {
            public UNetInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;
        }

        static Dictionary<int, Invoker> s_CmdHandlerDelegates = new Dictionary<int, Invoker>();

        // helper function register a Command/Rpc/SyncEvent delegate
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterDelegate(Type invokeClass, string cmdName, UNetInvokeType invokerType, CmdDelegate func)
        {
            int cmdHash = (invokeClass + ":" + cmdName).GetStableHashCode(); // type+func so Inventory.RpcUse != Equipment.RpcUse

            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                // something already registered this hash
                Invoker oldInvoker = s_CmdHandlerDelegates[cmdHash];
                if (oldInvoker.invokeClass == invokeClass && oldInvoker.invokeType == invokerType && oldInvoker.invokeFunction == func)
                {
                    // it's all right,  it was the same function
                    return;
                }

                Debug.LogError(string.Format(
                    "Function {0}.{1} and {2}.{3} have the same hash.  Please rename one of them",
                    oldInvoker.invokeClass,
                    oldInvoker.invokeFunction.GetMethodName(),
                    invokeClass,
                    oldInvoker.invokeFunction.GetMethodName()));
            }
            Invoker invoker = new Invoker();
            invoker.invokeType = invokerType;
            invoker.invokeClass = invokeClass;
            invoker.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = invoker;
            if (LogFilter.Debug) { Debug.Log("RegisterDelegate hash:" + cmdHash + " invokerType: " + invokerType + " method:" + func.GetMethodName()); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterCommandDelegate(Type invokeClass, string cmdName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, cmdName, UNetInvokeType.Command, func);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterRpcDelegate(Type invokeClass, string rpcName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, rpcName, UNetInvokeType.ClientRpc, func);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterEventDelegate(Type invokeClass, string eventName, CmdDelegate func)
        {
            RegisterDelegate(invokeClass, eventName, UNetInvokeType.SyncEvent, func);
        }

        static bool GetInvokerForHash(int cmdHash, UNetInvokeType invokeType, out Invoker invoker)
        {
            if (s_CmdHandlerDelegates.TryGetValue(cmdHash, out invoker) &&
                invoker != null &&
                invoker.invokeType == invokeType)
            {
                return true;
            }

            // debug message if not found, or null, or mismatched type
            // (no need to throw an error, an attacker might just be trying to
            //  call an cmd with an rpc's hash)
            if (LogFilter.Debug) { Debug.Log("GetInvokerForHash hash:" + cmdHash + " not found"); }
            return false;
        }

        // InvokeCmd/Rpc/SyncEventDelegate can all use the same function here
        internal bool InvokeHandlerDelegate(int cmdHash, UNetInvokeType invokeType, NetworkReader reader)
        {
            Invoker invoker;
            if (GetInvokerForHash(cmdHash, invokeType, out invoker) &&
                invoker.invokeClass.IsInstanceOfType(this))
            {
                invoker.invokeFunction(this, reader);
                return true;
            }
            return false;
        }

        // ----------------------------- Helpers  --------------------------------

        // helper function for [SyncVar] GameObjects.
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref uint netIdField)
        {
            if (m_SyncVarGuard)
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
                if (LogFilter.Debug) { Debug.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + netIdField + "->" + newNetId); }
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
            NetworkIdentity identity;
            if (NetworkIdentity.spawned.TryGetValue(netId, out identity) && identity != null)
                return identity.gameObject;
            return null;
        }

        // helper function for [SyncVar] NetworkIdentities.
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarNetworkIdentity(NetworkIdentity newIdentity, ref NetworkIdentity identityField, ulong dirtyBit, ref uint netIdField)
        {
            if (m_SyncVarGuard)
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
                if (LogFilter.Debug) { Debug.Log("SetSyncVarNetworkIdentity NetworkIdentity " + GetType().Name + " bit [" + dirtyBit + "] netIdField:" + netIdField + "->" + newNetId); }
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
            NetworkIdentity identity;
            NetworkIdentity.spawned.TryGetValue(netId, out identity);
            return identity;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVar<T>(T value, ref T fieldValue, ulong dirtyBit)
        {
            // newly initialized or changed value?
            if ((value == null && fieldValue != null) ||
                (value != null && !value.Equals(fieldValue)))
            {
                if (LogFilter.Debug) { Debug.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value); }
                SetDirtyBit(dirtyBit);
                fieldValue = value;
            }
        }

        // these are masks, not bit numbers, ie. 0x004 not 2
        public void SetDirtyBit(ulong dirtyBit)
        {
            m_SyncVarDirtyBits |= dirtyBit;
        }

        public void ClearAllDirtyBits()
        {
            m_LastSendTime = Time.time;
            m_SyncVarDirtyBits = 0L;

            // flush all unsynchronized changes in syncobjects
            m_SyncObjects.ForEach(obj => obj.Flush());
        }

        internal bool IsDirty()
        {
            if (Time.time - m_LastSendTime >= syncInterval)
            {
                if (m_SyncVarDirtyBits != 0L) {
                    return true;
                }

                for (var i = 0; i < m_SyncObjects.Count; i++) {
                    if (m_SyncObjects[i].IsDirty) {
                        return true;
                    }
                }
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
            for (int i = 0; i < m_SyncObjects.Count; i++)
            {
                SyncObject syncObject = m_SyncObjects[i];
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
            for (int i = 0; i < m_SyncObjects.Count; i++)
            {
                SyncObject syncObject = m_SyncObjects[i];
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
            for (int i = 0; i < m_SyncObjects.Count; i++)
            {
                SyncObject syncObject = m_SyncObjects[i];
                if (syncObject.IsDirty)
                {
                    syncObject.OnSerializeDelta(writer);
                    dirty = true;
                }
            }
            return dirty;
        }

        private void DeSerializeObjectsAll(NetworkReader reader)
        {
            for (int i = 0; i < m_SyncObjects.Count; i++)
            {
                SyncObject syncObject = m_SyncObjects[i];
                syncObject.OnDeserializeAll(reader);
            }
        }

        private void DeSerializeObjectsDelta(NetworkReader reader)
        {
            ulong dirty = reader.ReadPackedUInt64();
            for (int i = 0; i < m_SyncObjects.Count; i++)
            {
                SyncObject syncObject = m_SyncObjects[i];
                if ((dirty & (1UL << i)) != 0)
                {
                    syncObject.OnDeserializeDelta(reader);
                }
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        ///<summary>Called on clients when the server destroys the GameObject.</summary>
        public virtual void OnNetworkDestroy() {}
        ///<summary>Called when the GameObject spawns on the server, or when the server is started for GameObjects in the scene.</summary>
        public virtual void OnStartServer() {}
        ///<summary>Called when the GameObject spawns on the client, or when the client connects to a server for GameObject in the scene.</summary>
        public virtual void OnStartClient() {}
        ///<summary>Called on clients for GameObjects on the local client only.</summary>
        public virtual void OnStartLocalPlayer() {}
        ///<summary>Called when the GameObject starts with local player authority.</summary>
        public virtual void OnStartAuthority() {}
        ///<summary>Called when the GameObject stops with local player authority.</summary>
        public virtual void OnStopAuthority() {}

        // rebuild observers ourselves. otherwise it uses built in rebuild.
        ///<summary>Called on the server when the set of observers for a GameObject is rebuilt.</summary>
        ///<returns>Return true when overwriting so that Mirror knows that we wanted to.</returns>
        public virtual bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            return false;
        }

        ///<summary>Called on the client and/or server when the visibility of a GameObject changes for the local client.</summary>
        ///<param name="vis">New visibility state.</param>
        public virtual void OnSetLocalVisibility(bool vis)
        {
        }

        ///<summary>Called on the server to check visibility state for a new client.</summary>
        ///<param name="conn">Network connection of a player.</param>
        ///<returns>True if the player can see this object.</returns>
        public virtual bool OnCheckObserver(NetworkConnection conn)
        {
            return true;
        }
    }
}
