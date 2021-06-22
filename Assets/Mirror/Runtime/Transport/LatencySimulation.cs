// wraps around a transport and adds latency/loss/scramble simulation.
//
// reliable: latency
// unreliable: latency, loss, scramble (unreliable isn't ordered so we scramble)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    struct QueuedMessage
    {
        public int connectionId;
        public byte[] bytes;
        public float time;
    }

    [HelpURL("https://mirror-networking.gitbook.io/docs/transports/latency-simulaton-transport")]
    [DisallowMultipleComponent]
    public class LatencySimulation : Transport
    {
        public Transport wrap;

        [Header("Common")]
        [Tooltip("Spike latency via perlin(Time * speedMultiplier) * spikeMultiplier")]
        [Range(0, 1)] public float latencySpikeMultiplier;
        [Tooltip("Spike latency via perlin(Time * speedMultiplier) * spikeMultiplier")]
        public float latencySpikeSpeedMultiplier = 1;

        [Header("Reliable Messages")]
        [Tooltip("Reliable latency in seconds")]
        public float reliableLatency;
        // note: packet loss over reliable manifests itself in latency.
        //       don't need (and can't add) a loss option here.
        // note: reliable is ordered by definition. no need to scramble.

        [Header("Unreliable Messages")]
        [Tooltip("Packet loss in %")]
        [Range(0, 1)] public float unreliableLoss;
        [Tooltip("Unreliable latency in seconds")]
        public float unreliableLatency;
        [Tooltip("Scramble % of unreliable messages, just like over the real network. Mirror unreliable is unordered.")]
        [Range(0, 1)] public float unreliableScramble;

        // message queues
        // list so we can insert randomly (scramble)
        List<QueuedMessage> reliableClientToServer = new List<QueuedMessage>();
        List<QueuedMessage> reliableServerToClient = new List<QueuedMessage>();
        List<QueuedMessage> unreliableClientToServer = new List<QueuedMessage>();
        List<QueuedMessage> unreliableServerToClient = new List<QueuedMessage>();

        // random
        // UnityEngine.Random.value is [0, 1] with both upper and lower bounds inclusive
        // but we need the upper bound to be exclusive, so using System.Random instead.
        // => NextDouble() is NEVER < 0 so loss=0 never drops!
        // => NextDouble() is ALWAYS < 1 so loss=1 always drops!
        System.Random random = new System.Random();

        public void Awake()
        {
            if (wrap == null)
                throw new Exception("PressureDrop requires an underlying transport to wrap around.");
        }

        // forward enable/disable to the wrapped transport
        void OnEnable() { wrap.enabled = true; }
        void OnDisable() { wrap.enabled = false; }

        // noise function can be replaced if needed
        protected virtual float Noise(float time) => Mathf.PerlinNoise(time, time);

        // helper function to simulate latency
        float SimulateLatency(int channeldId)
        {
            // spike over perlin noise.
            // no spikes isn't realistic.
            // sin is too predictable / no realistic.
            // perlin is still deterministic and random enough.
            float spike = Noise(Time.time * latencySpikeSpeedMultiplier) * latencySpikeMultiplier;

            // base latency
            switch (channeldId)
            {
                case Channels.Reliable:
                    return reliableLatency + spike;
                case Channels.Unreliable:
                    return unreliableLatency + spike;
                default:
                    return 0;
            }
        }

        // helper function to simulate a send with latency/loss/scramble
        void SimulateSend(int connectionId, ArraySegment<byte> segment, int channelId, float latency, List<QueuedMessage> reliableQueue, List<QueuedMessage> unreliableQueue)
        {
            // segment is only valid after returning. copy it.
            // (allocates for now. it's only for testing anyway.)
            byte[] bytes = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, bytes, 0, segment.Count);

            // enqueue message. send after latency interval.
            QueuedMessage message = new QueuedMessage
            {
                connectionId = connectionId,
                bytes = bytes,
                time = Time.time + latency
            };

            switch (channelId)
            {
                case Channels.Reliable:
                    // simulate latency
                    reliableQueue.Add(message);
                    break;
                case Channels.Unreliable:
                    // simulate packet loss
                    bool drop = random.NextDouble() < unreliableLoss;
                    if (!drop)
                    {
                        // simulate scramble (Random.Next is < max, so +1)
                        bool scramble = random.NextDouble() < unreliableScramble;
                        int last = unreliableQueue.Count;
                        int index = scramble ? random.Next(0, last + 1) : last;

                        // simulate latency
                        unreliableQueue.Insert(index, message);
                    }
                    break;
                default:
                    Debug.LogError($"{nameof(LatencySimulation)} unexpected channelId: {channelId}");
                    break;
            }
        }

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

        public override void ClientDisconnect()
        {
            wrap.ClientDisconnect();
            reliableClientToServer.Clear();
            unreliableClientToServer.Clear();
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            float latency = SimulateLatency(channelId);
            SimulateSend(0, segment, channelId, latency, reliableClientToServer, unreliableClientToServer);
        }

        public override Uri ServerUri() => wrap.ServerUri();

        public override bool ServerActive() => wrap.ServerActive();

        public override string ServerGetClientAddress(int connectionId) => wrap.ServerGetClientAddress(connectionId);

        public override void ServerDisconnect(int connectionId) => wrap.ServerDisconnect(connectionId);

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            float latency = SimulateLatency(channelId);
            SimulateSend(connectionId, segment, channelId, latency, reliableServerToClient, unreliableServerToClient);
        }

        public override void ServerStart()
        {
            wrap.OnServerConnected = OnServerConnected;
            wrap.OnServerDataReceived = OnServerDataReceived;
            wrap.OnServerError = OnServerError;
            wrap.OnServerDisconnected = OnServerDisconnected;
            wrap.ServerStart();
        }

        public override void ServerStop()
        {
            wrap.ServerStop();
            reliableServerToClient.Clear();
            unreliableServerToClient.Clear();
        }

        public override void ClientEarlyUpdate() => wrap.ClientEarlyUpdate();
        public override void ServerEarlyUpdate() => wrap.ServerEarlyUpdate();
        public override void ClientLateUpdate()
        {
            // flush reliable messages after latency
            while (reliableClientToServer.Count > 0)
            {
                // check the first message time
                QueuedMessage message = reliableClientToServer[0];
                if (message.time <= Time.time)
                {
                    // send and eat
                    wrap.ClientSend(new ArraySegment<byte>(message.bytes), Channels.Reliable);
                    reliableClientToServer.RemoveAt(0);
                }
                // not enough time elapsed yet
                break;
            }

            // flush unreliable messages after latency
            while (unreliableClientToServer.Count > 0)
            {
                // check the first message time
                QueuedMessage message = unreliableClientToServer[0];
                if (message.time <= Time.time)
                {
                    // send and eat
                    wrap.ClientSend(new ArraySegment<byte>(message.bytes), Channels.Unreliable);
                    unreliableClientToServer.RemoveAt(0);
                }
                // not enough time elapsed yet
                break;
            }

            // update wrapped transport too
            wrap.ClientLateUpdate();
        }
        public override void ServerLateUpdate()
        {
            // flush reliable messages after latency
            while (reliableServerToClient.Count > 0)
            {
                // check the first message time
                QueuedMessage message = reliableServerToClient[0];
                if (message.time <= Time.time)
                {
                    // send and eat
                    wrap.ServerSend(message.connectionId, new ArraySegment<byte>(message.bytes), Channels.Reliable);
                    reliableServerToClient.RemoveAt(0);
                }
                // not enough time elapsed yet
                break;
            }

            // flush unreliable messages after latency
            while (unreliableServerToClient.Count > 0)
            {
                // check the first message time
                QueuedMessage message = unreliableServerToClient[0];
                if (message.time <= Time.time)
                {
                    // send and eat
                    wrap.ServerSend(message.connectionId, new ArraySegment<byte>(message.bytes), Channels.Unreliable);
                    unreliableServerToClient.RemoveAt(0);
                }
                // not enough time elapsed yet
                break;
            }

            // update wrapped transport too
            wrap.ServerLateUpdate();
        }

        public override int GetBatchThreshold(int channelId) => wrap.GetBatchThreshold(channelId);
        public override int GetMaxPacketSize(int channelId = 0) => wrap.GetMaxPacketSize(channelId);

        public override void Shutdown() => wrap.Shutdown();

        public override string ToString() => $"{nameof(LatencySimulation)} {wrap}";
    }
}
