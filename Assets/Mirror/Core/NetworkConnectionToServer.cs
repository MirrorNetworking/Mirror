using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        // Send stage three: hand off to transport
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            Transport.active.ClientSend(segment, channelId);

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            // TODO remove redundant state. have one source of truth for .ready!
            isReady = false;
            NetworkClient.ready = false;
            Transport.active.ClientDisconnect();
        }
    }
}
