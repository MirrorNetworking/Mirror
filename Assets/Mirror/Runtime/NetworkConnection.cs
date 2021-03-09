using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>Base NetworkConnection class for server-to-client and client-to-server connection.</summary>
    public abstract class NetworkConnection
    {
        public const int LocalConnectionId = 0;

        // NetworkIdentities that this connection can see
        internal readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

        Dictionary<int, NetworkMessageDelegate> messageHandlers;

        /// <summary>Unique identifier for this connection that is assigned by the transport layer.</summary>
        // assigned by transport, this id is unique for every connection on server.
        // clients don't know their own id and they don't know other client's ids.
        public readonly int connectionId;

        /// <summary>Flag that indicates the client has been authenticated.</summary>
        public bool isAuthenticated;

        /// <summary>General purpose object to hold authentication data, character selection, tokens, etc.</summary>
        public object authenticationData;

        /// <summary>A server connection is ready after joining the game world.</summary>
        public bool isReady;

        /// <summary>IP address of the connection. Can be useful for game master IP bans etc.</summary>
        public abstract string address { get; }

        /// <summary>Last time a message was received for this connection. Includes system & user messages.</summary>
        public float lastMessageTime;

        /// <summary>This connection's main object (usually the player object).</summary>
        public NetworkIdentity identity { get; internal set; }

        /// <summary>All NetworkIdentities owned by this connection. Can be main player, pets, etc.</summary>
        // IMPORTANT: this needs to be <NetworkIdentity>, not <uint netId>.
        //            fixes a bug where DestroyOwnedObjects wouldn't find the
        //            netId anymore: https://github.com/vis2k/Mirror/issues/1380
        //            Works fine with NetworkIdentity pointers though.
        public readonly HashSet<NetworkIdentity> clientOwnedObjects = new HashSet<NetworkIdentity>();

        internal NetworkConnection()
        {
            // set lastTime to current time when creating connection to make
            // sure it isn't instantly kicked for inactivity
            lastMessageTime = Time.time;
        }

        internal NetworkConnection(int networkConnectionId) : this()
        {
            connectionId = networkConnectionId;
            // TODO why isn't lastMessageTime set in here like in the other ctor?
        }

        /// <summary>Disconnects this connection.</summary>
        public abstract void Disconnect();

        internal void SetHandlers(Dictionary<int, NetworkMessageDelegate> handlers)
        {
            messageHandlers = handlers;
        }

        /// <summary>Send a NetworkMessage to this connection over the given channel.</summary>
        public void Send<T>(T msg, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message and send allocation free
                MessagePacking.Pack(msg, writer);
                NetworkDiagnostics.OnSend(msg, channelId, writer.Position, 1);
                Send(writer.ToArraySegment(), channelId);
            }
        }

        // validate packet size before sending. show errors if too big/small.
        // => it's best to check this here, we can't assume that all transports
        //    would check max size and show errors internally. best to do it
        //    in one place in hlapi.
        // => it's important to log errors, so the user knows what went wrong.
        protected static bool ValidatePacketSize(ArraySegment<byte> segment, int channelId)
        {
            if (segment.Count > Transport.activeTransport.GetMaxPacketSize(channelId))
            {
                Debug.LogError("NetworkConnection.ValidatePacketSize: cannot send packet larger than " + Transport.activeTransport.GetMaxPacketSize(channelId) + " bytes");
                return false;
            }

            if (segment.Count == 0)
            {
                // zero length packets getting into the packet queues are bad.
                Debug.LogError("NetworkConnection.ValidatePacketSize: cannot send zero bytes");
                return false;
            }

            // good size
            return true;
        }

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        internal abstract void Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable);

        public override string ToString() => $"connection({connectionId})";

        internal void AddToObserving(NetworkIdentity identity)
        {
            observing.Add(identity);

            // spawn identity for this conn
            NetworkServer.ShowForConnection(identity, this);
        }

        internal void RemoveFromObserving(NetworkIdentity identity, bool isDestroyed)
        {
            observing.Remove(identity);

            if (!isDestroyed)
            {
                // hide identity for this conn
                NetworkServer.HideForConnection(identity, this);
            }
        }

        internal void RemoveObservers()
        {
            foreach (NetworkIdentity identity in observing)
            {
                identity.RemoveObserverInternal(this);
            }
            observing.Clear();
        }

        // helper function
        protected bool UnpackAndInvoke(NetworkReader reader, int channelId)
        {
            if (MessagePacking.Unpack(reader, out int msgType))
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
                    // Debug.Log("Unknown message ID " + msgType + " " + this + ". May be due to no existing RegisterHandler for this message.");
                    return false;
                }
            }
            else
            {
                Debug.LogError("Closed connection: " + this + ". Invalid message header.");
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
            if (buffer.Count < MessagePacking.HeaderSize)
            {
                Debug.LogError($"ConnectionRecv {this} Message was too short (messages should start with message id)");
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
