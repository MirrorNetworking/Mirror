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
        public float timeToSend;
    }

    [HelpURL("https://mirror-networking.gitbook.io/docs/transports/latency-simulaton-transport")]
    [DisallowMultipleComponent]
    public class LatencySimulation : Transport
    {
        public Transport wrap;

        [Header("Reliable Messages")]
        [Tooltip("Reliable latency in seconds")]
        public float reliableLatency = 0;
        [Tooltip("Simulate latency spikes with % of latency for % of messages.")]
        [Range(0, 1)] public float reliableLatencySpikes;
        // note: packet loss over reliable manifests itself in latency.
        //       don't need (and can't add) a loss option here.
        // note: reliable is ordered by definition. no need to scramble.

        [Header("Unreliable Messages")]
        [Tooltip("Unreliable latency in seconds")]
        public float unreliableLatency = 0;
        [Tooltip("Simulate latency spikes with % of latency for % of messages.")]
        [Range(0, 1)] public float unreliableLatencySpikes;
        [Tooltip("Packet loss in %")]
        [Range(0, 1)] public float unreliableLoss;
        [Tooltip("Scramble unreliable messages, just like over the real network. Mirror unreliable is unordered.")]
        public bool unreliableScramble;

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

        // helper function to simulate latency & spike with spike probability
        float SimulateLatency(float latency, float spikesPercent)
        {
            // will this one spike?
            bool spike = random.NextDouble() < spikesPercent;

            // if it spiked, add spike latency by percent of original latency
            float add = spike ? latency * spikesPercent : 0;

            // return latency + spike
            return latency + add;
        }

        // helper function to simulate a send with latency/loss/scramble
        void SimulateSend(int connectionId, int channelId, ArraySegment<byte> segment, List<QueuedMessage> reliableQueue, List<QueuedMessage> unreliableQueue)
        {
            // segment is only valid after returning. copy it.
            // (allocates for now. it's only for testing anyway.)
            byte[] bytes = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, bytes, 0, segment.Count);

            // enqueue message. send after latency interval.
            QueuedMessage message = new QueuedMessage
            {
                connectionId = connectionId,
                bytes = bytes
            };

            switch (channelId)
            {
                case Channels.DefaultReliable:
                    // simulate latency & spikes
                    message.timeToSend = Time.time + SimulateLatency(reliableLatency, reliableLatencySpikes);
                    reliableQueue.Add(message);
                    break;
                case Channels.DefaultUnreliable:
                    // simulate packet loss
                    bool drop = random.NextDouble() < unreliableLoss;
                    if (!drop)
                    {
                        // simulate scramble (Random.Next is < max, so +1)
                        // note that list entries are NOT ordered by time anymore
                        // after inserting randomly.
                        int last = unreliableQueue.Count;
                        int index = unreliableScramble ? random.Next(0, last + 1) : last;

                        // simulate latency & spikes
                        message.timeToSend = Time.time + SimulateLatency(unreliableLatency, unreliableLatencySpikes);
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

        public override void ClientSend(int channelId, ArraySegment<byte> segment) =>
            SimulateSend(0, channelId, segment, reliableClientToServer, unreliableClientToServer);

        public override Uri ServerUri() => wrap.ServerUri();

        public override bool ServerActive() => wrap.ServerActive();

        public override string ServerGetClientAddress(int connectionId) => wrap.ServerGetClientAddress(connectionId);

        public override bool ServerDisconnect(int connectionId) => wrap.ServerDisconnect(connectionId);

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment) =>
            SimulateSend(connectionId, channelId, segment, reliableServerToClient, unreliableServerToClient);

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
            // flush reliable messages that are ready to be sent
            // => list isn't ordered (due to scramble). need to iterate all.
            for (int i = 0; i < reliableClientToServer.Count; ++i)
            {
                QueuedMessage message = reliableClientToServer[i];
                if (Time.time >= message.timeToSend)
                {
                    // send and remove
                    wrap.ClientSend(Channels.DefaultReliable, new ArraySegment<byte>(message.bytes));
                    reliableClientToServer.RemoveAt(i);
                    --i;
                }
            }

            // flush unrelabe messages that are ready to be sent
            // => list isn't ordered (due to scramble). need to iterate all.
            for (int i = 0; i < unreliableClientToServer.Count; ++i)
            {
                QueuedMessage message = unreliableClientToServer[i];
                if (Time.time >= message.timeToSend)
                {
                    // send and remove
                    wrap.ClientSend(Channels.DefaultUnreliable, new ArraySegment<byte>(message.bytes));
                    unreliableClientToServer.RemoveAt(i);
                    --i;
                }
            }

            // update wrapped transport too
            wrap.ClientLateUpdate();
        }
        public override void ServerLateUpdate()
        {
            // flush reliable messages that are ready to be sent
            // => list isn't ordered (due to scramble). need to iterate all.
            for (int i = 0; i < reliableServerToClient.Count; ++i)
            {
                QueuedMessage message = reliableServerToClient[i];
                if (Time.time >= message.timeToSend)
                {
                    // send and remove
                    wrap.ServerSend(message.connectionId, Channels.DefaultReliable, new ArraySegment<byte>(message.bytes));
                    reliableServerToClient.RemoveAt(i);
                    --i;
                }
            }

            // flush unrelabe messages that are ready to be sent
            // => list isn't ordered (due to scramble). need to iterate all.
            for (int i = 0; i < unreliableServerToClient.Count; ++i)
            {
                QueuedMessage message = unreliableServerToClient[i];
                if (Time.time >= message.timeToSend)
                {
                    // send and remove
                    wrap.ServerSend(message.connectionId, Channels.DefaultUnreliable, new ArraySegment<byte>(message.bytes));
                    unreliableServerToClient.RemoveAt(i);
                    --i;
                }
            }

            // update wrapped transport too
            wrap.ServerLateUpdate();
        }

        public override int GetMaxBatchSize(int channelId) => wrap.GetMaxBatchSize(channelId);
        public override int GetMaxPacketSize(int channelId = 0) => wrap.GetMaxPacketSize(channelId);

        public override void Shutdown() => wrap.Shutdown();

        public override string ToString() => $"{nameof(LatencySimulation)} {wrap}";
    }
}
