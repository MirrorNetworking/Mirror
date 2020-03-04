using System;
using System.Collections;
using System.Collections.Generic;
using Mirror.Tcp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{

    public class RpcComponent : NetworkBehaviour
    {
        public int cmdArg1;
        public string cmdArg2;

        [Command]
        public void CmdTest(int arg1, string arg2)
        {
            this.cmdArg1 = arg1;
            this.cmdArg2 = arg2;
        }

        public int rpcArg1;
        public string rpcArg2;

        [ClientRpc]
        public void RpcTest(int arg1, string arg2)
        {
            this.rpcArg1 = arg1;
            this.rpcArg2 = arg2;
        }

        public int targetRpcArg1;
        public string targetRpcArg2;

        [TargetRpc]
        public void TargetRpcTest(NetworkConnection conn, int arg1, string arg2)
        {
            this.targetRpcArg1 = arg1;
            this.targetRpcArg2 = arg2;
        }
    }

    public class RpcTests
    {
        GameObject networkManagerGo;
        NetworkManager manager;
        GameObject gameObject;

        RpcComponent rpcComponent;
        NetworkIdentity identity;

        [SetUp]
        public void SetupNetworkServer()
        {
            networkManagerGo = new GameObject();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            manager.client = networkManagerGo.GetComponent<NetworkClient>();
            manager.server = networkManagerGo.GetComponent<NetworkServer>();
            Transport transport = networkManagerGo.AddComponent<TcpTransport>();
            Transport.activeTransport = transport;

            manager.autoCreatePlayer = false;

            manager.StartHost();

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
            rpcComponent = gameObject.AddComponent<RpcComponent>();

            manager.server.AddPlayerForConnection(manager.server.localConnection, gameObject);
        }

        [TearDown]
        public void ShutdownNetworkServer()
        {

            GameObject.DestroyImmediate(gameObject);
            manager.StopHost();
            GameObject.DestroyImmediate(networkManagerGo);
        }

        [Test]
        public void Command()
        {
            // process spawn message from server
            manager.client.Update();

            rpcComponent.CmdTest(1, "hello");

            Assert.That(rpcComponent.cmdArg1, Is.EqualTo(1));
            Assert.That(rpcComponent.cmdArg2, Is.EqualTo("hello"));
        }

        [Test]
        public void ClientRpc()
        {

            rpcComponent.RpcTest(1, "hello");
            // process spawn message from server
            manager.client.Update();

            Assert.That(rpcComponent.rpcArg1, Is.EqualTo(1));
            Assert.That(rpcComponent.rpcArg2, Is.EqualTo("hello"));
        }

        [Test]
        public void TargetRpc()
        {

            rpcComponent.TargetRpcTest(manager.server.localConnection, 1, "hello");
            // process spawn message from server
            manager.client.Update();

            Assert.That(rpcComponent.targetRpcArg1, Is.EqualTo(1));
            Assert.That(rpcComponent.targetRpcArg2, Is.EqualTo("hello"));
        }
    }
}
