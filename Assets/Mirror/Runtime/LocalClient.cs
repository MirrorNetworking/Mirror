using System;
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
        bool m_Connected;

        public override void Disconnect()
        {
            ClientScene.HandleClientDisconnect(connection);
            if (m_Connected)
            {
                packetQueue.Enqueue(Protocol.PackMessage((ushort)MsgType.Disconnect, new EmptyMessage()));
                m_Connected = false;
            }
            connectState = ConnectState.Disconnected;
            NetworkServer.RemoveLocalClient(connection);
        }

        internal void InternalConnectLocalServer()
        {
            connection = new ULocalConnectionToServer();
            SetHandlers(connection);
            connection.connectionId = NetworkServer.AddLocalClient(this);
            connectState = ConnectState.Connected;

            active = true;
            RegisterSystemHandlers(true);

            packetQueue.Enqueue(Protocol.PackMessage((ushort)MsgType.Connect, new EmptyMessage()));

            m_Connected = true;
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
                localPlayer.EnableIsClient();
                NetworkIdentity.spawned[localPlayer.netId] = localPlayer;
                localPlayer.SetConnectionToServer(connection);
            }
            // there is no SystemOwnerMessage for local client. add to ClientScene here instead
            ClientScene.InternalAddPlayer(localPlayer);
        }
    }
}
