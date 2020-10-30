using System;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkConnectionToServer>();

        public override string address => "";

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (logger.LogEnabled()) logger.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                Transport.activeTransport.ClientSend(channelId, segment);
            }
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            ClientScene.HandleClientDisconnect(this);
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
