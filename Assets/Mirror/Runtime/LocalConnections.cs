using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // a server's connection TO a LocalClient.
    // sending messages on this connection causes the client's handler function to be invoked directly
    class ULocalConnectionToClient : NetworkConnectionToClient
    {
        internal ULocalConnectionToServer connectionToServer;

        public ULocalConnectionToClient() : base(0)
        {
        }

        public override string address => "localhost";

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            // LocalConnection doesn't support allocation-free sends yet.
            // previously we allocated in Mirror. now we do it here.
            byte[] data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
            connectionToServer.packetQueue.Enqueue(data);
            return true;
        }

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

    // a localClient's connection TO a server.
    // send messages on this connection causes the server's handler function to be invoked directly.
    internal class ULocalConnectionToServer : NetworkConnectionToServer
    {
        internal ULocalConnectionToClient connectionToClient;

        // local client in host mode might call Cmds/Rpcs during Update, but we
        // want to apply them in LateUpdate like all other Transport messages
        // to avoid race conditions. keep packets in Queue until LateUpdate.
        internal Queue<byte[]> packetQueue = new Queue<byte[]>();

        public override string address => "localhost";

        internal override bool Send(ArraySegment<byte> segment, int channelId = Channels.DefaultReliable)
        {
            if (segment.Count == 0)
            {
                Debug.LogError("LocalConnection.SendBytes cannot send zero bytes");
                return false;
            }

            // handle the server's message directly
            connectionToClient.TransportReceive(segment, channelId);
            return true;
        }

        internal void Update()
        {
            // process internal messages so they are applied at the correct time
            while (packetQueue.Count > 0)
            {
                byte[] packet = packetQueue.Dequeue();
                // Treat host player messages exactly like connected client
                // to avoid deceptive / misleading behavior differences
                TransportReceive(new ArraySegment<byte>(packet), Channels.DefaultReliable);
            }
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
