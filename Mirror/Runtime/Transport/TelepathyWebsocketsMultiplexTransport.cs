// multiplex transport that uses either:
// * Telepathy for standalone (windows/mac/linux/mobile/etc.)
// * UNET's LLAPI for websockets
using UnityEngine;
namespace Mirror
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
        public void ClientConnect(string address, ushort port)
        {
            client.ClientConnect(address, port);
        }
        public bool ClientSend(byte channelId, byte[] data)
        {
            return client.ClientSend(channelId, data);
        }
        public bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            return client.ClientGetNextMessage(out transportEvent, out data);
        }
        public void ClientDisconnect()
        {
            client.ClientDisconnect();
        }

        // server
        public bool ServerActive()
        {
            return server != null && server.ServerActive();
        }

        public void ServerStart(string address, ushort port, int maxConnections)
        {
            // WebGL host mode should work without errors, even though we can't
            // start a server in WebGL
            // (can't use Telepathy threads in webgl anyway)
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                server = new TelepathyTransport();
                server.ServerStart(address, port, maxConnections);
            }
            else Debug.LogWarning("ServerStart can't be called in WebGL.");
        }

        public void ServerStartWebsockets(string address, ushort port, int maxConnections)
        {
            // WebGL host mode should work without errors, even though we can't
            // start a server in WebGL
            // (can't call LLAPI's AddWebsocketHost in webgl anyway)
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                server = new LLAPITransport();
                server.ServerStartWebsockets(address,port, maxConnections);
            }
            else Debug.LogWarning("ServerStartWebsockets can't be called in WebGL.");
        }

        public bool ServerSend(int connectionId, byte channelId, byte[] data)
        {
            return server != null && server.ServerSend(connectionId, channelId, data);
        }

        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            connectionId = -1;
            transportEvent = TransportEvent.Disconnected;
            data = null;
            return server != null && server.ServerGetNextMessage(out connectionId, out transportEvent, out data);
        }

        public bool ServerDisconnect(int connectionId)
        {
            return server != null && server.ServerDisconnect(connectionId);
        }

        public bool GetConnectionInfo(int connectionId, out string address)
        {
            address = null;
            return server != null && server.GetConnectionInfo(connectionId, out address);
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

        public int GetMaxPacketSize(byte channelId)
        {
            if (server != null) return server.GetMaxPacketSize(channelId);
            if (client != null) return client.GetMaxPacketSize(channelId);
            return 0;
        }
    }
}