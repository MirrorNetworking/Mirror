using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    sealed class LocalClient : NetworkClient
    {
        Queue<NetworkMessage> m_InternalMsgs = new Queue<NetworkMessage>();
        bool m_Connected;

        public override void Disconnect()
        {
            ClientScene.HandleClientDisconnect(m_Connection);
            if (m_Connected)
            {
                PostInternalMessage((short)MsgType.Disconnect);
                m_Connected = false;
            }
            connectState = ConnectState.Disconnected;
            NetworkServer.RemoveLocalClient(m_Connection);
        }

        internal void InternalConnectLocalServer(bool generateConnectMsg)
        {
            m_Connection = new ULocalConnectionToServer();
            SetHandlers(m_Connection);
            m_Connection.connectionId = NetworkServer.AddLocalClient(this);
            connectState = ConnectState.Connected;

            SetActive(true);
            RegisterSystemHandlers(true);

            if (generateConnectMsg)
            {
                PostInternalMessage((short)MsgType.Connect);
            }
            m_Connected = true;
        }

        internal override void Update()
        {
            ProcessInternalMessages();
        }

        // Called by the server to set the LocalClient's LocalPlayer object during NetworkServer.AddPlayer()
        internal void AddLocalPlayer(NetworkIdentity localPlayer)
        {
            if (LogFilter.Debug) Debug.Log("Local client AddLocalPlayer " + localPlayer.gameObject.name + " conn=" + m_Connection.connectionId);
            m_Connection.isReady = true;
            m_Connection.SetPlayerController(localPlayer);
            if (localPlayer != null)
            {
                localPlayer.EnableIsClient();
                NetworkIdentity.spawned[localPlayer.netId] = localPlayer;
                localPlayer.SetConnectionToServer(m_Connection);
            }
            // there is no SystemOwnerMessage for local client. add to ClientScene here instead
            ClientScene.InternalAddPlayer(localPlayer);
        }

        void PostInternalMessage(short msgType, byte[] content)
        {
            NetworkMessage msg = new NetworkMessage();
            msg.msgType = msgType;
            msg.reader = new NetworkReader(content);
            msg.conn = connection;
            m_InternalMsgs.Enqueue(msg);
        }

        void PostInternalMessage(short msgType)
        {
            // call PostInternalMessage with empty content array if we just want to call a message like Connect
            // -> original NetworkTransport used empty [] and not null array for those messages too
            PostInternalMessage(msgType, new byte[0]);
        }

        void ProcessInternalMessages()
        {
            while (m_InternalMsgs.Count > 0)
            {
                NetworkMessage internalMessage = m_InternalMsgs.Dequeue();
                m_Connection.InvokeHandler(internalMessage);
                connection.lastMessageTime = Time.time;
            }
        }

        // called by the server, to bypass network
        internal void InvokeBytesOnClient(byte[] buffer)
        {
            // unpack message and post to internal list for processing
            ushort msgType;
            byte[] content;
            if (Protocol.UnpackMessage(buffer, out msgType, out content))
            {
                PostInternalMessage((short)msgType, content);
            }
            else Debug.LogError("InvokeBytesOnClient failed to unpack message: " + BitConverter.ToString(buffer));
        }
    }
}
