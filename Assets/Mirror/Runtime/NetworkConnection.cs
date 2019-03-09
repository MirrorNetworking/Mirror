using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnection : IDisposable
    {
        public HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>();

        Dictionary<int, NetworkMessageDelegate> m_MessageHandlers;

        public int connectionId = -1;
        public bool isReady;
        public string address;
        public float lastMessageTime;
        public NetworkIdentity playerController { get; private set; }
        public HashSet<uint> clientOwnedObjects;
        public bool logNetworkMessages;

        // this is always true for regular connections, false for local
        // connections because it's set in the constructor and never reset.
        [Obsolete("isConnected will be removed because it's pointless. A NetworkConnection is always connected.")]
        public bool isConnected { get; protected set; }

        // this is always 0 for regular connections, -1 for local
        // connections because it's set in the constructor and never reset.
        [Obsolete("hostId will be removed because it's not needed ever since we removed LLAPI as default. It's always 0 for regular connections and -1 for local connections. Use connection.GetType() == typeof(NetworkConnection) to check if it's a regular or local connection.")]
        public int hostId = -1;

        public NetworkConnection(string networkAddress)
        {
            address = networkAddress;
        }
        public NetworkConnection(string networkAddress, int networkConnectionId)
        {
            address = networkAddress;
            connectionId = networkConnectionId;
#pragma warning disable 618
            isConnected = true;
            hostId = 0;
#pragma warning restore 618
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
                    if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                    {
                        identity.clientAuthorityOwner = null;
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

            // paul:  we may be connecting or connected,  either way, we need to disconnect
            // transport should not do anything if it is not connecting/connected
            Transport.activeTransport.ClientDisconnect();

            // server? then disconnect that client
            if (Transport.activeTransport.ServerActive())
            {
                Transport.activeTransport.ServerDisconnect(connectionId);
            }

            // remove observers
            RemoveObservers();
        }

        internal void SetHandlers(Dictionary<int, NetworkMessageDelegate> handlers)
        {
            m_MessageHandlers = handlers;
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (m_MessageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType);
            }
            m_MessageHandlers[msgType] = handler;
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.Remove(msgType);
        }

        internal void SetPlayerController(NetworkIdentity player)
        {
            playerController = player;
        }

        internal void RemovePlayerController()
        {
            playerController = null;
        }

        [Obsolete("use Send<T> instead")]
        public virtual bool Send(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            // pack message and send
            byte[] message = MessagePacker.PackMessage(msgType, msg);
            return SendBytes(message, channelId);
        }

        public virtual bool Send<T>(T msg, int channelId = Channels.DefaultReliable) where T: MessageBase
        {
            // pack message and send
            byte[] message = MessagePacker.Pack(msg);
            return SendBytes(message, channelId);
        }

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        internal virtual bool SendBytes( byte[] bytes, int channelId = Channels.DefaultReliable)
        {
            if (logNetworkMessages) Debug.Log("ConnectionSend con:" + connectionId + " bytes:" + BitConverter.ToString(bytes));

            if (bytes.Length > Transport.activeTransport.GetMaxPacketSize(channelId))
            {
                Debug.LogError("NetworkConnection.SendBytes cannot send packet larger than " + Transport.activeTransport.GetMaxPacketSize(channelId) + " bytes");
                return false;
            }

            if (bytes.Length == 0)
            {
                // zero length packets getting into the packet queues are bad.
                Debug.LogError("NetworkConnection.SendBytes cannot send zero bytes");
                return false;
            }

            return TransportSend(channelId, bytes, out byte error);
        }

        public override string ToString()
        {
            return $"connectionId: {connectionId} isReady: {isReady}";
        }

        internal void AddToVisList(NetworkIdentity identity)
        {
            visList.Add(identity);

            // spawn identity for this conn
            NetworkServer.ShowForConnection(identity, this);
        }

        internal void RemoveFromVisList(NetworkIdentity identity, bool isDestroyed)
        {
            visList.Remove(identity);

            if (!isDestroyed)
            {
                // hide identity for this conn
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

        public bool InvokeHandlerNoData(int msgType)
        {
            return InvokeHandler(msgType, null);
        }

        public bool InvokeHandler(int msgType, NetworkReader reader)
        {
            if (m_MessageHandlers.TryGetValue(msgType, out NetworkMessageDelegate msgDelegate))
            {
                NetworkMessage message = new NetworkMessage
                {
                    msgType = msgType,
                    reader = reader,
                    conn = this
                };

                msgDelegate(message);
                return true;
            }
            Debug.LogError("Unknown message ID " + msgType + " connId:" + connectionId);
            return false;
        }

        public bool InvokeHandler<T>(T msg) where T : MessageBase
        {
            int msgType = MessagePacker.GetId<T>();
            byte[] data = MessagePacker.Pack(msg);
            return InvokeHandler(msgType, new NetworkReader(data));
        }

        // handle this message
        // note: original HLAPI HandleBytes function handled >1 message in a while loop, but this wasn't necessary
        //       anymore because NetworkServer/NetworkClient.Update both use while loops to handle >1 data events per
        //       frame already.
        //       -> in other words, we always receive 1 message per Receive call, never two.
        //       -> can be tested easily with a 1000ms send delay and then logging amount received in while loops here
        //          and in NetworkServer/Client Update. HandleBytes already takes exactly one.
        public virtual void TransportReceive(byte[] buffer)
        {
            // unpack message
            NetworkReader reader = new NetworkReader(buffer);
            if (MessagePacker.UnpackMessage(reader, out int msgType))
            {
                if (logNetworkMessages)
                {
                    Debug.Log("ConnectionRecv con:" + connectionId + " msgType:" + msgType + " content:" + BitConverter.ToString(buffer));
                }

                // try to invoke the handler for that message
                if (InvokeHandler(msgType, reader))
                {
                    lastMessageTime = Time.time;
                }
            }
            else Debug.LogError("HandleBytes UnpackMessage failed for: " + BitConverter.ToString(buffer));
        }

        public virtual bool TransportSend(int channelId, byte[] bytes, out byte error)
        {
            error = 0;
            if (Transport.activeTransport.ClientConnected())
            {
                return Transport.activeTransport.ClientSend(channelId, bytes);
            }
            else if (Transport.activeTransport.ServerActive())
            {
                return Transport.activeTransport.ServerSend(connectionId, channelId, bytes);
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
