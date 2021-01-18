using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientServer
{
    public class SimpleServerObjectManager : ServerObjectManager
    {
        public void SpawnObjectExpose(GameObject obj, INetworkConnection ownerConnection)
        {
            SpawnObject(obj, ownerConnection);
        }
    }

    [TestFixture]
    public class ServerObjectManagerTest : ClientServerSetup<MockComponent>
    {
        GameObject playerReplacement;

        [Test]
        public void SpawnObjectExposeExceptionTest()
        {
            var gameObject = new GameObject();
            SimpleServerObjectManager comp = gameObject.AddComponent<SimpleServerObjectManager>();

            var obj = new GameObject();

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
                serverObjectManager.Spawn(new GameObject(), new GameObject());
            });

            Assert.That(ex.Message, Is.EqualTo("Player object has no NetworkIdentity"));
        }

        [Test]
        public void SpawnNotPlayerExceptionTest()
        {
            var player = new GameObject();
            player.AddComponent<NetworkIdentity>();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                serverObjectManager.Spawn(new GameObject(), player);
            });

            Assert.That(ex.Message, Is.EqualTo("Player object is not a player in the connection"));
        }

        [UnityTest]
        public IEnumerator ShowForConnection() => UniTask.ToCoroutine(async () =>
        {
            bool invoked = false;

            connectionToServer.RegisterHandler<SpawnMessage>(msg => invoked = true);

            connectionToClient.IsReady = true;

        // call ShowForConnection
        serverObjectManager.ShowForConnection(serverIdentity, connectionToClient);

            connectionToServer.ProcessMessagesAsync().Forget();

            await AsyncUtil.WaitUntilWithTimeout(() => invoked);
        });

        [Test]
        public void SpawnSceneObject()
        {
            serverIdentity.sceneId = 42;
            // unspawned scene objects are set to inactive before spawning
            serverIdentity.gameObject.SetActive(false);
            Assert.That(serverObjectManager.SpawnObjects(), Is.True);
            Assert.That(serverIdentity.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SpawnPrefabObject()
        {
            serverIdentity.sceneId = 0;
            // unspawned scene objects are set to inactive before spawning
            serverIdentity.gameObject.SetActive(false);
            Assert.That(serverObjectManager.SpawnObjects(), Is.True);
            Assert.That(serverIdentity.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ReplacePlayerBaseTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            clientObjectManager.RegisterPrefab(replacementIdentity);

            serverObjectManager.ReplacePlayerForConnection(connectionToClient, client, playerReplacement);

            Assert.That(connectionToClient.Identity, Is.EqualTo(replacementIdentity));
        }

        [Test]
        public void ReplacePlayerDontKeepAuthTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            clientObjectManager.RegisterPrefab(replacementIdentity);

            serverObjectManager.ReplacePlayerForConnection(connectionToClient, client, playerReplacement, true);

            Assert.That(clientIdentity.ConnectionToClient, Is.EqualTo(null));
        }

        [Test]
        public void ReplacePlayerAssetIdTest()
        {
            var replacementGuid = Guid.NewGuid();
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = replacementGuid;
            clientObjectManager.RegisterPrefab(replacementIdentity);

            serverObjectManager.ReplacePlayerForConnection(connectionToClient, client, playerReplacement, replacementGuid);

            Assert.That(connectionToClient.Identity.AssetId, Is.EqualTo(replacementGuid));
        }

        [Test]
        public void AddPlayerForConnectionAssetIdTest()
        {
            var replacementGuid = Guid.NewGuid();
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = replacementGuid;
            clientObjectManager.RegisterPrefab(replacementIdentity);

            connectionToClient.Identity = null;

            Assert.That(serverObjectManager.AddPlayerForConnection(connectionToClient, playerReplacement, replacementGuid), Is.True);
        }

        [UnityTest]
        public IEnumerator RemovePlayerForConnectionTest() => UniTask.ToCoroutine(async () =>
        {
            serverObjectManager.RemovePlayerForConnection(connectionToClient);

            await AsyncUtil.WaitUntilWithTimeout(() => !clientIdentity);

            Assert.That(serverPlayerGO);
        });

        [UnityTest]
        public IEnumerator RemovePlayerForConnectionExceptionTest() => UniTask.ToCoroutine(async () =>
        {
            serverObjectManager.RemovePlayerForConnection(connectionToClient);

            await AsyncUtil.WaitUntilWithTimeout(() => !clientIdentity);

            Assert.Throws<InvalidOperationException>(() =>
            {
                serverObjectManager.RemovePlayerForConnection(connectionToClient);
            });
        });

        [UnityTest]
        public IEnumerator RemovePlayerForConnectionDestroyTest() => UniTask.ToCoroutine(async () =>
        {
            serverObjectManager.RemovePlayerForConnection(connectionToClient, true);

            await AsyncUtil.WaitUntilWithTimeout(() => !clientIdentity);

            Assert.That(!serverPlayerGO);
        });

        [Test]
        public void SpawnObjectExceptionTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                serverObjectManager.SpawnObject(new GameObject(), connectionToClient);
            });
        }

        [Test]
        public void AddPlayerForConnectionFalseTest()
        {
            Assert.That(serverObjectManager.AddPlayerForConnection(connectionToClient, new GameObject()), Is.False);
        }

        [UnityTest]
        public IEnumerator SpawnObjectsFalseTest() => UniTask.ToCoroutine(async () =>
        {
            server.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !server.Active);

            Assert.That(serverObjectManager.SpawnObjects(), Is.False);
        });
    }
}

