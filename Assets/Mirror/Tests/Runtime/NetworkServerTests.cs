using System;
using System.Collections;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class NetworkServerTests
    {
        NetworkServer server;
        GameObject serverGO;

        NetworkConnection connectionToServer;

        NetworkConnection connectionToClient;
        WovenTestMessage message;
        NetworkIdentity identity;

        [UnitySetUp]
        public IEnumerator SetupNetworkServer() => RunAsync(async () =>
        {
            serverGO = new GameObject();
            var transport = serverGO.AddComponent<LoopbackTransport>();
            server = serverGO.AddComponent<NetworkServer>();
            await server.ListenAsync();

            IConnection tconn = await transport.ConnectAsync(new System.Uri("tcp4://localhost"));

            connectionToClient = server.connections.First();
            connectionToServer = new NetworkConnection(tconn);

            message = new WovenTestMessage
            {
                IntValue = 1,
                DoubleValue = 1.0,
                StringValue = "hello"
            };

            identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.ConnectionToClient = connectionToClient;

        });

        [TearDown]
        public void ShutdownNetworkServer()
        {
            GameObject.DestroyImmediate(identity.gameObject);
            server.Disconnect();
            GameObject.DestroyImmediate(serverGO);
        }


        [Test]
        public void InitializeTest()
        {
            Assert.That(server.connections, Has.Count.EqualTo(1));
            Assert.That(server.Active);
            Assert.That(server.LocalClientActive, Is.False);
        }

        [Test]
        public void SpawnTest()
        {
            var gameObject = new GameObject();
            gameObject.AddComponent<NetworkIdentity>();
            server.Spawn(gameObject);

            Assert.That(gameObject.GetComponent<NetworkIdentity>().Server, Is.SameAs(server));
        }

        [UnityTest]
        public IEnumerator ReadyMessageSetsClientReadyTest()
        {
            connectionToServer.Send(new ReadyMessage());

            yield return null;

            // ready?
            Assert.That(connectionToClient.isReady, Is.True);
        }

        [UnityTest]
        public IEnumerator SendToAll()
        {
            Action<WovenTestMessage> func = Substitute.For<Action<WovenTestMessage>>();

            connectionToServer.RegisterHandler(func);

            server.SendToAll(message);

            _ = connectionToServer.ProcessMessagesAsync();

            yield return null;

            func.Received().Invoke(
                Arg.Is<WovenTestMessage>(msg => msg.Equals(message)
            ));
        }

        [UnityTest]
        public IEnumerator SendToClientOfPlayer()
        {
            Action<WovenTestMessage> func = Substitute.For<Action<WovenTestMessage>>();

            connectionToServer.RegisterHandler(func);

            server.SendToClientOfPlayer(identity, message);

            _ = connectionToServer.ProcessMessagesAsync();

            yield return null;

            func.Received().Invoke(
                Arg.Is<WovenTestMessage>(msg => msg.Equals(message)
            ));
        }

        [UnityTest]
        public IEnumerator ShowForConnection()
        {
            Action<SpawnMessage> func = Substitute.For<Action<SpawnMessage>>();
            connectionToServer.RegisterHandler(func);

            connectionToClient.isReady = true;

            // call ShowForConnection
            server.ShowForConnection(identity, connectionToClient);

            _ = connectionToServer.ProcessMessagesAsync();

            yield return null;

            func.Received().Invoke(
                Arg.Any<SpawnMessage>());
        }

        [Test]
        public void SpawnSceneObject()
        {
            identity.sceneId = 42;
            // unspawned scene objects are set to inactive before spawning
            identity.gameObject.SetActive(false);
            Assert.That(server.SpawnObjects(), Is.True);
            Assert.That(identity.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SpawnPrefabObject()
        {
            identity.sceneId = 0;
            // unspawned scene objects are set to inactive before spawning
            identity.gameObject.SetActive(false);
            Assert.That(server.SpawnObjects(), Is.True);
            Assert.That(identity.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RegisterMessage1()
        {
            Action<WovenTestMessage> func = Substitute.For<Action<WovenTestMessage>>();

            connectionToClient.RegisterHandler(func);
            connectionToServer.Send(message);

            yield return null;

            func.Received().Invoke(
                Arg.Is<WovenTestMessage>(msg => msg.Equals(message)
            ));
        }

        [UnityTest]
        public IEnumerator RegisterMessage2()
        {

            Action<NetworkConnection, WovenTestMessage> func = Substitute.For<Action<NetworkConnection, WovenTestMessage>>();

            connectionToClient.RegisterHandler<WovenTestMessage>(func);

            connectionToServer.Send(message);

            yield return null;

            func.Received().Invoke(
                connectionToClient,
                Arg.Is<WovenTestMessage>(msg => msg.Equals(message)
            ));
        }

        [UnityTest]
        public IEnumerator UnRegisterMessage1()
        {
            Action<WovenTestMessage> func = Substitute.For<Action<WovenTestMessage>>();

            connectionToClient.RegisterHandler(func);
            connectionToClient.UnregisterHandler<WovenTestMessage>();

            connectionToServer.Send(message);

            yield return null;

            func.Received(0).Invoke(
                Arg.Any<WovenTestMessage>());
        }

    }
}
