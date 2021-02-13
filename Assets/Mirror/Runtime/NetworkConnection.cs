using System;
using System.Collections.Generic;
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
    /// <para>There are many virtual functions on NetworkConnection that allow its behaviour to be customized. NetworkClient and NetworkServer can both be made to instantiate custom classes derived from NetworkConnection by setting their networkConnectionClass member variable.</para>
    /// </remarks>
    public abstract class NetworkConnection
    {
        public const int LocalConnectionId = 0;
        static readonly ILogger logger = LogFactory.GetLogger<NetworkConnection>();

        // internal so it can be tested
        internal readonly HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>();

        Dictionary<int, NetworkMessageDelegate> messageHandlers;

        /// <summary>
        /// Unique identifier for this connection that is assigned by the transport layer.
        /// </summary>
        /// <remarks>
        /// <para>On a server, this Id is unique for every connection on the server. On a client this Id is local to the client, it is not the same as the Id on the server for this connection.</para>
        /// <para>Transport layers connections begin at one. So on a client with a single connection to a server, the connectionId of that connection will be one. In NetworkServer, the connectionId of the local connection is zero.</para>
        /// <para>Clients do not know their connectionId on the server, and do not know the connectionId of other clients on the server.</para>
        /// </remarks>
        public readonly int connectionId;

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
        /// Can be useful for a game master to do IP Bans etc.
        /// </summary>
        public abstract string address { get; }

        /// <summary>
        /// The last time that a message was received on this connection.
        /// <para>This includes internal system messages (such as Commands and ClientRpc calls) and user messages.</para>
        /// </summary>
        public float lastMessageTime;

        /// <summary>
        /// The NetworkIdentity for this connection.
        /// </summary>
        public NetworkIdentity identity { get; internal set; }

        /// <summary>
        /// A list of the NetworkIdentity objects owned by this connection. This list is read-only.
        /// <para>This includes the player object for the connection - if it has localPlayerAutority set, and any objects spawned with local authority or set with AssignLocalAuthority.</para>
        /// <para>This list can be used to validate messages from clients, to ensure that clients are only trying to control objects that they own.</para>
        /// </summary>
        // IMPORTANT: this needs to be <NetworkIdentity>, not <uint netId>. fixes a bug where DestroyOwnedObjects wouldn't find
        //            the netId anymore: https://github.com/vis2k/Mirror/issues/1380 . Works fine with NetworkIdentity pointers though.
        public readonly HashSet<NetworkIdentity> clientOwnedObjects = new HashSet<NetworkIdentity>();

        /// <summary>
        /// Creates a new NetworkConnection
        /// </summary>
        internal NetworkConnection()
        {
            // set lastTime to current time when creating connection to make sure it isn't instantly kicked for inactivity
            lastMessageTime = Time.time;
        }

        /// <summary>
        /// Creates a new NetworkConnection with the specified connectionId
        /// </summary>
        /// <param name="networkConnectionId"></param>
        internal NetworkConnection(int networkConnectionId) : this()
        {
            connectionId = networkConnectionId;
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public abstract void Disconnect();

        internal void SetHandlers(Dictionary<int, NetworkMessageDelegate> handlers)
        {
            messageHandlers = handlers;
        }

        /// <summary>
        /// This sends a network message with a message ID on the connection. This message is sent on channel zero, which by default is the reliable channel.
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="msg">The message to send.</param>
        /// <param name="channelId">The transport layer channel to send on.</param>
        public void Send<T>(T msg, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message and send allocation free
                MessagePacker.Pack(msg, writer);
                NetworkDiagnostics.OnSend(msg, channelId, writer.Position, 1);
                Send(writer.ToArraySegment(), channelId);
            }
        }

        // validate packet size before sending. show errors if too big/small.
        // => it's best to check this here, we can't assume that all transports
        //    would check max size and show errors internally. best to do it
        //    in one place in hlapi.
        // => it's important to log errors, so the user knows what went wrong.
        protected internal static bool ValidatePacketSize(ArraySegment<byte> segment, int channelId)
        {
            if (segment.Count > Transport.activeTransport.GetMaxPacketSize(channelId))
            {
                logger.LogError("NetworkConnection.ValidatePacketSize: cannot send packet larger than " + Transport.activeTransport.GetMaxPacketSize(channelId) + " bytes");
                return false;
            }

            if (segment.Count == 0)
            {
                // zero length packets getting into the packet queues are bad.
                logger.LogError("NetworkConnection.ValidatePacketSize: cannot send zero bytes");
                return false;
            }

            // good size
            return true;
        }

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        internal abstract void Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable);

        public override string ToString()
        {
            return $"connection({connectionId})";
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

        // helper function
        protected bool UnpackAndInvoke(NetworkReader reader, int channelId)
        {
            if (MessagePacker.Unpack(reader, out int msgType))
            {
                // try to invoke the handler for that message
                if (messageHandlers.TryGetValue(msgType, out NetworkMessageDelegate msgDelegate))
                {
                    msgDelegate.Invoke(this, reader, channelId);
                    lastMessageTime = Time.time;
                    return true;
                }
                else
                {
                    if (logger.LogEnabled()) logger.Log("Unknown message ID " + msgType + " " + this + ". May be due to no existing RegisterHandler for this message.");
                    return false;
                }
            }
            else
            {
                logger.LogError("Closed connection: " + this + ". Invalid message header.");
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// This function allows custom network connection classes to process data from the network before it is passed to the application.
        /// </summary>
        /// <param name="buffer">The data received.</param>
        internal void TransportReceive(ArraySegment<byte> buffer, int channelId)
        {
            if (buffer.Count < MessagePacker.HeaderSize)
            {
                logger.LogError($"ConnectionRecv {this} Message was too short (messages should start with message id)");
                Disconnect();
                return;
            }

            // unpack message
            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(buffer))
            {
                // the other end might batch multiple messages into one packet.
                // we need to try to unpack multiple times.
                while (reader.Position < reader.Length)
                {
                    if (!UnpackAndInvoke(reader, channelId))
                        break;
                }
            }
        }

        /// <summary>
        /// Checks if client has sent a message within timeout
        /// <para>
        /// Some transports are unreliable at sending disconnect message to the server
        /// so this acts as a failsafe to make sure clients are kicked
        /// </para>
        /// <para>
        /// Client should send ping message to server every 2 seconds to keep this alive
        /// </para>
        /// </summary>
        /// <returns>True if server has recently received a message</returns>
        internal virtual bool IsAlive(float timeout) => Time.time - lastMessageTime < timeout;

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Add(obj);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Remove(obj);
        }

        internal void DestroyOwnedObjects()
        {
            // create a copy because the list might be modified when destroying
            HashSet<NetworkIdentity> tmp = new HashSet<NetworkIdentity>(clientOwnedObjects);
            foreach (NetworkIdentity netIdentity in tmp)
            {
                if (netIdentity != null)
                {
                    NetworkServer.Destroy(netIdentity.gameObject);
                }
            }

            // clear the hashset because we destroyed them all
            clientOwnedObjects.Clear();
        }
    }
}
