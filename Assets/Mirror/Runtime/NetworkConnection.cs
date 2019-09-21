using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// A High level network connection. This is used for connections from client-to-server and for connection from server-to-client.
    /// </summary>
    /// <remarks>
    /// <para>A NetworkConnection corresponds to a specific connection for a host in the transport layer. It has a connectionId that is assigned by the transport layer and passed to the Initialize function.</para>
    /// <para>A NetworkClient has one NetworkConnection. A NetworkServerSimple manages multiple NetworkConnections. The NetworkServer has multiple "remote" connections and a "local" connection for the local client.</para>
    /// <para>The NetworkConnection class provides message sending and handling facilities. For sending data over a network, there are methods to send message objects, byte arrays, and NetworkWriter objects. To handle data arriving from the network, handler functions can be registered for message Ids, byte arrays can be processed by HandleBytes(), and NetworkReader object can be processed by HandleReader().</para>
    /// <para>NetworkConnection objects also act as observers for networked objects. When a connection is an observer of a networked object with a NetworkIdentity, then the object will be visible to corresponding client for the connection, and incremental state changes will be sent to the client.</para>
    /// <para>NetworkConnection objects can "own" networked game objects. Owned objects will be destroyed on the server by default when the connection is destroyed. A connection owns the player objects created by its client, and other objects with client-authority assigned to the corresponding client.</para>
    /// <para>There are many virtual functions on NetworkConnection that allow its behaviour to be customized. NetworkClient and NetworkServer can both be made to instantiate custom classes derived from NetworkConnection by setting their networkConnectionClass member variable.</para>
    /// </remarks>
    public class NetworkConnection : IDisposable
    {
        public readonly HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>();

        Dictionary<int, NetworkMessageDelegate> messageHandlers;

        /// <summary>
        /// Unique identifier for this connection that is assigned by the transport layer.
        /// </summary>
        /// <remarks>
        /// <para>On a server, this Id is unique for every connection on the server. On a client this Id is local to the client, it is not the same as the Id on the server for this connection.</para>
        /// <para>Transport layers connections begin at one. So on a client with a single connection to a server, the connectionId of that connection will be one. In NetworkServer, the connectionId of the local connection is zero.</para>
        /// <para>Clients do not know their connectionId on the server, and do not know the connectionId of other clients on the server.</para>
        /// </remarks>
        public int connectionId = -1;

        /// <summary>
        /// Flag that indicates the client has been authenticated.
        /// </summary>
        public bool isAuthenticated;

        /// <summary>
        /// General purpose object to hold authentication data, character selection, tokens, etc.
        /// associated with the connection for reference after Authentication completes.
        /// </summary>
        public object authenticationData;

        /// <summary>
        /// Flag that tells if the connection has been marked as "ready" by a client calling ClientScene.Ready().
        /// <para>This property is read-only. It is set by the system on the client when ClientScene.Ready() is called, and set by the system on the server when a ready message is received from a client.</para>
        /// <para>A client that is ready is sent spawned objects by the server and updates to the state of spawned objects. A client that is not ready is not sent spawned objects.</para>
        /// </summary>
        public bool isReady;

        /// <summary>
        /// The IP address / URL / FQDN associated with the connection.
        /// </summary>
        public string address;

        /// <summary>
        /// The last time that a message was received on this connection.
        /// <para>This includes internal system messages (such as Commands and ClientRpc calls) and user messages.</para>
        /// </summary>
        public float lastMessageTime;

        /// <summary>
        /// Obsolete: use <see cref="identity"/> instead
        /// </summary>
        [Obsolete("Use NetworkConnection.identity instead")]
        public NetworkIdentity playerController
        {
            get
            {
                return identity;
            }
            internal set
            {
                identity = value;
            }
        }

        /// <summary>
        /// The NetworkIdentity for this connection.
        /// </summary>
        public NetworkIdentity identity { get; internal set; }

        /// <summary>
        /// A list of the NetworkIdentity objects owned by this connection. This list is read-only.
        /// <para>This includes the player object for the connection - if it has localPlayerAutority set, and any objects spawned with local authority or set with AssignLocalAuthority.</para>
        /// <para>This list can be used to validate messages from clients, to ensure that clients are only trying to control objects that they own.</para>
        /// </summary>
        public readonly HashSet<uint> clientOwnedObjects = new HashSet<uint>();

        /// <summary>
        /// Setting this to true will log the contents of network message to the console.
        /// </summary>
        /// <remarks>
        /// <para>Warning: this can be a lot of data and can be very slow. Both incoming and outgoing messages are logged. The format of the logs is:</para>
        /// <para>ConnectionSend con:1 bytes:11 msgId:5 FB59D743FD120000000000 ConnectionRecv con:1 bytes:27 msgId:8 14F21000000000016800AC3FE090C240437846403CDDC0BD3B0000</para>
        /// <para>Note that these are application-level network messages, not protocol-level packets. There will typically be multiple network messages combined in a single protocol packet.</para>
        /// </remarks>
        public bool logNetworkMessages;

        // this is always true for regular connections, false for local
        // connections because it's set in the constructor and never reset.
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("isConnected will be removed because it's pointless. A NetworkConnection is always connected.")]
        public bool isConnected { get; protected set; }

        // this is always 0 for regular connections, -1 for local
        // connections because it's set in the constructor and never reset.
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("hostId will be removed because it's not needed ever since we removed LLAPI as default. It's always 0 for regular connections and -1 for local connections. Use connection.GetType() == typeof(NetworkConnection) to check if it's a regular or local connection.")]
        public int hostId = -1;

        /// <summary>
        /// Creates a new NetworkConnection with the specified address
        /// </summary>
        /// <param name="networkAddress"></param>
        public NetworkConnection(string networkAddress)
        {
            address = networkAddress;
        }

        /// <summary>
        /// Creates a new NetworkConnection with the specified address and connectionId
        /// </summary>
        /// <param name="networkAddress"></param>
        /// <param name="networkConnectionId"></param>
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

        /// <summary>
        /// Disposes of this connection, releasing channel buffers that it holds.
        /// </summary>
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

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
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

        /// <summary>
        /// Obsolete: Use NetworkClient/NetworkServer.RegisterHandler{T} instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient/NetworkServer.RegisterHandler<T> instead")]
        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (messageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType);
            }
            messageHandlers[msgType] = handler;
        }

        /// <summary>
        /// Obsolete: Use <see cref="NetworkClient.UnregisterHandler{T}"/> and <see cref="NetworkServer.UnregisterHandler{T}"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient/NetworkServer.UnregisterHandler<T> instead")]
        public void UnregisterHandler(short msgType)
        {
            messageHandlers.Remove(msgType);
        }

        /// <summary>
        /// Obsolete: use <see cref="Send{T}(T, int)"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("use Send<T> instead")]
        public virtual bool Send(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            // pack message and send
            byte[] message = MessagePacker.PackMessage(msgType, msg);
            return SendBytes(message, channelId);
        }

        /// <summary>
        /// This sends a network message with a message ID on the connection. This message is sent on channel zero, which by default is the reliable channel.
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="msg">The message to send.</param>
        /// <param name="channelId">The transport layer channel to send on.</param>
        /// <returns></returns>
        public virtual bool Send<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            // pack message and send
            byte[] message = MessagePacker.Pack(msg);
            NetworkDiagnostics.OnSend(msg, channelId, message.Length, 1);
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

        /// <summary>
        /// Obsolete: Use <see cref="InvokeHandler{T}(T)"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use InvokeHandler<T> instead")]
        public bool InvokeHandlerNoData(int msgType)
        {
            return InvokeHandler(msgType, null);
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

        /// <summary>
        /// This function invokes the registered handler function for a message.
        /// <para>Network connections used by the NetworkClient and NetworkServer use this function for handling network messages.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="msg">The message object to process.</param>
        /// <returns></returns>
        public bool InvokeHandler<T>(T msg) where T : IMessageBase
        {
            int msgType = MessagePacker.GetId<T>();
            byte[] data = MessagePacker.Pack(msg);
            return InvokeHandler(msgType, new NetworkReader(data));
        }

        // note: original HLAPI HandleBytes function handled >1 message in a while loop, but this wasn't necessary
        //       anymore because NetworkServer/NetworkClient Update both use while loops to handle >1 data events per
        //       frame already.
        //       -> in other words, we always receive 1 message per Receive call, never two.
        //       -> can be tested easily with a 1000ms send delay and then logging amount received in while loops here
        //          and in NetworkServer/Client Update. HandleBytes already takes exactly one.
        /// <summary>
        /// This virtual function allows custom network connection classes to process data from the network before it is passed to the application.
        /// </summary>
        /// <param name="buffer">The data recieved.</param>
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

        /// <summary>
        /// This virtual function allows custom network connection classes to process data send by the application before it goes to the network transport layer.
        /// </summary>
        /// <param name="channelId">Channel to send data on.</param>
        /// <param name="bytes">Data to send.</param>
        /// <returns></returns>
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
