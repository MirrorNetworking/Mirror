using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public NetworkConnectionToClient(string networkAddress, int networkConnectionId) : base(networkAddress, networkConnectionId)
        {
        }

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

    }


}