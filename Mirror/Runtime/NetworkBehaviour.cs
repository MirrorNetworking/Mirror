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

        // this prevents recursion when SyncVar hook functions are called.
        bool m_SyncVarGuard;

        public bool localPlayerAuthority { get { return netIdentity.localPlayerAuthority; } }
        public bool isServer { get { return netIdentity.isServer; } }
        public bool isClient { get { return netIdentity.isClient; } }
        public bool isLocalPlayer { get { return netIdentity.isLocalPlayer; } }
        public bool isServerOnly { get { return isServer && !isClient; } }
        public bool isClientOnly { get { return isClient && !isServer; } }
        public bool hasAuthority { get { return netIdentity.hasAuthority; } }
        public uint netId { get { return netIdentity.netId; } }
        public NetworkConnection connectionToServer { get { return netIdentity.connectionToServer; } }
        public NetworkConnection connectionToClient { get { return netIdentity.connectionToClient; } }
        protected ulong syncVarDirtyBits { get { return m_SyncVarDirtyBits; } }
        protected bool syncVarHookGuard { get { return m_SyncVarGuard; } set { m_SyncVarGuard = value; }}

        // objects that can synchronize themselves,  such as synclists
        protected readonly List<SyncObject> m_SyncObjects = new List<SyncObject>();

        const float k_DefaultSendInterval = 0.1f;

        // NetworkIdentity component caching for easier access
        NetworkIdentity m_netIdentity;
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
        protected void SendCommandInternal(string cmdName, NetworkWriter writer, int channelId)
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
            message.cmdHash = cmdName.GetStableHashCode();
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
        protected void SendRPCInternal(string rpcName, NetworkWriter writer, int channelId)
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
            message.rpcHash = rpcName.GetStableHashCode();
            message.payload = writer.ToArray();

            NetworkServer.SendToReady(gameObject, (short)MsgType.Rpc, message, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, string rpcName, NetworkWriter writer, int channelId)
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
            message.rpcHash = rpcName.GetStableHashCode();
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
        protected void SendEventInternal(string eventName, NetworkWriter writer, int channelId)
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
            message.eventHash = eventName.GetStableHashCode();
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
            int cmdHash = cmdName.GetStableHashCode();
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref uint netIdField)
        {
            if (m_SyncVarGuard)
                return;

            uint newGameObjectNetId = 0;
            if (newGameObject != null)
            {
                NetworkIdentity identity = newGameObject.GetComponent<NetworkIdentity>();
                if (identity != null)
                {
                    newGameObjectNetId = identity.netId;
                    if (newGameObjectNetId == 0)
                    {
                        Debug.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?");
                    }
                }
            }

            uint oldGameObjectNetId = 0;
            if (gameObjectField != null)
            {
                oldGameObjectNetId = gameObjectField.GetComponent<NetworkIdentity>().netId;
            }

            if (newGameObjectNetId != oldGameObjectNetId)
            {
                if (LogFilter.Debug) { Debug.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + oldGameObjectNetId + "->" + newGameObjectNetId); }
                SetDirtyBit(dirtyBit);
                gameObjectField = newGameObject;
                netIdField = newGameObjectNetId;
            }
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
            if (Time.time - m_LastSendTime > GetNetworkSendInterval())
            {
                return m_SyncVarDirtyBits != 0L
                        || m_SyncObjects.Any(obj => obj.IsDirty);
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
        public virtual void PreStartClient() {}
        public virtual void OnNetworkDestroy() {}
        public virtual void OnStartServer() {}
        public virtual void OnStartClient() {}
        public virtual void OnStartLocalPlayer() {}
        public virtual void OnStartAuthority() {}
        public virtual void OnStopAuthority() {}

        public virtual float GetNetworkSendInterval()
        {
            return k_DefaultSendInterval;
        }
    }
}
