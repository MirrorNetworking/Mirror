using System;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public override string address => "";

        protected override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
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
            // TODO: This does not work if there is no player yet
            if (identity != null)
                identity.client.HandleClientDisconnect(this);
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
