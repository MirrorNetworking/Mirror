// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using UnityEngine;

namespace Mirror
{
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }

    public abstract class Transport : MonoBehaviour
    {
        // client
        public abstract bool ClientConnected();
        public abstract void ClientConnect(string address);
        public abstract bool ClientSend(int channelId, byte[] data);
        public abstract bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data);
        public abstract void ClientDisconnect();

        // server
        public abstract bool ServerActive();
        public abstract void ServerStart();
        public abstract bool ServerSend(int connectionId, int channelId, byte[] data);
        public abstract bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data);
        public abstract bool ServerDisconnect(int connectionId);
        public abstract bool GetConnectionInfo(int connectionId, out string address);
        public abstract void ServerStop();

        // common
        public abstract void Shutdown();
        public abstract int GetMaxPacketSize(int channelId=Channels.DefaultReliable);
    }
}