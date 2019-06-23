using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnection : IDisposable
    {
        public readonly HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>();

        Dictionary<int, NetworkMessageDelegate> messageHandlers;

        public int connectionId = -1;
        public bool isReady;
        public string address;
        public float lastMessageTime;
        public NetworkIdentity playerController { get; internal set; }
        public readonly HashSet<uint> clientOwnedObjects = new HashSet<uint>();
        public bool logNetworkMessages;

        public NetworkConnection(string networkAddress)
        {
            address = networkAddress;
        }
        public NetworkConnection(string networkAddress, int networkConnectionId)
        {
            address = networkAddress;
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
            foreach (uint netId in clientOwnedObjects)
            {
                if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                {
                    identity.clientAuthorityOwner = null;
                }
            }
            clientOwnedObjects.Clear();
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

            // server? then disconnect that client (not for host local player though)
            if (Transport.activeTransport.ServerActive() && connectionId != 0)
            {
                Transport.activeTransport.ServerDisconnect(connectionId);
            }
            // not server and not host mode? then disconnect client
            else
            {
                Transport.activeTransport.ClientDisconnect();
            }

            RemoveObservers();
        }

        internal void SetHandlers(Dictionary<int, NetworkMessageDelegate> handlers)
        {
            messageHandlers = handlers;
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient/NetworkServer.RegisterHandler<T> instead")]
        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (messageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType);
            }
            messageHandlers[msgType] = handler;
        }

        public virtual bool Send<T>(T msg, int channelId = Channels.DefaultReliable) where T: IMessageBase
        {
            // pack message and send
            byte[] message = MessagePacker.Pack(msg);
            return SendBytes(message, channelId);
        }

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        internal virtual bool SendBytes(byte[] bytes, int channelId = Channels.DefaultReliable)
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

            return TransportSend(channelId, bytes);
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

        internal bool InvokeHandler(int msgType, NetworkReader reader)
        {
            if (messageHandlers.TryGetValue(msgType, out NetworkMessageDelegate msgDelegate))
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

        public bool InvokeHandler<T>(T msg) where T : IMessageBase
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
        public virtual void TransportReceive(ArraySegment<byte> buffer)
        {
            // unpack message
            NetworkReader reader = new NetworkReader(buffer);
            if (MessagePacker.UnpackMessage(reader, out int msgType))
            {
                // logging
                if (logNetworkMessages) Debug.Log("ConnectionRecv con:" + connectionId + " msgType:" + msgType + " content:" + BitConverter.ToString(buffer.Array, buffer.Offset, buffer.Count));

                // try to invoke the handler for that message
                if (InvokeHandler(msgType, reader))
                {
                    lastMessageTime = Time.time;
                }
            }
            else
            {
                Debug.LogError("Closed connection: " + connectionId + ". Invalid message header.");
                Disconnect();
            }
        }

        public virtual bool TransportSend(int channelId, byte[] bytes)
        {
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
            clientOwnedObjects.Add(obj.netId);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Remove(obj.netId);
        }
    }
}
