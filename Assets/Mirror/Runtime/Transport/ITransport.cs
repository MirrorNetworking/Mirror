// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using UnityEngine;

namespace Mirror
{
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }

    public interface ITransport
    {
        // client
        bool ClientConnected();
        void ClientConnect(string address);
        bool ClientSend(int channelId, byte[] data);
        bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data);
        void ClientDisconnect();

        // server
        bool ServerActive();
        void ServerStart();
        bool ServerSend(int connectionId, int channelId, byte[] data);
        bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data);
        bool ServerDisconnect(int connectionId);
        bool GetConnectionInfo(int connectionId, out string address);
        void ServerStop();

        // common
        void Shutdown();
        int GetMaxPacketSize(int channelId=Channels.DefaultReliable);
    }
}