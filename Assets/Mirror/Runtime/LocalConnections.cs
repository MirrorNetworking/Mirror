using System;
using UnityEngine;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    class ULocalConnectionToClient : NetworkConnectionToClient
    {
        internal ULocalConnectionToServer connectionToServer;

        public ULocalConnectionToClient() : base(LocalConnectionId) { }

        public override string address => "localhost";

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            connectionToServer.buffer.Write(segment);

            return true;
        }

        // override for host client: always return true.
        internal override bool IsClientAlive() => true;

        internal void DisconnectInternal()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            RemoveObservers();
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect()
        {
            DisconnectInternal();
            connectionToServer.DisconnectInternal();
        }
    }

    internal class LocalConnectionBuffer
    {
        readonly NetworkWriter writer = new NetworkWriter();
        readonly NetworkReader reader = new NetworkReader(default(ArraySegment<byte>));
        // The buffer is atleast 1500 bytes long. So need to keep track of
        // packet count to know how many ArraySegments are in the buffer
        int packetCount;

        public void Write(ArraySegment<byte> segment)
        {
            writer.WriteBytesAndSizeSegment(segment);
            packetCount++;

            // update buffer incase writer's length has changed
            reader.buffer = writer.ToArraySegment();
        }

        public bool HasPackets()
        {
            return packetCount > 0;
        }
        public ArraySegment<byte> GetNextPacket()
        {
            ArraySegment<byte> packet = reader.ReadBytesAndSizeSegment();
            packetCount--;

            return packet;
        }

        public void ResetBuffer()
        {
            writer.Reset();
            reader.Position = 0;
        }
    }

    // a localClient's connection TO a server.
    // send messages on this connection causes the server's handler function to be invoked directly.
    internal class ULocalConnectionToServer : NetworkConnectionToServer
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(ULocalConnectionToClient));

        internal ULocalConnectionToClient connectionToClient;
        internal readonly LocalConnectionBuffer buffer = new LocalConnectionBuffer();

        public override string address => "localhost";

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (segment.Count == 0)
            {
                logger.LogError("LocalConnection.SendBytes cannot send zero bytes");
                return false;
            }

            // handle the server's message directly
            connectionToClient.TransportReceive(segment, channelId);
            return true;
        }

        internal void Update()
        {
            // process internal messages so they are applied at the correct time
            while (buffer.HasPackets())
            {
                ArraySegment<byte> packet = buffer.GetNextPacket();

                // Treat host player messages exactly like connected client
                // to avoid deceptive / misleading behavior differences
                TransportReceive(packet, Channels.DefaultReliable);
            }

            buffer.ResetBuffer();
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        internal void DisconnectInternal()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            ClientScene.HandleClientDisconnect(this);
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect()
        {
            connectionToClient.DisconnectInternal();
            DisconnectInternal();
        }
    }
}
