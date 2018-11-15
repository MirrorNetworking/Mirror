// transport layer backend
// - set to telepathy by default
// - can be changed by assigning Transport.layer to whatever you want
namespace Mirror
{
    // Transport class used by HLAPI ///////////////////////////////////////////
    public static class Transport
    {
        // hlapi needs to know max packet size to show warnings
        public static int MaxPacketSize = int.MaxValue;

        // selected transport layer
        // the transport is normally initialized in NetworkManager InitializeTransport
        // initialize it yourself if you are not using NetworkManager
        public static TransportLayer layer;
    }

    // abstract transport layer class //////////////////////////////////////////
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }
    public interface TransportLayer
    {
        // client
        bool ClientConnected();
        void ClientConnect(string address, int port);
        bool ClientSend(int channelId, byte[] data);
        bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data);
        void ClientDisconnect();

        // server
        bool ServerActive();
        void ServerStart(string address, int port, int maxConnections);
        void ServerStartWebsockets(string address, int port, int maxConnections);
        bool ServerSend(int connectionId, int channelId, byte[] data);
        bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data);
        bool ServerDisconnect(int connectionId);
        bool GetConnectionInfo(int connectionId, out string address);
        void ServerStop();

        // common
        void Shutdown();
    }
}