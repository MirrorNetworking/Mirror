using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        /// <summary>
        /// A list of the NetworkIdentity objects owned by this connection. This list is read-only.
        /// <para>This includes the player object for the connection - if it has localPlayerAutority set, and any objects spawned with local authority or set with AssignLocalAuthority.</para>
        /// <para>This list can be used to validate messages from clients, to ensure that clients are only trying to control objects that they own.</para>
        /// </summary>
        public readonly HashSet<NetworkIdentity> clientOwnedObjects = new HashSet<NetworkIdentity>();

        public NetworkConnectionToClient(int networkConnectionId) : base(networkConnectionId)
        {
        }

        public override string address => Transport.activeTransport.ServerGetClientAddress(connectionId);

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        List<int> singleConnectionId = new List<int> { -1 };

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (logNetworkMessages) Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                singleConnectionId[0] = connectionId;
                return Transport.activeTransport.ServerSend(singleConnectionId, channelId, segment);
            }
            return false;
        }

        // Send to many. basically Transport.Send(connections) + checks.
        internal static bool Send(List<int> connectionIds, ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                // only the server sends to many, we don't have that function on
                // a client.
                if (Transport.activeTransport.ServerActive())
                {
                    return Transport.activeTransport.ServerSend(connectionIds, channelId, segment);
                }
            }
            return false;
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            Transport.activeTransport.ServerDisconnect(connectionId);
            RemoveObservers();
            DestroyOwnedObjects();
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Add(obj);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            clientOwnedObjects.Remove(obj);
        }

        protected void DestroyOwnedObjects()
        {
            // work on copy because the list may be modified when we destroy the objects
            HashSet<NetworkIdentity> objects = new HashSet<NetworkIdentity>(clientOwnedObjects);
            foreach (NetworkIdentity netId in objects)
            {
                if (netId != null)
                {
                    NetworkServer.Destroy(netId.gameObject);
                }
            }
            clientOwnedObjects.Clear();
        }
    }
}