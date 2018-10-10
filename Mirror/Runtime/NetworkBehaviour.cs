using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    // provide an interface so that we can mock it in unit tests
    public interface INetworkBehaviour
    {
        bool isServer { get; }
        bool isClient { get; }
    }


    [RequireComponent(typeof(NetworkIdentity))]
    [AddComponentMenu("")]
    public class NetworkBehaviour : MonoBehaviour, INetworkBehaviour
    {
        ulong m_SyncVarDirtyBits; // ulong instead of uint for 64 instead of 32 SyncVar limit per component
        float m_LastSendTime;

        // this prevents recursion when SyncVar hook functions are called.
        bool m_SyncVarGuard;

        public bool localPlayerAuthority { get { return myView.localPlayerAuthority; } }
        public bool isServer { get { return myView.isServer; } }
        public bool isClient { get { return myView.isClient; } }
        public bool isLocalPlayer { get { return myView.isLocalPlayer; } }
        public bool hasAuthority { get { return myView.hasAuthority; } }
        public NetworkInstanceId netId { get { return myView.netId; } }
        public NetworkConnection connectionToServer { get { return myView.connectionToServer; } }
        public NetworkConnection connectionToClient { get { return myView.connectionToClient; } }
        protected ulong syncVarDirtyBits { get { return m_SyncVarDirtyBits; } }
        protected bool syncVarHookGuard { get { return m_SyncVarGuard; } set { m_SyncVarGuard = value; }}

        internal NetworkIdentity netIdentity { get { return myView; } }

        // objects that can synchronize themselves,  such as synclists
        protected readonly List<SyncObject> m_SyncObjects = new List<SyncObject>();

        const float k_DefaultSendInterval = 0.1f;

        NetworkIdentity m_MyView;
        NetworkIdentity myView
        {
            get
            {
                if (m_MyView == null)
                {
                    m_MyView = GetComponent<NetworkIdentity>();
                    if (m_MyView == null)
                    {
                        if (LogFilter.logError) { Debug.LogError("There is no NetworkIdentity on this object. Please add one."); }
                    }
                    return m_MyView;
                }
                return m_MyView;
            }
        }

        // ----------------------------- Commands --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendCommandInternal(int cmdHash, NetworkWriter writer, string cmdName)
        {
            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(isLocalPlayer || hasAuthority))
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Trying to send command for object without authority."); }
                return;
            }

            if (ClientScene.readyConnection == null)
            {
                if (LogFilter.logError) { Debug.LogError("Send command attempted with no client running [client=" + connectionToServer + "]."); }
                return;
            }

            // construct the message
            CommandMessage message = new CommandMessage();
            message.netId = netId;
            message.cmdHash = cmdHash;
            message.payload = writer.ToArray();

            ClientScene.readyConnection.Send((short)MsgType.Command, message);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeCommand(int cmdHash, NetworkReader reader)
        {
            return InvokeCommandDelegate(cmdHash, reader);
        }

        // ----------------------------- Client RPCs --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(int rpcHash, NetworkWriter writer, string rpcName)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ClientRpc call on un-spawned object"); }
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage();
            message.netId = netId;
            message.rpcHash = rpcHash;
            message.payload = writer.ToArray();

            NetworkServer.SendToReady(gameObject, (short)MsgType.Rpc, message);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, int rpcHash, NetworkWriter writer, string rpcName)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("TargetRpc call on un-spawned object"); }
                return;
            }

            // construct the message
            RpcMessage message = new RpcMessage();
            message.netId = netId;
            message.rpcHash = rpcHash;
            message.payload = writer.ToArray();

            conn.Send((short)MsgType.Rpc, message);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeRPC(int cmdHash, NetworkReader reader)
        {
            return InvokeRpcDelegate(cmdHash, reader);
        }

        // ----------------------------- Sync Events --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendEventInternal(int eventHash, NetworkWriter writer, string eventName)
        {
            if (!NetworkServer.active)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("SendEvent no server?"); }
                return;
            }

            // construct the message
            SyncEventMessage message = new SyncEventMessage();
            message.netId = netId;
            message.eventHash = eventHash;
            message.payload = writer.ToArray();

            NetworkServer.SendToReady(gameObject, (short)MsgType.SyncEvent, message);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int cmdHash, NetworkReader reader)
        {
            return InvokeSyncEventDelegate(cmdHash, reader);
        }


        // ----------------------------- Code Gen Path Helpers  --------------------------------

        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);
        protected delegate void EventDelegate(List<Delegate> targets, NetworkReader reader);

        protected enum UNetInvokeType
        {
            Command,
            ClientRpc,
            SyncEvent
        };

        protected class Invoker
        {
            public UNetInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;

            public string DebugString()
            {
                return invokeType + ":" + invokeClass + ":" + invokeFunction.GetMethodName();
            }
        };

        static Dictionary<int, Invoker> s_CmdHandlerDelegates = new Dictionary<int, Invoker>();

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterCommandDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.Command;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterCommandDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterRpcDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.ClientRpc;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterRpcDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterEventDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.SyncEvent;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterEventDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        internal static string GetInvoker(int cmdHash)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return null;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            return inv.DebugString();
        }

        // wrapper fucntions for each type of network operation
        internal static bool GetInvokerForHashCommand(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.Command, out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashClientRpc(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.ClientRpc, out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashSyncEvent(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncEvent, out invokeClass, out invokeFunction);
        }

        static bool GetInvokerForHash(int cmdHash, UNetInvokeType invokeType, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            Invoker invoker = null;
            if (!s_CmdHandlerDelegates.TryGetValue(cmdHash, out invoker))
            {
                if (LogFilter.logDev) { Debug.Log("GetInvokerForHash hash:" + cmdHash + " not found"); }
                invokeClass = null;
                invokeFunction = null;
                return false;
            }

            if (invoker == null)
            {
                if (LogFilter.logDev) { Debug.Log("GetInvokerForHash hash:" + cmdHash + " invoker null"); }
                invokeClass = null;
                invokeFunction = null;
                return false;
            }

            if (invoker.invokeType != invokeType)
            {
                if (LogFilter.logError) { Debug.LogError("GetInvokerForHash hash:" + cmdHash + " mismatched invokeType"); }
                invokeClass = null;
                invokeFunction = null;
                return false;
            }

            invokeClass = invoker.invokeClass;
            invokeFunction = invoker.invokeFunction;
            return true;
        }

        internal static void DumpInvokers()
        {
            Debug.Log("DumpInvokers size:" + s_CmdHandlerDelegates.Count);
            foreach (var inv in s_CmdHandlerDelegates)
            {
                Debug.Log("  Invoker:" + inv.Value.invokeClass + ":" + inv.Value.invokeFunction.GetMethodName() + " " + inv.Value.invokeType + " " + inv.Key);
            }
        }

        internal bool ContainsCommandDelegate(int cmdHash)
        {
            return s_CmdHandlerDelegates.ContainsKey(cmdHash);
        }

        internal bool InvokeCommandDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.Command)
            {
                return false;
            }

            // 'this' instance of invokeClass?
            if (inv.invokeClass.IsInstanceOfType(this))
            {
                inv.invokeFunction(this, reader);
                return true;
            }
            return false;
        }

        internal bool InvokeRpcDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.ClientRpc)
            {
                return false;
            }

            // 'this' instance of invokeClass?
            if (inv.invokeClass.IsInstanceOfType(this))
            {
                inv.invokeFunction(this, reader);
                return true;
            }
            return false;
        }

        internal bool InvokeSyncEventDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.SyncEvent)
            {
                return false;
            }

            inv.invokeFunction(this, reader);
            return true;
        }

        internal static string GetCmdHashHandlerName(int cmdHash)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return cmdHash.ToString();
            }
            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            return inv.invokeType + ":" + inv.invokeFunction.GetMethodName();
        }

        static string GetCmdHashPrefixName(int cmdHash, string prefix)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return cmdHash.ToString();
            }
            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            string name = inv.invokeFunction.GetMethodName();

            int index = name.IndexOf(prefix);
            if (index > -1)
            {
                name = name.Substring(prefix.Length);
            }
            return name;
        }

        internal static string GetCmdHashCmdName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeCmd");
        }

        internal static string GetCmdHashRpcName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeRpc");
        }

        internal static string GetCmdHashEventName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeSyncEvent");
        }


        // ----------------------------- Helpers  --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref NetworkInstanceId netIdField)
        {
            if (m_SyncVarGuard)
                return;

            NetworkInstanceId newGameObjectNetId = new NetworkInstanceId();
            if (newGameObject != null)
            {
                var uv = newGameObject.GetComponent<NetworkIdentity>();
                if (uv != null)
                {
                    newGameObjectNetId = uv.netId;
                    if (newGameObjectNetId.IsEmpty())
                    {
                        if (LogFilter.logWarn) { Debug.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?"); }
                    }
                }
            }

            NetworkInstanceId oldGameObjectNetId = new NetworkInstanceId();
            if (gameObjectField != null)
            {
                oldGameObjectNetId = gameObjectField.GetComponent<NetworkIdentity>().netId;
            }

            if (newGameObjectNetId != oldGameObjectNetId)
            {
                if (LogFilter.logDev) { Debug.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + oldGameObjectNetId + "->" + newGameObjectNetId); }
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
                if (LogFilter.logDev) { Debug.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value); }
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
            for (int i = 0; i < m_SyncObjects.Count; i++)
            {
                SyncObject syncObject = m_SyncObjects[i];
                syncObject.Flush();
            }
        }

        internal bool IsDirty()
        {
            if (Time.time - m_LastSendTime > GetNetworkSendInterval())
            {
                // is any syncobject dirty?
                for (int i = 0; i < m_SyncObjects.Count; i++)
                {
                    SyncObject syncObject = m_SyncObjects[i];
                    if (syncObject.IsDirty)
                    {
                        return true;
                    }
                }
                return m_SyncVarDirtyBits != 0L;
            }
            return false;
        }

        public virtual bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (!initialState)
            {
                writer.WritePackedUInt64(0);
            }
            return SerializeObjects(writer, initialState);
        }

        public virtual void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (!initialState)
            {
                reader.ReadPackedUInt64();
            }
            DeSerializeObjects(reader, initialState);
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

        public bool SerializeObjects(NetworkWriter writer, bool initialState)
        {
            bool dirty = false;

            if (initialState)
            {
                for (int i = 0; i < m_SyncObjects.Count; i++)
                {
                    SyncObject syncObject = m_SyncObjects[i];
                    syncObject.OnSerializeAll(writer);
                    dirty = true;
                }
            }
            else
            {
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
            }
            return dirty;
        }

        private void DeSerializeObjects(NetworkReader reader, bool initialState)
        {
            // serializable objects, such as synclists
            if (initialState)
            {
                for (int i = 0; i < m_SyncObjects.Count; i++)
                {
                    SyncObject syncObject = m_SyncObjects[i];
                    syncObject.OnDeserializeAll(reader);
                }
            }
            else
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
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void PreStartClient()
        {
        }

        public virtual void OnNetworkDestroy()
        {
        }

        public virtual void OnStartServer()
        {
        }

        public virtual void OnStartClient()
        {
        }

        public virtual void OnStartLocalPlayer()
        {
        }

        public virtual void OnStartAuthority()
        {
        }

        public virtual void OnStopAuthority()
        {
        }

        public virtual bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            return false;
        }

        public virtual void OnSetLocalVisibility(bool vis)
        {
        }

        public virtual bool OnCheckObserver(NetworkConnection conn)
        {
            return true;
        }

        public virtual float GetNetworkSendInterval()
        {
            return k_DefaultSendInterval;
        }
    }

}
