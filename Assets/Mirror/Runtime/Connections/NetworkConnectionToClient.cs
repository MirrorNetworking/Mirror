using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkConnectionToClient>();

        public NetworkConnectionToClient(int networkConnectionId) : base(networkConnectionId) { }

        public override string address => Transport.activeTransport.ServerGetClientAddress(connectionId);

        // internal because no one except Mirror should send bytes directly to
        // the client. they would be detected as a message. send messages instead.
        readonly List<int> singleConnectionId = new List<int> { -1 };

        // Failsafe to kick clients that have stopped sending anything to the server.
        // Clients ping the server every 2 seconds but transports are unreliable
        // when it comes to properly generating Disconnect messages to the server.
        internal override bool IsClientAlive() => Time.time - lastMessageTime < NetworkServer.disconnectInactiveTimeout;

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (logger.LogEnabled()) logger.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

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
        }
    }
}
