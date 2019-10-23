using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public NetworkConnectionToServer(string networkAddress) : base(networkAddress)
        {
        }

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (logNetworkMessages) Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                return Transport.activeTransport.ClientSend(channelId, segment);
            }
            return false;
        }

    }

}