// multiplex transport that uses either:
// * Telepathy for standalone (windows/mac/linux/mobile/etc.)
// * UNET's LLAPI for websockets
namespace UnityEngine.Networking
{
    public class TelepathyWebsocketsMultiplexTransport : TransportLayer
    {
        // client & server transports are assigned dynamically
        TransportLayer client;
        TransportLayer server;

        // initialization
        public TelepathyWebsocketsMultiplexTransport()
        {
            // set client to llapi in webgl, telepathy otherwise
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                client = new LLAPITransport();
            }
            else
            {
                client = new TelepathyTransport();
            }

            // server never runs as WebGLPlayer, need to wait for Start call to
            // decide which transport to use
        }

        // client
        public bool ClientConnected()
        {
            return client.ClientConnected();
        }
        public void ClientConnect(string address, int port)
        {
            client.ClientConnect(address, port);
        }
        public bool ClientSend(byte[] data)
        {
            return client.ClientSend(data);
        }
        public bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            return client.ClientGetNextMessage(out transportEvent, out data);
        }
        public float ClientGetRTT()
        {
            return client.ClientGetRTT();
        }
        public void ClientDisconnect()
        {
            client.ClientDisconnect();
        }

        // server
        public bool ServerActive()
        {
            return server != null ? server.ServerActive() : false;
        }

        public void ServerStart(string address, int port, int maxConnections)
        {
            server = new TelepathyTransport();
            server.ServerStart(address, port, maxConnections);
        }

        public void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            server = new LLAPITransport();
            server.ServerStartWebsockets(address,port, maxConnections);
        }

        public bool ServerSend(int connectionId, byte[] data)
        {
            return server != null ? server.ServerSend(connectionId, data) : false;
        }

        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            connectionId = -1;
            transportEvent = TransportEvent.Disconnected;
            data = null;
            return server != null ? server.ServerGetNextMessage(out connectionId, out transportEvent, out data) : false;
        }

        public bool GetConnectionInfo(int connectionId, out string address)
        {
            address = null;
            return server != null ? server.GetConnectionInfo(connectionId, out address) : false;
        }

        public void ServerStop()
        {
            if (server != null) server.ServerStop();
        }

        // common
        public void Shutdown()
        {
            client.Shutdown();
            if (server != null) server.Shutdown();
        }
    }
}