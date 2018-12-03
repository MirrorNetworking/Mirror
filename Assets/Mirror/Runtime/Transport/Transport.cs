// transport layer backend
// - set to telepathy by default
// - can be changed by assigning Transport.layer to whatever you want
using System;

namespace Mirror
{
    // Transport class used by HLAPI ///////////////////////////////////////////
    public static class Transport
    {
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
        // events for the client
        event Action OnClientConnect;
        event Action<byte[]> OnClientData;
        event Action<Exception> OnClientError;
        event Action OnClientDisconnect;

        // client
        bool ClientConnected();
        // connect to a given ip address and port,  do not block
        void ClientConnect(string address, int port);
        bool ClientSend(int channelId, byte[] data);
        void ClientDisconnect();


        // events for the server
        event Action<int> OnServerConnect;
        event Action<int,byte[]> OnServerData;
        event Action<int, Exception> OnServerError;
        event Action<int> OnServerDisconnect;

        // server
        bool ServerActive();
        void ServerStart(string address, int port, int maxConnections);
        void ServerStartWebsockets(string address, int port, int maxConnections);
        bool ServerSend(int connectionId, int channelId, byte[] data);
        bool ServerDisconnect(int connectionId);
        bool GetConnectionInfo(int connectionId, out string address);
        void ServerStop();

        // common
        void Shutdown();
        int GetMaxPacketSize(int channelId=Channels.DefaultReliable);
    }
}