using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    sealed class LocalClient : NetworkClient
    {
        struct InternalMsg
        {
            internal ushort msgType;
            internal byte[] content;
        }

        List<InternalMsg> m_InternalMsgs = new List<InternalMsg>();

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
            if (LogFilter.logDev) Debug.Log("Local client AddLocalPlayer " + localPlayer.gameObject.name + " conn=" + m_Connection.connectionId);
            m_Connection.isReady = true;
            m_Connection.SetPlayerController(localPlayer);
            NetworkIdentity uv = localPlayer;
            if (uv != null)
            {
                ClientScene.SetLocalObject(uv.netId, localPlayer.gameObject);
                uv.SetConnectionToServer(m_Connection);
            }
            // there is no SystemOwnerMessage for local client. add to ClientScene here instead
            ClientScene.InternalAddPlayer(uv);
        }

        private void PostInternalMessage(short msgType, byte[] content)
        {
            InternalMsg msg = new InternalMsg();
            msg.msgType = (ushort)msgType;
            msg.content = content;
            m_InternalMsgs.Add(msg);
        }

        private void PostInternalMessage(short msgType)
        {
            // call PostInternalMessage with empty content array if we just want to call a message like Connect
            // -> original NetworkTransport used empty [] and not null array for those messages too
            PostInternalMessage(msgType, new byte[0]);
        }

        private void ProcessInternalMessages()
        {
            if (m_InternalMsgs.Count == 0)
            {
                return;
            }

            // we will process all existing messages, 
            // but new messages might be added in the process
            // so we work on a copy of the list
            List<InternalMsg> tmp = new List<InternalMsg>(m_InternalMsgs);
            m_InternalMsgs.Clear();

            // iterate through existing set
            for (int i = 0; i < tmp.Count; i++)
            {
                InternalMsg msg = tmp[i];

                NetworkMessage internalMessage = new NetworkMessage();
                internalMessage.msgType = (short)msg.msgType;
                internalMessage.reader = new NetworkReader(msg.content);
                internalMessage.conn = connection;

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
            else if (LogFilter.logError) Debug.LogError("InvokeBytesOnClient failed to unpack message: " + BitConverter.ToString(buffer));
        }
    }
}
