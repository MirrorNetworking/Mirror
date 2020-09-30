using System;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public NetworkConnectionToClient(int networkConnectionId) : base(networkConnectionId) { }

        public override string address => Transport.activeTransport.ServerGetClientAddress(connectionId);

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            //Debug.Log("ConnectionSend " + this + " bytes:" + BitConverter.ToString(segment.Array, segment.Offset, segment.Count));

            // validate packet size first.
            if (ValidatePacketSize(segment, channelId))
            {
                return Transport.activeTransport.ServerSend(connectionId, channelId, segment);
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
