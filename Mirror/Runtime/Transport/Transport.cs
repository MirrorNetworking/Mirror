// transport layer backend
// - set to telepathy by default
// - can be changed by assigning Transport.layer to whatever you want
namespace Mirror
{
    // Transport class used by HLAPI ///////////////////////////////////////////
    public static class Transport
    {
        // hlapi needs to know max packet size to show warnings
        public static int MaxPacketSize = ushort.MaxValue;

        // selected transport layer: Telepathy by default
        public static TransportLayer layer = new TelepathyWebsocketsMultiplexTransport();
    }

    // abstract transport layer class //////////////////////////////////////////
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }
    public interface TransportLayer
    {
        // client
        bool ClientConnected();
        void ClientConnect(string address, int port);
        bool ClientSend(byte[] data);
        bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data);
        float ClientGetRTT();
        void ClientDisconnect();

        // server
        bool ServerActive();
        void ServerStart(string address, int port, int maxConnections);
        void ServerStartWebsockets(string address, int port, int maxConnections);
        bool ServerSend(int connectionId, byte[] data);
        bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data);
        bool GetConnectionInfo(int connectionId, out string address);
        void ServerStop();

        // common
        void Shutdown();
    }
}