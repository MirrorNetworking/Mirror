using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    sealed class LocalClient : NetworkClient
    {
        // local client in host mode might call Cmds/Rpcs during Update, but we
        // want to apply them in LateUpdate like all other Transport messages
        // to avoid race conditions.
        // -> that's why there is an internal message queue.
        Queue<NetworkMessage> m_InternalMsgs = new Queue<NetworkMessage>();
        bool m_Connected;

        public override void Disconnect()
        {
            ClientScene.HandleClientDisconnect(connection);
            if (m_Connected)
            {
                PostInternalMessage((short)MsgType.Disconnect);
                m_Connected = false;
            }
            connectState = ConnectState.Disconnected;
            NetworkServer.RemoveLocalClient(connection);
        }

        internal void InternalConnectLocalServer(bool generateConnectMsg)
        {
            connection = new ULocalConnectionToServer();
            SetHandlers(connection);
            connection.connectionId = NetworkServer.AddLocalClient(this);
            connectState = ConnectState.Connected;

            active = true;
            RegisterSystemHandlers(true);

            if (generateConnectMsg)
            {
                PostInternalMessage((short)MsgType.Connect);
            }
            m_Connected = true;
        }

        internal override void Update()
        {
            // process internal messages so they are applied at the correct time
            while (m_InternalMsgs.Count > 0)
            {
                NetworkMessage internalMessage = m_InternalMsgs.Dequeue();
                connection.InvokeHandler(internalMessage);
                connection.lastMessageTime = Time.time;
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

        void PostInternalMessage(short msgType, NetworkReader contentReader)
        {
            NetworkMessage msg = new NetworkMessage
            {
                msgType = msgType,
                reader = contentReader,
                conn = connection
            };
            m_InternalMsgs.Enqueue(msg);
        }

        void PostInternalMessage(short msgType)
        {
            // call PostInternalMessage with empty content array if we just want to call a message like Connect
            // -> original NetworkTransport used empty [] and not null array for those messages too
            PostInternalMessage(msgType, new NetworkReader(new byte[0]));
        }

        // called by the server, to bypass network
        internal void InvokeBytesOnClient(byte[] buffer)
        {
            // unpack message and post to internal list for processing
            NetworkReader reader = new NetworkReader(buffer);
            if (Protocol.UnpackMessage(reader, out ushort msgType))
            {
                PostInternalMessage((short)msgType, reader);
            }
            else Debug.LogError("InvokeBytesOnClient failed to unpack message: " + BitConverter.ToString(buffer));
        }
    }
}
