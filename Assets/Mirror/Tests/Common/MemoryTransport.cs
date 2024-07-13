// memory transport for easier testing
// note: file needs to be outside of Editor folder, otherwise AddComponent
//       can't be called with MemoryTransport
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Tests
{
    public class MemoryTransport : Transport
    {
        public enum EventType { Connected, Data, Disconnected }
        public struct Message
        {
            public int connectionId;
            public EventType eventType;
            public byte[] data;
            public Message(int connectionId, EventType eventType, byte[] data)
            {
                this.connectionId = connectionId;
                this.eventType = eventType;
                this.data = data;
            }
        }

        bool clientConnected;
        public Queue<Message> clientIncoming = new Queue<Message>();
        bool serverActive;
        public Queue<Message> serverIncoming = new Queue<Message>();

        public override bool Available() => true;
        // limit max size to something reasonable so pool doesn't allocate
        // int.MaxValue = 2GB each time.
        public override int GetMaxPacketSize(int channelId) => ushort.MaxValue;
        // 1400 max batch size
        // -> need something != GetMaxPacketSize for testing
        // -> MTU aka 1400 is used a lot anyway
        public override int GetBatchThreshold(int channelId) => 1400;
        public override void Shutdown() {}
        public override bool ClientConnected() => clientConnected;
        public override void ClientConnect(string address)
        {
            // only if server is running
            if (serverActive)
            {
                // add server connected message with connId=1 because 0 is reserved
                serverIncoming.Enqueue(new Message(1, EventType.Connected, null));

                // add client connected message
                clientIncoming.Enqueue(new Message(0, EventType.Connected, null));

                clientConnected = true;
            }
        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            // only  if client connected
            if (clientConnected)
            {
                // a real transport fails for > max sized messages.
                // mirror checks it, but let's guarantee that we catch > max
                // sized message send attempts just like a real transport would.
                // => helps to cover packet size issues i.e. for timestamp
                //    batching tests
                int max = GetMaxPacketSize(channelId);
                if (segment.Count > max)
                    throw new Exception($"MemoryTransport ClientSend of {segment.Count} bytes exceeds max of {max} bytes");

                // copy segment data because it's only valid until return
                byte[] data = new byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);

                // add server data message with connId=1 because 0 is reserved
                serverIncoming.Enqueue(new Message(1, EventType.Data, data));

                // call event. might be null if no statistics are listening etc.
                OnClientDataSent?.Invoke(segment, channelId);
            }
        }
        public override void ClientDisconnect()
        {
            // only  if client connected
            if (clientConnected)
            {
                // clear all pending messages that we may have received.
                // over the wire, we wouldn't receive any more pending messages
                // ether after calling disconnect.
                clientIncoming.Clear();

                // add server disconnected message with connId=1 because 0 is reserved
                serverIncoming.Enqueue(new Message(1, EventType.Disconnected, null));

                // add client disconnected message
                clientIncoming.Enqueue(new Message(0, EventType.Disconnected, null));

                // not connected anymore
                clientConnected = false;
            }
        }
        // messages should always be processed in early update
        public override void ClientEarlyUpdate()
        {
            // note: process even if not connected because when calling
            // Disconnect, we add a Disconnected event which still needs to be
            // processed here.
            while (clientIncoming.Count > 0)
            {
                Message message = clientIncoming.Dequeue();
                switch (message.eventType)
                {
                    case EventType.Connected:
                        Debug.Log("MemoryTransport Client Message: Connected");
                        // event might be null in tests if no NetworkClient is used.
                        OnClientConnected?.Invoke();
                        break;
                    case EventType.Data:
                        Debug.Log($"MemoryTransport Client Message: Data: {BitConverter.ToString(message.data)}");
                        // event might be null in tests if no NetworkClient is used.
                        OnClientDataReceived?.Invoke(new ArraySegment<byte>(message.data), 0);
                        break;
                    case EventType.Disconnected:
                        Debug.Log("MemoryTransport Client Message: Disconnected");
                        // event might be null in tests if no NetworkClient is used.
                        OnClientDisconnected?.Invoke();
                        break;
                }
            }
        }

        public override bool ServerActive() => serverActive;
        public override Uri ServerUri() => throw new NotImplementedException();
        public override void ServerStart() { serverActive = true; }
        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            // only if server is running and client is connected
            if (serverActive && clientConnected)
            {
                // a real transport fails for > max sized messages.
                // mirror checks it, but let's guarantee that we catch > max
                // sized message send attempts just like a real transport would.
                // => helps to cover packet size issues i.e. for timestamp
                //    batching tests
                int max = GetMaxPacketSize(channelId);
                if (segment.Count > max)
                    throw new Exception($"MemoryTransport ServerSend of {segment.Count} bytes exceeds max of {max} bytes");

                // copy segment data because it's only valid until return
                byte[] data = new byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);

                // add client data message
                clientIncoming.Enqueue(new Message(0, EventType.Data, data));

                // call event. might be null if no statistics are listening etc.
                OnServerDataSent?.Invoke(connectionId, segment, channelId);
            }
        }

        public override void ServerDisconnect(int connectionId)
        {
            // clear all pending messages that we may have received.
            // over the wire, we wouldn't receive any more pending messages
            // ether after calling disconnect.
            serverIncoming.Clear();

            // add client disconnected message with connectionId
            clientIncoming.Enqueue(new Message(connectionId, EventType.Disconnected, null));

            // add server disconnected message with connectionId
            serverIncoming.Enqueue(new Message(connectionId, EventType.Disconnected, null));

            // not active anymore
            serverActive = false;
        }

        public override string ServerGetClientAddress(int connectionId) => string.Empty;
        public override void ServerStop()
        {
            // clear all pending messages that we may have received.
            // over the wire, we wouldn't receive any more pending messages
            // ether after calling stop.
            serverIncoming.Clear();

            // add client disconnected message
            clientIncoming.Enqueue(new Message(0, EventType.Disconnected, null));

            // add server disconnected message with connId=1 because 0 is reserved
            serverIncoming.Enqueue(new Message(1, EventType.Disconnected, null));

            // not active anymore
            serverActive = false;
        }
        // messages should always be processed in early update
        public override void ServerEarlyUpdate()
        {
            while (serverIncoming.Count > 0)
            {
                Message message = serverIncoming.Dequeue();
                switch (message.eventType)
                {
                    case EventType.Connected:
                        Debug.Log("MemoryTransport Server Message: Connected");
                        // event might be null in tests if no NetworkClient is used.
                        OnServerConnectedWithAddress?.Invoke(message.connectionId, "");
                        break;
                    case EventType.Data:
                        Debug.Log($"MemoryTransport Server Message: Data: {BitConverter.ToString(message.data)}");
                        // event might be null in tests if no NetworkClient is used.
                        OnServerDataReceived?.Invoke(message.connectionId, new ArraySegment<byte>(message.data), 0);
                        break;
                    case EventType.Disconnected:
                        Debug.Log("MemoryTransport Server Message: Disconnected");
                        // event might be null in tests if no NetworkClient is used.
                        OnServerDisconnected?.Invoke(message.connectionId);
                        break;
                }
            }
        }
    }
}
