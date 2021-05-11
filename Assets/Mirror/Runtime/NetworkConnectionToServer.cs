using System;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public override string address => "";

        internal override void Send(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                Transport.activeTransport.ClientSend(channelId, segment);
            }
        }

        /// <summary>Disconnects this connection.</summary>
        // IMPORTANT: calls Transport.Disconnect.
        // Transport.OnDisconnected is then called at some point in the future.
        public override void Disconnect()
        {
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
