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

    [DisallowMultipleComponent]
    public class PressureDrop : Transport
    {
        public Transport wrap;

        [Header("Reliable Messages")]
        [Tooltip("Reliable latency in seconds")]
        public float reliableLatency = 0;

        [Header("Unreliable Messages")]
        [Tooltip("Packet loss in %")]
        [Range(0, 1)] public float unreliableLoss;
        [Tooltip("Unreliable latency in seconds")]
        public float unreliableLatency = 0;

        // message queues
        Queue<QueuedMessage> reliableClientToServer = new Queue<QueuedMessage>();
        Queue<QueuedMessage> reliableServerToClient = new Queue<QueuedMessage>();
        Queue<QueuedMessage> unreliableClientToServer = new Queue<QueuedMessage>();
        Queue<QueuedMessage> unreliableServerToClient = new Queue<QueuedMessage>();

        // random
        // UnityEngine.Random.value is [0, 1] but we need [0, 1)
        // aka exclusive to 1, not inclusive.
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

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            // segment is only valid after returning. copy it.
            // (allocates for now. it's only for testing anyway.)
            byte[] bytes = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, bytes, 0, segment.Count);
            // enqueue message. send after latency interval.
            QueuedMessage message = new QueuedMessage
            {
                connectionId = 0,
                bytes = bytes,
                time = Time.time
            };

            switch (channelId)
            {
                case Channels.DefaultReliable:
                    // simulate latency
                    reliableClientToServer.Enqueue(message);
                    break;
                case Channels.DefaultUnreliable:
                    // simulate packet loss
                    bool drop = random.NextDouble() < unreliableLoss;
                    if (!drop)
                    {
                        // simulate latency
                        unreliableClientToServer.Enqueue(message);
                    }
                    break;
                default:
                    Debug.LogError($"{nameof(PressureDrop)} unexpected channelId: {channelId}");
                    break;
            }
        }

        public override Uri ServerUri() => wrap.ServerUri();

        public override bool ServerActive() => wrap.ServerActive();

        public override string ServerGetClientAddress(int connectionId) => wrap.ServerGetClientAddress(connectionId);

        public override bool ServerDisconnect(int connectionId) => wrap.ServerDisconnect(connectionId);

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
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
                time = Time.time
            };

            switch (channelId)
            {
                case Channels.DefaultReliable:
                    // simulate latency
                    reliableServerToClient.Enqueue(message);
                    break;
                case Channels.DefaultUnreliable:
                    // simulate packet loss
                    bool drop = random.NextDouble() < unreliableLoss;
                    if (!drop)
                    {
                        // simulate latency
                        unreliableServerToClient.Enqueue(message);
                    }
                    break;
                default:
                    Debug.LogError($"{nameof(PressureDrop)} unexpected channelId: {channelId}");
                    break;
            }
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
                QueuedMessage message = reliableClientToServer.Peek();
                if (message.time + reliableLatency <= Time.time)
                {
                    // send and eat (we peeked before)
                    wrap.ClientSend(Channels.DefaultReliable, new ArraySegment<byte>(message.bytes));
                    reliableClientToServer.Dequeue();
                }
                // not enough time elapsed yet
                break;
            }

            // flush unreliable messages after latency
            while (unreliableClientToServer.Count > 0)
            {
                // check the first message time
                QueuedMessage message = unreliableClientToServer.Peek();
                if (message.time + unreliableLatency <= Time.time)
                {
                    // send and eat (we peeked before)
                    wrap.ClientSend(Channels.DefaultUnreliable, new ArraySegment<byte>(message.bytes));
                    unreliableClientToServer.Dequeue();
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
                QueuedMessage message = reliableServerToClient.Peek();
                if (message.time + reliableLatency <= Time.time)
                {
                    // send and eat (we peeked before)
                    wrap.ServerSend(message.connectionId, Channels.DefaultReliable, new ArraySegment<byte>(message.bytes));
                    reliableServerToClient.Dequeue();
                }
                // not enough time elapsed yet
                break;
            }

            // flush unreliable messages after latency
            while (unreliableServerToClient.Count > 0)
            {
                // check the first message time
                QueuedMessage message = unreliableServerToClient.Peek();
                if (message.time + unreliableLatency <= Time.time)
                {
                    // send and eat (we peeked before)
                    wrap.ServerSend(message.connectionId, Channels.DefaultUnreliable, new ArraySegment<byte>(message.bytes));
                    unreliableServerToClient.Dequeue();
                }
                // not enough time elapsed yet
                break;
            }

            // update wrapped transport too
            wrap.ServerLateUpdate();
        }

        public override int GetMaxBatchSize(int channelId) => wrap.GetMaxBatchSize(channelId);
        public override int GetMaxPacketSize(int channelId = 0) => wrap.GetMaxPacketSize(channelId);

        public override void Shutdown() => wrap.Shutdown();

        public override string ToString() => nameof(PressureDrop) + " " + wrap;
    }
}
