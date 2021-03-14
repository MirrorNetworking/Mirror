// wraps around a transport and adds latency/loss/scramble simulation.
//
// reliable: latency
// unreliable: latency, loss, scramble (unreliable isn't ordered so we scramble)
using System;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    public class PressureDrop : Transport
    {
        public Transport wrap;

        [Header("Latency Simulation")]
        public int health;

        public void Awake()
        {
            if (wrap == null)
                throw new Exception("PressureDrop requires an underlying transport to wrap around.");
        }

        // forward enable/disable to the wrapped transport
        void OnEnable() { wrap.enabled = true; }
        void OnDisable() { wrap.enabled = false; }

        public override bool Available() => wrap.Available();

        public override void ClientConnect(string address)
        {
            wrap.OnClientConnected = OnClientConnected;
            wrap.OnClientDataReceived = OnClientDataReceived;
            wrap.OnClientError = OnClientError;
            wrap.OnClientDisconnected = OnClientDisconnected;
            wrap.ClientConnect(address);
        }

        public override void ClientConnect(Uri uri)
        {
            wrap.OnClientConnected = OnClientConnected;
            wrap.OnClientDataReceived = OnClientDataReceived;
            wrap.OnClientError = OnClientError;
            wrap.OnClientDisconnected = OnClientDisconnected;
            wrap.ClientConnect(uri);
        }

        public override bool ClientConnected() => wrap.ClientConnected();

        public override void ClientDisconnect() => wrap.ClientDisconnect();

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            // TODO
            wrap.ClientSend(channelId, segment);
        }

        public override Uri ServerUri() => wrap.ServerUri();

        public override bool ServerActive() => wrap.ServerActive();

        public override string ServerGetClientAddress(int connectionId) => wrap.ServerGetClientAddress(connectionId);

        public override bool ServerDisconnect(int connectionId) => wrap.ServerDisconnect(connectionId);

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            // TODO
            wrap.ServerSend(connectionId, channelId, segment);
        }

        public override void ServerStart()
        {
            wrap.OnServerConnected = OnServerConnected;
            wrap.OnServerDataReceived = OnServerDataReceived;
            wrap.OnServerError = OnServerError;
            wrap.OnServerDisconnected = OnServerDisconnected;
            wrap.ServerStart();
        }

        public override void ServerStop() => wrap.ServerStop();

        public override void ClientEarlyUpdate() => wrap.ClientEarlyUpdate();
        public override void ServerEarlyUpdate() => wrap.ServerEarlyUpdate();
        public override void ClientLateUpdate() => wrap.ClientLateUpdate();
        public override void ServerLateUpdate() => wrap.ServerLateUpdate();

        public override int GetMaxBatchSize(int channelId) => wrap.GetMaxBatchSize(channelId);
        public override int GetMaxPacketSize(int channelId = 0) => wrap.GetMaxPacketSize(channelId);

        public override void Shutdown() => wrap.Shutdown();

        public override string ToString() => nameof(PressureDrop) + " " + wrap;
    }
}
