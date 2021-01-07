// uses the first available transport for server and client.
// example: to use Apathy if on Windows/Mac/Linux and fall back to Telepathy
//          otherwise.
using System;
using UnityEngine;

namespace Mirror
{
    [HelpURL("https://mirror-networking.com/docs/Articles/Transports/Fallback.html")]
    [DisallowMultipleComponent]
    public class FallbackTransport : Transport
    {
        public Transport[] transports;

        // the first transport that is available on this platform
        Transport available;

        public void Awake()
        {
            if (transports == null || transports.Length == 0)
            {
                throw new Exception("FallbackTransport requires at least 1 underlying transport");
            }
            available = GetAvailableTransport();
            Debug.Log("FallbackTransport available: " + available.GetType());
        }

        void OnEnable()
        {
            available.enabled = true;
        }

        void OnDisable()
        {
            available.enabled = false;
        }

        // The client just uses the first transport available
        Transport GetAvailableTransport()
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    return transport;
                }
            }
            throw new Exception("No transport suitable for this platform");
        }

        public override bool Available()
        {
            return available.Available();
        }

        public override void ClientConnect(string address)
        {
            available.OnClientConnected = OnClientConnected;
            available.OnClientDataReceived = OnClientDataReceived;
            available.OnClientError = OnClientError;
            available.OnClientDisconnected = OnClientDisconnected;
            available.ClientConnect(address);
        }

        public override void ClientConnect(Uri uri)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Available())
                {
                    try
                    {
                        transport.ClientConnect(uri);
                        available = transport;
                    }
                    catch (ArgumentException)
                    {
                        // transport does not support the schema, just move on to the next one
                    }
                }
            }
            throw new Exception("No transport suitable for this platform");
        }

        public override bool ClientConnected()
        {
            return available.ClientConnected();
        }

        public override void ClientDisconnect()
        {
            available.ClientDisconnect();
        }

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            available.ClientSend(channelId, segment);
        }

        // right now this just returns the first available uri,
        // should we return the list of all available uri?
        public override Uri ServerUri() => available.ServerUri();

        public override bool ServerActive()
        {
            return available.ServerActive();
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return available.ServerGetClientAddress(connectionId);
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return available.ServerDisconnect(connectionId);
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            available.ServerSend(connectionId, channelId, segment);
        }

        public override void ServerStart()
        {
            available.OnServerConnected = OnServerConnected;
            available.OnServerDataReceived = OnServerDataReceived;
            available.OnServerError = OnServerError;
            available.OnServerDisconnected = OnServerDisconnected;
            available.ServerStart();
        }

        public override void ServerStop()
        {
            available.ServerStop();
        }

        public override void Shutdown()
        {
            available.Shutdown();
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            // finding the max packet size in a fallback environment has to be
            // done very carefully:
            // * servers and clients might run different transports depending on
            //   which platform they are on.
            // * there should only ever be ONE true max packet size for everyone,
            //   otherwise a spawn message might be sent to all tcp sockets, but
            //   be too big for some udp sockets. that would be a debugging
            //   nightmare and allow for possible exploits and players on
            //   different platforms seeing a different game state.
            // => the safest solution is to use the smallest max size for all
            //    transports. that will never fail.
            int mininumAllowedSize = int.MaxValue;
            foreach (Transport transport in transports)
            {
                int size = transport.GetMaxPacketSize(channelId);
                mininumAllowedSize = Mathf.Min(size, mininumAllowedSize);
            }
            return mininumAllowedSize;
        }

        public override string ToString()
        {
            return available.ToString();
        }

    }
}
