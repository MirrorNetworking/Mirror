using System;
using System.Collections;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class SimpleNetworkServer : NetworkServer
    {
        public void SpawnObjectExpose(GameObject obj, INetworkConnection ownerConnection)
        {
            SpawnObject(obj, ownerConnection);
        }
    }

    public class NetworkServerTests : ClientServerSetup<MockComponent>
    {
        WovenTestMessage message;
        GameObject playerReplacement;

        public override void ExtraSetup()
        {
            message = new WovenTestMessage
            {
                IntValue = 1,
                DoubleValue = 1.0,
                StringValue = "hello"
            };

        }

        [Test]
        public void InitializeTest()
        {
            Assert.That(server.connections, Has.Count.EqualTo(1));
            Assert.That(server.Active);
            Assert.That(server.LocalClientActive, Is.False);
        }

        [Test]
        public void SendToClientOfPlayerExceptionTest()
        {
            SimpleNetworkServer comp = serverPlayerGO.AddComponent<SimpleNetworkServer>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                comp.SendToClientOfPlayer<CommandMessage>(null, new CommandMessage());
            });
        }

        [Test]
        public void SpawnObjectExposeExceptionTest()
        {
            GameObject gameObject = new GameObject();
            SimpleNetworkServer comp = gameObject.AddComponent<SimpleNetworkServer>();

            GameObject obj = new GameObject();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                comp.SpawnObjectExpose(obj, connectionToServer);
            });

            Assert.That(ex.Message, Is.EqualTo("SpawnObject for " + obj + ", NetworkServer is not active. Cannot spawn objects without an active server."));
        }

        [Test]
        public void SpawnNoIdentExceptionTest()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                server.Spawn(new GameObject(), new GameObject());
            });

            Assert.That(ex.Message, Is.EqualTo("Player object has no NetworkIdentity"));
        }

        [Test]
        public void SpawnNotPlayerExceptionTest()
        {
            GameObject player = new GameObject();
            player.AddComponent<NetworkIdentity>();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                server.Spawn(new GameObject(), player);
            });

            Assert.That(ex.Message, Is.EqualTo("Player object is not a " + nameof(player) + "."));
        }

        [UnityTest]
        public IEnumerator ReadyMessageSetsClientReadyTest()
        {
            connectionToServer.Send(new ReadyMessage());

            yield return null;

            // ready?
            Assert.That(connectionToClient.IsReady, Is.True);
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

            server.SendToClientOfPlayer(serverIdentity, message);

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

            connectionToClient.IsReady = true;

            // call ShowForConnection
            server.ShowForConnection(serverIdentity, connectionToClient);

            _ = connectionToServer.ProcessMessagesAsync();

            yield return null;

            func.Received().Invoke(
                Arg.Any<SpawnMessage>());
        }

        [Test]
        public void SpawnSceneObject()
        {
            serverIdentity.sceneId = 42;
            // unspawned scene objects are set to inactive before spawning
            serverIdentity.gameObject.SetActive(false);
            Assert.That(server.SpawnObjects(), Is.True);
            Assert.That(serverIdentity.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SpawnPrefabObject()
        {
            serverIdentity.sceneId = 0;
            // unspawned scene objects are set to inactive before spawning
            serverIdentity.gameObject.SetActive(false);
            Assert.That(server.SpawnObjects(), Is.True);
            Assert.That(serverIdentity.gameObject.activeSelf, Is.False);
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
            Action<INetworkConnection, WovenTestMessage> func = Substitute.For<Action<INetworkConnection, WovenTestMessage>>();

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

        int ServerChangeCalled;
        public void ServerChangeScene(string newSceneName)
        {
            ServerChangeCalled++;
        }

        [Test]
        public void ServerChangeSceneTest()
        {
            server.ServerChangeScene.AddListener(ServerChangeScene);
            server.OnServerChangeScene("");
            Assert.That(ServerChangeCalled, Is.EqualTo(1));
        }

        int ServerSceneChangedCalled;
        public void ServerSceneChanged(string sceneName)
        {
            ServerSceneChangedCalled++;
        }

        [Test]
        public void ServerSceneChangedTest()
        {
            server.ServerSceneChanged.AddListener(ServerSceneChanged);
            server.OnServerSceneChanged("");
            Assert.That(ServerSceneChangedCalled, Is.EqualTo(1));
        }

        [Test]
        public void ReplacePlayerBaseTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            client.RegisterPrefab(playerReplacement);

            server.ReplacePlayerForConnection(connectionToClient, client, playerReplacement);

            Assert.That(connectionToClient.Identity, Is.EqualTo(replacementIdentity));
        }

        [Test]
        public void ReplacePlayerDontKeepAuthTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            client.RegisterPrefab(playerReplacement);

            server.ReplacePlayerForConnection(connectionToClient, client, playerReplacement, true);

            Assert.That(clientIdentity.ConnectionToClient, Is.EqualTo(null));
        }

        [Test]
        public void ReplacePlayerAssetIdTest()
        {
            Guid replacementGuid = Guid.NewGuid();
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = replacementGuid;
            client.RegisterPrefab(playerReplacement);

            server.ReplacePlayerForConnection(connectionToClient, client, playerReplacement, replacementGuid);

            Assert.That(connectionToClient.Identity.AssetId, Is.EqualTo(replacementGuid));
        }

        [Test]
        public void NumPlayersTest()
        {
            Assert.That(server.NumPlayers, Is.EqualTo(1));
        }

        [Test]
        public void AddPlayerForConnectionAssetIdTest()
        {
            Guid replacementGuid = Guid.NewGuid();
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = replacementGuid;
            client.RegisterPrefab(playerReplacement);

            connectionToClient.Identity = null;

            Assert.That(server.AddPlayerForConnection(connectionToClient, playerReplacement, replacementGuid), Is.True);
        }
    }
}
