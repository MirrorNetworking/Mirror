using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    sealed class LocalClient : NetworkClient
    {
        // local client in host mode might call Cmds/Rpcs during Update, but we
        // want to apply them in LateUpdate like all other Transport messages
        // to avoid race conditions. keep packets in Queue until LateUpdate.
        internal Queue<byte[]> packetQueue = new Queue<byte[]>();

        internal void InternalConnectLocalServer()
        {
            // create local connection to server
            connection = new ULocalConnectionToServer()
            {
                // local player always has connectionId == 0
                connectionId = 0
            };
            SetHandlers(connection);

            // create server connection to local client
            ULocalConnectionToClient connectionToClient = new ULocalConnectionToClient(this)
            {
                // local player always has connectionId == 0
                connectionId = 0
            };
            NetworkServer.SetLocalConnection(connectionToClient);

            connectState = ConnectState.Connected;

            active = true;
            RegisterSystemHandlers(true);

            packetQueue.Enqueue(MessagePacker.Pack(new ConnectMessage()));
        }

        public override void Disconnect()
        {
            connectState = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(connection);
            if (isConnected)
            {
                packetQueue.Enqueue(MessagePacker.Pack(new DisconnectMessage()));
            }
            NetworkServer.RemoveLocalConnection();
        }

        internal override void Update()
        {
            // process internal messages so they are applied at the correct time
            while (packetQueue.Count > 0)
            {
                byte[] packet = packetQueue.Dequeue();
                OnDataReceived(packet);
            }
        }

        // Called by the server to set the LocalClient's LocalPlayer object during NetworkServer.AddPlayer()
        internal void AddLocalPlayer(NetworkIdentity localPlayer)
        {
            if (LogFilter.Debug) Debug.Log("Local client AddLocalPlayer " + localPlayer.gameObject.name + " conn=" + connection.connectionId);
            connection.isReady = true;
            connection.SetPlayerController(localPlayer);
            if (localPlayer != null)
            {
                localPlayer.isClient = true;
                NetworkIdentity.spawned[localPlayer.netId] = localPlayer;
                localPlayer.connectionToServer = connection;
            }
            // there is no SystemOwnerMessage for local client. add to ClientScene here instead
            ClientScene.InternalAddPlayer(localPlayer);
        }
    }
}
