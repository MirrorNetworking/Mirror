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

        Dictionary<short, NetworkMessageDelegate> m_MessageHandlers;

        HashSet<NetworkInstanceId> m_ClientOwnedObjects;

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

        public virtual void Initialize(string networkAddress, int networkHostId, int networkConnectionId)
        {
            address = networkAddress;
            hostId = networkHostId;
            connectionId = networkConnectionId;
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

            Transport.layer.ClientDisconnect();

            RemoveObservers();
        }

        internal void SetHandlers(Dictionary<short, NetworkMessageDelegate> handlers)
        {
            m_MessageHandlers = handlers;
        }

        public bool InvokeHandlerNoData(short msgType)
        {
            return InvokeHandler(msgType, null);
        }

        public bool InvokeHandler(short msgType, NetworkReader reader)
        {
            NetworkMessageDelegate msgDelegate;
            if (m_MessageHandlers.TryGetValue(msgType, out msgDelegate))
            {
                NetworkMessage message = new NetworkMessage();
                message.msgType = msgType;
                message.conn = this;
                message.reader = reader;

                msgDelegate(message);
                return true;
            }
            if (LogFilter.logError) { Debug.LogError("NetworkConnection InvokeHandler no handler for " + msgType); }
            return false;
        }

        public bool InvokeHandler(NetworkMessage netMsg)
        {
            NetworkMessageDelegate msgDelegate;
            if (m_MessageHandlers.TryGetValue(netMsg.msgType, out msgDelegate))
            {
                msgDelegate(netMsg);
                return true;
            }
            return false;
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (m_MessageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.logDebug) { Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType); }
            }
            m_MessageHandlers[msgType] = handler;
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.Remove(msgType);
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

        public virtual bool Send(short msgType, MessageBase msg)
        {
            NetworkWriter writer = new NetworkWriter();
            msg.Serialize(writer);

            // pack message and send
            byte[] message = Protocol.PackMessage((ushort)msgType, writer.ToArray());
            return SendBytes(message);
        }

        // protected because no one except NetworkConnection should ever send bytes directly to the client, as they
        // would be detected as some kind of message. send messages instead.
        protected virtual bool SendBytes(byte[] bytes)
        {
            if (logNetworkMessages) { Debug.Log("ConnectionSend con:" + connectionId + " bytes:" + BitConverter.ToString(bytes)); }

            if (bytes.Length > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkConnection:SendBytes cannot send packet larger than " + UInt16.MaxValue + " bytes"); }
                return false;
            }

            if (bytes.Length == 0)
            {
                // zero length packets getting into the packet queues are bad.
                if (LogFilter.logError) { Debug.LogError("NetworkConnection:SendBytes cannot send zero bytes"); }
                return false;
            }

            byte error;
            return TransportSend(bytes, out error);
        }

        // handle this message
        // note: original HLAPI HandleBytes function handled >1 message in a while loop, but this wasn't necessary
        //       anymore because NetworkServer/NetworkClient.Update both use while loops to handle >1 data events per
        //       frame already.
        //       -> in other words, we always receive 1 message per Receive call, never two.
        //       -> can be tested easily with a 1000ms send delay and then logging amount received in while loops here
        //          and in NetworkServer/Client Update. HandleBytes already takes exactly one.
        protected void HandleBytes(byte[] buffer)
        {
            // unpack message
            ushort msgType;
            byte[] content;
            if (Protocol.UnpackMessage(buffer, out msgType, out content))
            {
                if (logNetworkMessages) { Debug.Log("ConnectionRecv con:" + connectionId + " msgType:" + msgType + " content:" + BitConverter.ToString(content)); }

                NetworkMessageDelegate msgDelegate;
                if (m_MessageHandlers.TryGetValue((short)msgType, out msgDelegate))
                {
                    // create message here instead of caching it. so we can add it to queue more easily.
                    NetworkMessage msg = new NetworkMessage();
                    msg.msgType = (short)msgType;
                    msg.reader = new NetworkReader(content);
                    msg.conn = this;

                    // add to queue while paused, otherwise process directly
                    if (pauseQueue != null)
                    {
                        pauseQueue.Enqueue(msg);
                        if (LogFilter.logWarn) { Debug.LogWarning("HandleReader: added message to pause queue: " + msgType + " str=" + ((MsgType)msgType) + " queue size=" + pauseQueue.Count); }
                    }
                    else
                    {
                        msgDelegate(msg);
                    }
                    lastMessageTime = Time.time;
                }
                else
                {
                    //NOTE: this throws away the rest of the buffer. Need moar error codes
                    if (LogFilter.logError) { Debug.LogError("Unknown message ID " + msgType + " connId:" + connectionId); }
                }
            }
            else
            {
                if (LogFilter.logError) { Debug.LogError("HandleBytes UnpackMessage failed for: " + BitConverter.ToString(buffer)); }
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

        public virtual void TransportReceive(byte[] bytes)
        {
            HandleBytes(bytes);
        }

        public virtual bool TransportSend(byte[] bytes, out byte error)
        {
            error = 0;
            if (Transport.layer.ClientConnected())
            {
                Transport.layer.ClientSend(bytes);
                return true;
            }
            else if (Transport.layer.ServerActive())
            {
                Transport.layer.ServerSend(connectionId, bytes);
                return true;
            }
            return false;
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
                    if (LogFilter.logWarn) { Debug.LogWarning("processing queued message: " + msg.msgType + " str=" + msg.msgType); }
                    var msgDelegate = m_MessageHandlers[msg.msgType];
                    msgDelegate(msg);
                }
                pauseQueue = null;
            }
        }
    }
}
#endif //ENABLE_UNET
