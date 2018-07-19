#if ENABLE_UNET
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace UnityEngine.Networking
{
    /*
    * wire protocol is a list of :   size   |  msgType     | payload
    *                               (short)  (variable)   (buffer)
    */
    public class NetworkConnection : IDisposable
    {
        List<PlayerController> m_PlayerControllers = new List<PlayerController>();
        HashSet<NetworkIdentity> m_VisList = new HashSet<NetworkIdentity>();
        internal HashSet<NetworkIdentity> visList { get { return m_VisList; } }

        Dictionary<short, NetworkMessageDelegate> m_MessageHandlersDict;
        NetworkMessageHandlers m_MessageHandlers;

        HashSet<NetworkInstanceId> m_ClientOwnedObjects;
        NetworkMessage m_MessageInfo = new NetworkMessage();

        const int k_MaxMessageLogSize = 150;
        private NetworkError error;

        public int hostId = -1;
        public int connectionId = -1;
        public bool isReady;
        public string address;
        public float lastMessageTime;
        public List<PlayerController> playerControllers { get { return m_PlayerControllers; } }
        public HashSet<NetworkInstanceId> clientOwnedObjects { get { return m_ClientOwnedObjects; } }
        public bool logNetworkMessages = false;
        public bool isConnected { get { return hostId != -1; }}

        public NetworkError lastError { get { return error; } internal set { error = value; } }

        public virtual void Initialize(string networkAddress, int networkHostId, int networkConnectionId, HostTopology hostTopology)
        {
            address = networkAddress;
            hostId = networkHostId;
            connectionId = networkConnectionId;

            if ((hostTopology.DefaultConfig.UsePlatformSpecificProtocols) && (Application.platform != RuntimePlatform.PS4) && (Application.platform != RuntimePlatform.PSP2))
                throw new ArgumentOutOfRangeException("Platform specific protocols are not supported on this platform");
        }

        ~NetworkConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            // Take yourself off the Finalization queue
            // to prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_ClientOwnedObjects != null)
            {
                foreach (var netId in m_ClientOwnedObjects)
                {
                    var obj = NetworkServer.FindLocalObject(netId);
                    if (obj != null)
                    {
                        obj.GetComponent<NetworkIdentity>().ClearClientOwner();
                    }
                }
            }
            m_ClientOwnedObjects = null;
        }

        public void Disconnect()
        {
            // don't clear address so we can still access it in NetworkManager.OnServerDisconnect
            // => it's reset in Initialize anyway and there is no address empty check anywhere either
            //address = "";
            isReady = false;
            ClientScene.HandleClientDisconnect(this);
            if (hostId == -1)
            {
                return;
            }
            byte error;
            NetworkTransport.Disconnect(hostId, connectionId, out error);

            RemoveObservers();
        }

        internal void SetHandlers(NetworkMessageHandlers handlers)
        {
            m_MessageHandlers = handlers;
            m_MessageHandlersDict = handlers.GetHandlers();
        }

        public bool CheckHandler(short msgType)
        {
            return m_MessageHandlersDict.ContainsKey(msgType);
        }

        public bool InvokeHandlerNoData(short msgType)
        {
            return InvokeHandler(msgType, null, 0);
        }

        public bool InvokeHandler(short msgType, NetworkReader reader, int channelId)
        {
            if (m_MessageHandlersDict.ContainsKey(msgType))
            {
                m_MessageInfo.msgType = msgType;
                m_MessageInfo.conn = this;
                m_MessageInfo.reader = reader;
                m_MessageInfo.channelId = channelId;

                NetworkMessageDelegate msgDelegate = m_MessageHandlersDict[msgType];
                if (msgDelegate == null)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkConnection InvokeHandler no handler for " + msgType); }
                    return false;
                }
                msgDelegate(m_MessageInfo);
                return true;
            }
            return false;
        }

        public bool InvokeHandler(NetworkMessage netMsg)
        {
            if (m_MessageHandlersDict.ContainsKey(netMsg.msgType))
            {
                NetworkMessageDelegate msgDelegate = m_MessageHandlersDict[netMsg.msgType];
                msgDelegate(netMsg);
                return true;
            }
            return false;
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            m_MessageHandlers.RegisterHandler(msgType, handler);
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.UnregisterHandler(msgType);
        }

        internal void SetPlayerController(PlayerController player)
        {
            while (player.playerControllerId >= m_PlayerControllers.Count)
            {
                m_PlayerControllers.Add(new PlayerController());
            }

            m_PlayerControllers[player.playerControllerId] = player;
        }

        internal void RemovePlayerController(short playerControllerId)
        {
            int count = m_PlayerControllers.Count;
            while (count >= 0)
            {
                if (playerControllerId == count && playerControllerId == m_PlayerControllers[count].playerControllerId)
                {
                    m_PlayerControllers[count] = new PlayerController();
                    return;
                }
                count -= 1;
            }
            if (LogFilter.logError) { Debug.LogError("RemovePlayer player at playerControllerId " + playerControllerId + " not found"); }
        }

        // Get player controller from connection's list
        internal bool GetPlayerController(short playerControllerId, out PlayerController playerController)
        {
            playerController = playerControllers.Find(pc => pc.IsValid && pc.playerControllerId == playerControllerId);
            return playerController != null;
        }

        public virtual bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            NetworkWriter writer = new NetworkWriter();
            writer.StartMessage(msgType);
            msg.Serialize(writer);
            writer.FinishMessage();
            return SendWriter(writer, channelId);
        }
        public virtual bool Send(short msgType, MessageBase msg) { return SendByChannel(msgType, msg, Channels.DefaultReliable); }
        public virtual bool SendUnreliable(short msgType, MessageBase msg) { return SendByChannel(msgType, msg, Channels.DefaultUnreliable); }

        public virtual bool SendBytes(byte[] bytes, int bytesToSend, int channelId)
        {
            if (logNetworkMessages)
            {
                LogSend(bytes);
            }
            
#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.HLAPIMsg, "msg", 1);
#endif
            if (bytesToSend > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("ChannelBuffer:SendBytes cannot send packet larger than " + UInt16.MaxValue + " bytes"); }
                return false;
            }

            if (bytesToSend <= 0)
            {
                // zero length packets getting into the packet queues are bad.
                if (LogFilter.logError) { Debug.LogError("ChannelBuffer:SendBytes cannot send zero bytes"); }
                return false;
            }

            byte error;
            return TransportSend(bytes, bytesToSend, channelId, out error);
        }

        public virtual bool SendWriter(NetworkWriter writer, int channelId)
        {
            // write relevant data, which is until .Position
            return SendBytes(writer.ToArray(), writer.Position, channelId);
        }

        void LogSend(byte[] bytes)
        {
            NetworkReader reader = new NetworkReader(bytes);
            var msgSize = reader.ReadUInt16();
            var msgId = reader.ReadUInt16();

            const int k_PayloadStartPosition = 4;

            StringBuilder msg = new StringBuilder();
            for (int i = k_PayloadStartPosition; i < k_PayloadStartPosition + msgSize; i++)
            {
                msg.AppendFormat("{0:X2}", bytes[i]);
                if (i > k_MaxMessageLogSize) break;
            }
            Debug.Log("ConnectionSend con:" + connectionId + " bytes:" + msgSize + " msgId:" + msgId + " " + msg);
        }
        
        protected void HandleBytes(byte[] buffer, int receivedSize, int channelId)
        {
            // build the stream form the buffer passed in
            NetworkReader reader = new NetworkReader(buffer);
            HandleReader(reader, receivedSize, channelId);
        }

        protected void HandleReader(NetworkReader reader, int receivedSize, int channelId)
        {
            // read until size is reached.
            // NOTE: stream.Capacity is 1300, NOT the size of the available data
            while (reader.Position < receivedSize)
            {
                // the reader passed to user code has a copy of bytes from the real stream. user code never touches the real stream.
                // this ensures it can never get out of sync if user code reads less or more than the real amount.
                ushort sz = reader.ReadUInt16();
                short msgType = reader.ReadInt16();

                // create a reader just for this message
                byte[] msgBuffer = reader.ReadBytes(sz);
                NetworkReader msgReader = new NetworkReader(msgBuffer);

                if (logNetworkMessages)
                {
                    StringBuilder msg = new StringBuilder();
                    for (int i = 0; i < sz; i++)
                    {
                        msg.AppendFormat("{0:X2}", msgBuffer[i]);
                        if (i > k_MaxMessageLogSize) break;
                    }
                    Debug.Log("ConnectionRecv con:" + connectionId + " bytes:" + sz + " msgId:" + msgType + " " + msg);
                }

                NetworkMessageDelegate msgDelegate = null;
                if (m_MessageHandlersDict.ContainsKey(msgType))
                {
                    msgDelegate = m_MessageHandlersDict[msgType];
                }
                if (msgDelegate != null)
                {
                    // create message here instead of caching it. so we can add it to queue more easily.
                    NetworkMessage msg = new NetworkMessage();
                    msg.msgType = msgType;
                    msg.reader = msgReader;
                    msg.conn = this;
                    msg.channelId = channelId;

                    // add to queue while paused, otherwise process directly
                    if (pauseQueue != null)
                    {
                        pauseQueue.Enqueue(msg);
                        if (LogFilter.logWarn) { Debug.LogWarning("HandleReader: added message to pause queue: " + msgType + " str=" + MsgType.MsgTypeToString(msgType) + " queue size=" + pauseQueue.Count); }
                    }
                    else
                    {
                        msgDelegate(msg);
                    }
                    lastMessageTime = Time.time;

#if UNITY_EDITOR
                    UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                        MsgType.HLAPIMsg, "msg", 1);

                    if (msgType > MsgType.Highest)
                    {
                        UnityEditor.NetworkDetailStats.IncrementStat(
                            UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                            MsgType.UserMessage, msgType.ToString() + ":" + msgType.GetType().Name, 1);
                    }
#endif
                }
                else
                {
                    //NOTE: this throws away the rest of the buffer. Need moar error codes
                    if (LogFilter.logError) { Debug.LogError("Unknown message ID " + msgType + " connId:" + connectionId); }
                    break;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("hostId: {0} connectionId: {1} isReady: {2}", hostId, connectionId, isReady);
        }

        internal void AddToVisList(NetworkIdentity uv)
        {
            m_VisList.Add(uv);

            // spawn uv for this conn
            NetworkServer.ShowForConnection(uv, this);
        }

        internal void RemoveFromVisList(NetworkIdentity uv, bool isDestroyed)
        {
            m_VisList.Remove(uv);

            if (!isDestroyed)
            {
                // hide uv for this conn
                NetworkServer.HideForConnection(uv, this);
            }
        }

        internal void RemoveObservers()
        {
            foreach (var uv in m_VisList)
            {
                uv.RemoveObserverInternal(this);
            }
            m_VisList.Clear();
        }

        public virtual void TransportReceive(byte[] bytes, int numBytes, int channelId)
        {
            HandleBytes(bytes, numBytes, channelId);
        }

        public virtual bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
        {
            if (NetworkTransport.Send(hostId, connectionId, channelId, bytes, numBytes, out error))
            {
                return true;
            }
            else
            {
                // ChannelPacket used to log errors. we do it here now.
                if (LogFilter.logError) { Debug.LogError("SendToTransport failed. error:" + (NetworkError)error + " channel:" + channelId + " bytesToSend:" + numBytes); }
                return false;
            }
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            if (m_ClientOwnedObjects == null)
            {
                m_ClientOwnedObjects = new HashSet<NetworkInstanceId>();
            }
            m_ClientOwnedObjects.Add(obj.netId);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            if (m_ClientOwnedObjects == null)
            {
                return;
            }
            m_ClientOwnedObjects.Remove(obj.netId);
        }

        // vis2k: pause mode
        // problem: if we handle packets (calling the msgDelegates) while a scene load is in progress, then all the
        //          handled data and state will be lost as soon as the scene load is finished, causing state bugs.
        // solution: call Pause, message handling keeps messages in a queue, Resume handles them all.
        //
        // this is the only safe way to do it. otherwise all delegate functions have to check if a scene is loading,
        // which is way too complicated and risky.
        Queue<NetworkMessage> pauseQueue;

        internal void PauseHandling()
        {
            pauseQueue = new Queue<NetworkMessage>();
        }

        internal void ResumeHandling()
        {
            // pauseQueue is null if Resume called without pausing, make sure to only do something if paused before.
            if (pauseQueue != null)
            {
                foreach (NetworkMessage msg in pauseQueue)
                {
                    if (LogFilter.logWarn) { Debug.LogWarning("processing queued message: " + msg.msgType + " str=" + MsgType.MsgTypeToString(msg.msgType)); }
                    var msgDelegate = m_MessageHandlersDict[msg.msgType];
                    msgDelegate(msg);
                }
                pauseQueue = null;
            }
        }
    }
}
#endif //ENABLE_UNET
