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
        // TODO move to server's NetworkConnectionToClient?
        internal readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

        // TODO this is NetworkServer.handlers on server and NetworkClient.handlers on client.
        //      maybe use them directly. avoid extra state.
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
        // TODO move this to ConnectionToClient so the flag only lives on server
        // connections? clients could use NetworkClient.ready to avoid redundant
        // state.
        public bool isReady;

        /// <summary>IP address of the connection. Can be useful for game master IP bans etc.</summary>
        public abstract string address { get; }

        /// <summary>Last time a message was received for this connection. Includes system and user messages.</summary>
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
        public void Send<T>(T msg, int channelId = Channels.Reliable)
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
                Debug.LogError($"NetworkConnection.ValidatePacketSize: cannot send packet larger than {Transport.activeTransport.GetMaxPacketSize(channelId)} bytes, was {segment.Count} bytes");
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
        internal abstract void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable);

        public override string ToString() => $"connection({connectionId})";

        // TODO move to server's NetworkConnectionToClient?
        internal void AddToObserving(NetworkIdentity netIdentity)
        {
            observing.Add(netIdentity);

            // spawn identity for this conn
            NetworkServer.ShowForConnection(netIdentity, this);
        }

        // TODO move to server's NetworkConnectionToClient?
        internal void RemoveFromObserving(NetworkIdentity netIdentity, bool isDestroyed)
        {
            observing.Remove(netIdentity);

            if (!isDestroyed)
            {
                // hide identity for this conn
                NetworkServer.HideForConnection(netIdentity, this);
            }
        }

        // TODO move to server's NetworkConnectionToClient?
        internal void RemoveObservers()
        {
            foreach (NetworkIdentity netIdentity in observing)
            {
                netIdentity.RemoveObserverInternal(this);
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

        // called when receiving data from the transport
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

        /// <summary>Check if we received a message within the last 'timeout' seconds.</summary>
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
