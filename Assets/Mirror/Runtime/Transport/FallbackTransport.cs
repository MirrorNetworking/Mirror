// uses the first available transport for server and client.
// example: to use Apathy if on Windows/Mac/Linux and fall back to Telepathy
//          otherwise.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [HelpURL("https://mirror-networking.com/docs/Transports/Fallback.html")]
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
            InitClient();
            InitServer();
            available = GetAvailableTransport();
            Debug.Log("FallbackTransport available: " + available.GetType());
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

        // clients always pick the first transport
        void InitClient()
        {
            // wire all the base transports to our events
            foreach (Transport transport in transports)
            {
                transport.OnClientConnected.AddListener(OnClientConnected.Invoke);
                transport.OnClientDataReceived.AddListener(OnClientDataReceived.Invoke);
                transport.OnClientError.AddListener(OnClientError.Invoke);
                transport.OnClientDisconnected.AddListener(OnClientDisconnected.Invoke);
            }
        }

        public override void ClientConnect(string address)
        {
            available.ClientConnect(address);
        }

        public override bool ClientConnected()
        {
            return available.ClientConnected();
        }

        public override void ClientDisconnect()
        {
            available.ClientDisconnect();
        }

        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            return available.ClientSend(channelId, segment);
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return available.GetMaxPacketSize(channelId);
        }

        void InitServer()
        {
            // wire all the base transports to our events
            foreach (Transport transport in transports)
            {
                transport.OnServerConnected.AddListener(OnServerConnected.Invoke);
                transport.OnServerDataReceived.AddListener(OnServerDataReceived.Invoke);
                transport.OnServerError.AddListener(OnServerError.Invoke);
                transport.OnServerDisconnected.AddListener(OnServerDisconnected.Invoke);
            }
        }

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

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            return available.ServerSend(connectionIds, channelId, segment);
        }

        public override void ServerStart()
        {
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

        public override string ToString()
        {
            return available.ToString();
        }
    }
}
