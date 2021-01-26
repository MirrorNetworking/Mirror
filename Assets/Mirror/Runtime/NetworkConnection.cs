using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

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
    public class NetworkConnection : INetworkConnection
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkConnection));

        // Handles network messages on client and server
        internal delegate void NetworkMessageDelegate(INetworkConnection conn, NetworkReader reader, int channelId);

        // internal so it can be tested
        private readonly HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>();

        // message handlers for this connection
        internal readonly Dictionary<int, NetworkMessageDelegate> messageHandlers = new Dictionary<int, NetworkMessageDelegate>();

        /// <summary>
        /// Transport level connection
        /// </summary>
        /// <remarks>
        /// <para>On a server, this Id is unique for every connection on the server. On a client this Id is local to the client, it is not the same as the Id on the server for this connection.</para>
        /// <para>Transport layers connections begin at one. So on a client with a single connection to a server, the connectionId of that connection will be one. In NetworkServer, the connectionId of the local connection is zero.</para>
        /// <para>Clients do not know their connectionId on the server, and do not know the connectionId of other clients on the server.</para>
        /// </remarks>
        private readonly IConnection connection;

        /// <summary>
        /// General purpose object to hold authentication data, character selection, tokens, etc.
        /// associated with the connection for reference after Authentication completes.
        /// </summary>
        public object AuthenticationData { get; set; }

        /// <summary>
        /// Flag that tells if the connection has been marked as "ready" by a client calling ClientScene.Ready().
        /// <para>This property is read-only. It is set by the system on the client when ClientScene.Ready() is called, and set by the system on the server when a ready message is received from a client.</para>
        /// <para>A client that is ready is sent spawned objects by the server and updates to the state of spawned objects. A client that is not ready is not sent spawned objects.</para>
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// The IP address / URL / FQDN associated with the connection.
        /// Can be useful for a game master to do IP Bans etc.
        /// </summary>
        public virtual EndPoint Address => connection.GetEndPointAddress();

        /// <summary>
        /// The NetworkIdentity for this connection.
        /// </summary>
        public NetworkIdentity Identity { get; set; }

        /// <summary>
        /// A list of the NetworkIdentity objects owned by this connection. This list is read-only.
        /// <para>This includes the player object for the connection - if it has localPlayerAutority set, and any objects spawned with local authority or set with AssignLocalAuthority.</para>
        /// <para>This list can be used to validate messages from clients, to ensure that clients are only trying to control objects that they own.</para>
        /// </summary>
        // IMPORTANT: this needs to be <NetworkIdentity>, not <uint netId>. fixes a bug where DestroyOwnedObjects wouldn't find
        //            the netId anymore: https://github.com/vis2k/Mirror/issues/1380 . Works fine with NetworkIdentity pointers though.
        private readonly HashSet<NetworkIdentity> clientOwnedObjects = new HashSet<NetworkIdentity>();

        /// <summary>
        /// Creates a new NetworkConnection with the specified address and connectionId
        /// </summary>
        /// <param name="networkConnectionId"></param>
        public NetworkConnection(IConnection connection)
        {
            Assert.IsNotNull(connection);
            this.connection = connection;
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public virtual void Disconnect()
        {
            connection.Disconnect();
        }

        private static NetworkMessageDelegate MessageHandler<T>(Action<INetworkConnection, T> handler)
        {
            void AdapterFunction(INetworkConnection conn, NetworkReader reader, int channelId)
            {
                // protect against DOS attacks if attackers try to send invalid
                // data packets to crash the server/client. there are a thousand
                // ways to cause an exception in data handling:
                // - invalid headers
                // - invalid message ids
                // - invalid data causing exceptions
                // - negative ReadBytesAndSize prefixes
                // - invalid utf8 strings
                // - etc.
                //
                // let's catch them all and then disconnect that connection to avoid
                // further attacks.
                var message = default(T);
                try
                {
                    message = reader.Read<T>();
                }
                finally
                {
                    NetworkDiagnostics.OnReceive(message, channelId, reader.Length);
                }

                handler(conn, message);
            }
            return AdapterFunction;
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked for when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public void RegisterHandler<T>(Action<INetworkConnection, T> handler)
        {
            int msgType = MessagePacker.GetId<T>();
            if (logger.filterLogType == LogType.Log && messageHandlers.ContainsKey(msgType))
            {
                logger.Log("NetworkServer.RegisterHandler replacing " + msgType);
            }
            messageHandlers[msgType] = MessageHandler(handler);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked for when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public void RegisterHandler<T>(Action<T> handler)
        {
            RegisterHandler<T>((_, value) => { handler(value); });
        }

        /// <summary>
        /// Unregisters a handler for a particular message type.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        public void UnregisterHandler<T>()
        {
            int msgType = MessagePacker.GetId<T>();
            messageHandlers.Remove(msgType);
        }

        /// <summary>
        /// Clear all registered callback handlers.
        /// </summary>
        public void ClearHandlers()
        {
            messageHandlers.Clear();
        }

        /// <summary>
        /// This sends a network message to the connection.
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="msg">The message to send</param>
        /// <param name="channelId">The transport layer channel to send on.</param>
        /// <returns></returns>
        public virtual void Send<T>(T msg, int channelId = Channel.Reliable)
        {
            SendAsync(msg, channelId).Forget();
        }

        public static void Send<T>(IEnumerable<INetworkConnection> connections, T msg, int channelId = Channel.Reliable)
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message into byte[] once
                MessagePacker.Pack(msg, writer);
                var segment = writer.ToArraySegment();
                int count = 0;

                foreach (INetworkConnection conn in connections)
                {
                    // send to all connections, but don't wait for them
                    conn.SendAsync(segment, channelId).Forget();
                    count++;
                }

                NetworkDiagnostics.OnSend(msg, channelId, segment.Count, count);
            }
        }

        /// <summary>
        /// This sends a network message to the connection. You can await it to check for errors
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="msg">The message to send.</param>
        /// <param name="channelId">The transport layer channel to send on.</param>
        /// <returns></returns>
        public virtual UniTask SendAsync<T>(T msg, int channelId = Channel.Reliable)
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message and send allocation free
                MessagePacker.Pack(msg, writer);
                NetworkDiagnostics.OnSend(msg, channelId, writer.Length, 1);
                return SendAsync(writer.ToArraySegment(), channelId);
            }
        }

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        public UniTask SendAsync(ArraySegment<byte> segment, int channelId = Channel.Reliable)
        {
            return connection.SendAsync(segment, channelId);
        }


        public override string ToString()
        {
            return $"connection({Address})";
        }

        public void AddToVisList(NetworkIdentity identity)
        {
            visList.Add(identity);
        }

        public void RemoveFromVisList(NetworkIdentity identity)
        {
            visList.Remove(identity);
        }

        public void RemoveObservers()
        {
            foreach (NetworkIdentity identity in visList)
            {
                identity.RemoveObserverInternal(this);
            }
            visList.Clear();
        }

        internal void InvokeHandler(int msgType, NetworkReader reader, int channelId)
        {
            if (messageHandlers.TryGetValue(msgType, out NetworkMessageDelegate msgDelegate))
            {
                msgDelegate(this, reader, channelId);
            }
            else
            {
                try
                {
                    Type type = MessagePacker.GetMessageType(msgType);
                    throw new InvalidDataException($"Unexpected message {type} received in {this}. Did you register a handler for it?");
                }
                catch (KeyNotFoundException)
                {
                    throw new InvalidDataException($"Unexpected message ID {msgType} received in {this}. May be due to no existing RegisterHandler for this message.");
                }
            }
        }

        // note: original HLAPI HandleBytes function handled >1 message in a while loop, but this wasn't necessary
        //       anymore because NetworkServer/NetworkClient Update both use while loops to handle >1 data events per
        //       frame already.
        //       -> in other words, we always receive 1 message per Receive call, never two.
        //       -> can be tested easily with a 1000ms send delay and then logging amount received in while loops here
        //          and in NetworkServer/Client Update. HandleBytes already takes exactly one.
        /// <summary>
        /// This function allows custom network connection classes to process data from the network before it is passed to the application.
        /// </summary>
        /// <param name="buffer">The data received.</param>
        internal void TransportReceive(ArraySegment<byte> buffer, int channelId)
        {
            // unpack message
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(buffer))
            {
                try
                {
                    int msgType = MessagePacker.UnpackId(networkReader);

                    if (msgType == MessagePacker.GetId<NotifyPacket>())
                    {
                        // this is a notify message, send to the notify receive
                        NotifyPacket notifyPacket = networkReader.ReadNotifyPacket();
                        ReceiveNotify(notifyPacket, networkReader, channelId);
                    }
                    else
                    {
                        // try to invoke the handler for that message
                        InvokeHandler(msgType, networkReader, channelId);
                    }
                }
                catch (InvalidDataException ex)
                {
                    logger.Log(ex.ToString());
                }
                catch (Exception ex)
                {
                    logger.LogError("Closed connection: " + this + ". Invalid message " + ex);
                    Disconnect();
                }
            }
        }

        public void AddOwnedObject(NetworkIdentity networkIdentity)
        {
            clientOwnedObjects.Add(networkIdentity);
        }

        public void RemoveOwnedObject(NetworkIdentity networkIdentity)
        {
            clientOwnedObjects.Remove(networkIdentity);
        }

        public void DestroyOwnedObjects()
        {
            // create a copy because the list might be modified when destroying
            var tmp = new HashSet<NetworkIdentity>(clientOwnedObjects);
            foreach (NetworkIdentity netIdentity in tmp)
            {
                //dont destroy self yet.
                if (netIdentity != null && netIdentity != Identity && Identity.ServerObjectManager != null)
                {
                    Identity.ServerObjectManager.Destroy(netIdentity.gameObject);
                }
            }

            if (Identity != null && Identity.Server != null)
                // Destroy the connections own identity.
                Identity.ServerObjectManager.Destroy(Identity.gameObject);

            // clear the hashset because we destroyed them all
            clientOwnedObjects.Clear();
        }

        public async UniTask ProcessMessagesAsync()
        {
            var buffer = new MemoryStream();

            try
            {
                while (true)
                {

                    int channel = await connection.ReceiveAsync(buffer);

                    buffer.TryGetBuffer(out ArraySegment<byte> data);
                    TransportReceive(data, channel);
                }
            }
            catch (EndOfStreamException)
            {
                // connection closed,  normal
            }
        }

        #region Notify

        internal struct PacketEnvelope
        {
            internal ushort Sequence;
            internal object Token;
        }
        const int ACK_MASK_BITS = sizeof(ulong) * 8;
        const int WINDOW_SIZE = 512;

        private Sequencer sequencer = new Sequencer(16);
        readonly Queue<PacketEnvelope> sendWindow = new Queue<PacketEnvelope>(WINDOW_SIZE);

        private ushort receiveSequence;
        private ulong receiveMask;


        /// <summary>
        /// Sends a message, but notify when it is delivered or lost
        /// </summary>
        /// <typeparam name="T">type of message to send</typeparam>
        /// <param name="msg">message to send</param>
        /// <param name="token">a arbitrary object that the sender will receive with their notification</param>
        public void SendNotify<T>(T msg, object token, int channelId = Channel.Unreliable)
        {
            if (sendWindow.Count == WINDOW_SIZE)
            {
                NotifyLost?.Invoke(this, token);
                return;
            }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                var notifyPacket = new NotifyPacket
                {
                    Sequence = (ushort)sequencer.Next(),
                    ReceiveSequence = receiveSequence,
                    AckMask = receiveMask
                };

                sendWindow.Enqueue(new PacketEnvelope
                {
                    Sequence = notifyPacket.Sequence,
                    Token = token
                });

                MessagePacker.Pack(notifyPacket, writer);
                MessagePacker.Pack(msg, writer);
                NetworkDiagnostics.OnSend(msg, channelId, writer.Length, 1);
                SendAsync(writer.ToArraySegment(), channelId).Forget();
            }

        }

        internal void ReceiveNotify(NotifyPacket notifyPacket, NetworkReader networkReader, int channelId)
        {
            int sequenceDistance = (int)sequencer.Distance(notifyPacket.Sequence, receiveSequence);

            // sequence is so far out of bounds we can't save, just kick him (or her!)
            if (Math.Abs(sequenceDistance) > WINDOW_SIZE)
            {
                Disconnect();
                return;
            }

            // this message is old,  we already received
            // a newer or duplicate packet.  Discard it
            if (sequenceDistance <= 0)
                return;

            receiveSequence = notifyPacket.Sequence;

            if (sequenceDistance >= ACK_MASK_BITS)
                receiveMask = 1;
            else
                receiveMask = (receiveMask << sequenceDistance) | 1;

            AckPackets(notifyPacket.ReceiveSequence, notifyPacket.AckMask);

            int msgType = MessagePacker.UnpackId(networkReader);
            InvokeHandler(msgType, networkReader, channelId);
        }

        // the other end just sent us a message
        // and it told us the latest message it got
        // and the ack mask
        private void AckPackets(ushort receiveSequence, ulong ackMask)
        {
            while (sendWindow.Count > 0)
            {
                PacketEnvelope envelope = sendWindow.Peek();

                int distance = (int)sequencer.Distance(envelope.Sequence, receiveSequence);

                if (distance > 0)
                    break;

                sendWindow.Dequeue();

                // if any of these cases trigger, packet is most likely lost
                if ((distance <= -ACK_MASK_BITS) || ((ackMask & (1UL << -distance)) == 0UL))
                {
                    NotifyLost?.Invoke(this, envelope.Token);
                }
                else
                {
                    NotifyDelivered?.Invoke(this, envelope.Token);
                }
            }
        }

        /// <summary>
        /// Raised when a message is delivered
        /// </summary>
        public event Action<INetworkConnection, object> NotifyDelivered;

        /// <summary>
        /// Raised when a message is lost
        /// </summary>
        public event Action<INetworkConnection, object> NotifyLost;
        #endregion
    }
}
