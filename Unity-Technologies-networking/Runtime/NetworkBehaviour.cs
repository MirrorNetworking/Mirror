#if ENABLE_UNET
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace UnityEngine.Networking
{
    [RequireComponent(typeof(NetworkIdentity))]
    [AddComponentMenu("")]
    public class NetworkBehaviour : MonoBehaviour
    {
        uint m_SyncVarDirtyBits;
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
        public short playerControllerId { get { return myView.playerControllerId; } }
        protected uint syncVarDirtyBits { get { return m_SyncVarDirtyBits; } }
        protected bool syncVarHookGuard { get { return m_SyncVarGuard; } set { m_SyncVarGuard = value; }}

        internal NetworkIdentity netIdentity { get { return myView; } }

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
        protected void SendCommandInternal(NetworkWriter writer, int channelId, string cmdName)
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

            writer.FinishMessage();
            ClientScene.readyConnection.SendWriter(writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.Command, cmdName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeCommand(int cmdHash, NetworkReader reader)
        {
            if (InvokeCommandDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Client RPCs --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(NetworkWriter writer, int channelId, string rpcName)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ClientRpc call on un-spawned object"); }
                return;
            }

            writer.FinishMessage();
            NetworkServer.SendWriterToReady(gameObject, writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.Rpc, rpcName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, NetworkWriter writer, int channelId, string rpcName)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("TargetRpc call on un-spawned object"); }
                return;
            }

            writer.FinishMessage();

            conn.SendWriter(writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.Rpc, rpcName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeRPC(int cmdHash, NetworkReader reader)
        {
            if (InvokeRpcDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Sync Events --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendEventInternal(NetworkWriter writer, int channelId, string eventName)
        {
            if (!NetworkServer.active)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("SendEvent no server?"); }
                return;
            }

            writer.FinishMessage();
            NetworkServer.SendWriterToReady(gameObject, writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.SyncEvent, eventName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int cmdHash, NetworkReader reader)
        {
            if (InvokeSyncEventDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Sync Lists --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncList(int cmdHash, NetworkReader reader)
        {
            if (InvokeSyncListDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Code Gen Path Helpers  --------------------------------

        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);
        protected delegate void EventDelegate(List<Delegate> targets, NetworkReader reader);

        protected enum UNetInvokeType
        {
            Command,
            ClientRpc,
            SyncEvent,
            SyncList
        };

        protected class Invoker
        {
            public UNetInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;

            public string DebugString()
            {
                return invokeType + ":" +
                    invokeClass + ":" +
                    invokeFunction.GetMethodName();
            }
        };

        static Dictionary<int, Invoker> s_CmdHandlerDelegates = new Dictionary<int, Invoker>();

        [EditorBrowsable(EditorBrowsableState.Never)]
        static protected void RegisterCommandDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
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
        static protected void RegisterRpcDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
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
        static protected void RegisterEventDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        static protected void RegisterSyncListDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.SyncList;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterSyncListDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
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

        internal static bool GetInvokerForHashSyncList(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncList, out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashSyncEvent(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncEvent, out invokeClass, out invokeFunction);
        }

        static bool GetInvokerForHash(int cmdHash, NetworkBehaviour.UNetInvokeType invokeType, out Type invokeClass, out CmdDelegate invokeFunction)
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

            if (GetType() != inv.invokeClass)
            {
                if (GetType().IsSubclassOf(inv.invokeClass))
                {
                    // allowed, commands function is on a base class.
                }
                else
                {
                    return false;
                }
            }

            inv.invokeFunction(this, reader);
            return true;
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

            if (GetType() != inv.invokeClass)
            {
                if (GetType().IsSubclassOf(inv.invokeClass))
                {
                    // allowed, rpc function is on a base class.
                }
                else
                {
                    return false;
                }
            }

            inv.invokeFunction(this, reader);
            return true;
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

        internal bool InvokeSyncListDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.SyncList)
            {
                return false;
            }

            if (GetType() != inv.invokeClass)
            {
                return false;
            }

            inv.invokeFunction(this, reader);
            return true;
        }

        static internal string GetCmdHashHandlerName(int cmdHash)
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
            var name = inv.invokeFunction.GetMethodName();

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

        internal static string GetCmdHashListName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeSyncList");
        }

        // ----------------------------- Helpers  --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, uint dirtyBit, ref NetworkInstanceId netIdField)
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
        protected void SetSyncVar<T>(T value, ref T fieldValue, uint dirtyBit)
        {
            bool changed = false;
            if (value == null)
            {
                if (fieldValue != null)
                    changed = true;
            }
            else
            {
                changed = !value.Equals(fieldValue);
            }
            if (changed)
            {
                if (LogFilter.logDev) { Debug.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value); }
                SetDirtyBit(dirtyBit);
                fieldValue = value;
            }
        }

        // these are masks, not bit numbers, ie. 0x004 not 2
        public void SetDirtyBit(uint dirtyBit)
        {
            m_SyncVarDirtyBits |= dirtyBit;
        }

        public void ClearAllDirtyBits()
        {
            m_LastSendTime = Time.time;
            m_SyncVarDirtyBits = 0;
        }

        internal int GetDirtyChannel()
        {
            if (Time.time - m_LastSendTime > GetNetworkSendInterval())
            {
                if (m_SyncVarDirtyBits != 0)
                {
                    return GetNetworkChannel();
                }
            }
            return -1;
        }

        public virtual bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (!initialState)
            {
                writer.WritePackedUInt32(0);
            }
            return false;
        }

        public virtual void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (!initialState)
            {
                reader.ReadPackedUInt32();
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

        public virtual int GetNetworkChannel()
        {
            return Channels.DefaultReliable;
        }

        public virtual float GetNetworkSendInterval()
        {
            return k_DefaultSendInterval;
        }
    }
}
#endif //ENABLE_UNET
