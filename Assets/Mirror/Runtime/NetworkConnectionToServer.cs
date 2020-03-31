using System;
using System.Net;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public override EndPoint Address => new IPEndPoint(IPAddress.Loopback, 0);

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (logNetworkMessages) Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            return Transport.activeTransport.ClientSend(channelId, segment);
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect()
        {
            // set not ready and handle client disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
