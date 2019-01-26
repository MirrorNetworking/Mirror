using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnection : IDisposable
    {
        NetworkIdentity m_PlayerController;
        public HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>();

        Dictionary<short, NetworkMessageDelegate> m_MessageHandlers;

        public int hostId = -1;
        public int connectionId = -1;
        public bool isReady;
        public string address;
        public float lastMessageTime;
        public NetworkIdentity playerController { get { return m_PlayerController; } }
        public HashSet<uint> clientOwnedObjects;
        public bool logNetworkMessages;
        public bool isConnected { get { return hostId != -1; }}

        public NetworkConnection(string networkAddress)
        {
            address = networkAddress;
        }
        public NetworkConnection(string networkAddress, int networkHostId, int networkConnectionId)
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
            if (clientOwnedObjects != null)
            {
                foreach (uint netId in clientOwnedObjects)
                {
                    NetworkIdentity identity;
                    if (NetworkIdentity.spawned.TryGetValue(netId, out identity))
                    {
                        identity.ClearClientOwner();
                    }
                }
            }
            clientOwnedObjects = null;
        }

        public void Disconnect()
        {
            // don't clear address so we can still access it in NetworkManager.OnServerDisconnect
            // => it's reset in Initialize anyway and there is no address empty check anywhere either
            //address = "";

            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            ClientScene.HandleClientDisconnect(this);

            // client? then stop transport
            if (NetworkManager.singleton.transport.ClientConnected())
            {
                NetworkManager.singleton.transport.ClientDisconnect();
            }
            // server? then disconnect that client
            else if (NetworkManager.singleton.transport.ServerActive())
            {
                NetworkManager.singleton.transport.ServerDisconnect(connectionId);
            }

            // remove observers. original HLAPI has hostId check for that too.
            if (hostId != -1)
            {
                RemoveObservers();
            }
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
            Debug.LogError("NetworkConnection InvokeHandler no handler for " + msgType);
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
                if (LogFilter.Debug) { Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType); }
            }
            m_MessageHandlers[msgType] = handler;
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.Remove(msgType);
        }

        internal void SetPlayerController(NetworkIdentity player)
        {
            m_PlayerController = player;
        }

        internal void RemovePlayerController()
        {
            m_PlayerController = null;
        }

        public virtual bool Send(short msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            NetworkWriter writer = new NetworkWriter();
            msg.Serialize(writer);

            // pack message and send
            byte[] message = Protocol.PackMessage((ushort)msgType, writer.ToArray());
            return SendBytes(message, channelId);
        }

        // protected because no one except NetworkConnection should ever send bytes directly to the client, as they
        // would be detected as some kind of message. send messages instead.
        protected virtual bool SendBytes( byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            if (logNetworkMessages) { Debug.Log("ConnectionSend con:" + connectionId + " bytes:" + BitConverter.ToString(bytes)); }

            if (bytes.Length > NetworkManager.singleton.transport.GetMaxPacketSize(channelId))
            {
                Debug.LogError("NetworkConnection:SendBytes cannot send packet larger than " + NetworkManager.singleton.transport.GetMaxPacketSize(channelId) + " bytes");
                return false;
            }

            if (bytes.Length == 0)
            {
                // zero length packets getting into the packet queues are bad.
                Debug.LogError("NetworkConnection:SendBytes cannot send zero bytes");
                return false;
            }

            byte error;
            return TransportSend(channelId, bytes, out error);
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
                if (logNetworkMessages)
                {
                    if (Enum.IsDefined(typeof(MsgType), msgType))
                    {
                        // one of Mirror mesage types,  display the message name
                        Debug.Log("ConnectionRecv con:" + connectionId + " msgType:" + (MsgType)msgType + " content:" + BitConverter.ToString(content));
                    }
                    else
                    {
                        // user defined message,  display the number
                        Debug.Log("ConnectionRecv con:" + connectionId + " msgType:" + msgType + " content:" + BitConverter.ToString(content));
                    }
                }

                NetworkMessageDelegate msgDelegate;
                if (m_MessageHandlers.TryGetValue((short)msgType, out msgDelegate))
                {
                    // create message here instead of caching it. so we can add it to queue more easily.
                    NetworkMessage msg = new NetworkMessage();
                    msg.msgType = (short)msgType;
                    msg.reader = new NetworkReader(content);
                    msg.conn = this;

                    msgDelegate(msg);
                    lastMessageTime = Time.time;
                }
                else
                {
                    //NOTE: this throws away the rest of the buffer. Need moar error codes
                    Debug.LogError("Unknown message ID " + msgType + " connId:" + connectionId);
                }
            }
            else
            {
                Debug.LogError("HandleBytes UnpackMessage failed for: " + BitConverter.ToString(buffer));
            }
        }

        public override string ToString()
        {
            return string.Format("hostId: {0} connectionId: {1} isReady: {2}", hostId, connectionId, isReady);
        }

        internal void AddToVisList(NetworkIdentity identity)
        {
            visList.Add(identity);

            // spawn uv for this conn
            NetworkServer.ShowForConnection(identity, this);
        }

        internal void RemoveFromVisList(NetworkIdentity identity, bool isDestroyed)
        {
            visList.Remove(identity);

            if (!isDestroyed)
            {
                // hide uv for this conn
                NetworkServer.HideForConnection(identity, this);
            }
        }

        internal void RemoveObservers()
        {
            foreach (NetworkIdentity identity in visList)
            {
                identity.RemoveObserverInternal(this);
            }
            visList.Clear();
        }

        public virtual void TransportReceive(byte[] bytes)
        {
            HandleBytes(bytes);
        }

        public virtual bool TransportSend(int channelId, byte[] bytes, out byte error)
        {
            error = 0;
            if (NetworkManager.singleton.transport.ClientConnected())
            {
                return NetworkManager.singleton.transport.ClientSend(channelId, bytes);
            }
            else if (NetworkManager.singleton.transport.ServerActive())
            {
                return NetworkManager.singleton.transport.ServerSend(connectionId, channelId, bytes);
            }
            return false;
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            if (clientOwnedObjects == null)
            {
                clientOwnedObjects = new HashSet<uint>();
            }
            clientOwnedObjects.Add(obj.netId);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            if (clientOwnedObjects == null)
            {
                return;
            }
            clientOwnedObjects.Remove(obj.netId);
        }
    }
}
