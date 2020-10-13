using System;
using System.Net.Sockets;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpTransport : Transport
    {
        void Awake()
        {
            Debug.Log("KCPTransport initialized!");
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // client
        public override bool ClientConnected() => throw new NotImplementedException();
        public override void ClientConnect(string address) => throw new NotImplementedException();
        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            throw new NotImplementedException();
        }

        public override void ClientDisconnect() => throw new NotImplementedException();

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled)
                return;
        }

        // server
        public override bool ServerActive() => throw new NotImplementedException();
        public override void ServerStart() => throw new NotImplementedException();
        public override bool ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            throw new NotImplementedException();
        }
        public override bool ServerDisconnect(int connectionId) => throw new NotImplementedException();
        public override string ServerGetClientAddress(int connectionId)
        {
            throw new NotImplementedException();
        }
        public override void ServerStop() => throw new NotImplementedException();

        // common
        public override void Shutdown()
        {
            throw new NotImplementedException();
        }

        // MTU
        public override ushort GetMaxPacketSize() => Kcp.MTU_DEF;

        public override string ToString()
        {
            return "KCP";
        }
    }
}
