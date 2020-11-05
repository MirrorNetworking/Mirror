using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Mirror.Tests.LocalConnections;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    public class SimpleServerObjectManager : ServerObjectManager
    {
        public void SpawnObjectExpose(GameObject obj, INetworkConnection ownerConnection)
        {
            SpawnObject(obj, ownerConnection);
        }
    }

    [TestFixture]
    public class ServerObjectManagerHostTest : HostSetup<MockComponent>
    {
        [Test]
        public void SetClientReadyAndNotReadyTest()
        {
            (_, NetworkConnection connection) = PipedConnections();
            Assert.That(connection.IsReady, Is.False);

            serverObjectManager.SetClientReady(connection);
            Assert.That(connection.IsReady, Is.True);

            serverObjectManager.SetClientNotReady(connection);
            Assert.That(connection.IsReady, Is.False);
        }

        [Test]
        public void SetAllClientsNotReadyTest()
        {
            // add first ready client
            (_, NetworkConnection first) = PipedConnections();
            first.IsReady = true;
            server.connections.Add(first);

            // add second ready client
            (_, NetworkConnection second) = PipedConnections();
            second.IsReady = true;
            server.connections.Add(second);

            // set all not ready
            serverObjectManager.SetAllClientsNotReady();
            Assert.That(first.IsReady, Is.False);
            Assert.That(second.IsReady, Is.False);
        }

        [Test]
        public void GetNetworkIdentity()
        {
            Assert.That(serverObjectManager.GetNetworkIdentity(playerGO), Is.EqualTo(identity));
        }

        [Test]
        public void GetNoNetworkIdentity()
        {
            // create a GameObject without NetworkIdentity
            var goWithout = new GameObject();

            // GetNetworkIdentity for GO without identity
            // (error log is expected)
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = serverObjectManager.GetNetworkIdentity(goWithout);
            });

            // clean up
            Object.Destroy(goWithout);
        }

        [Test]
        public void HideForConnection()
        {
            // add connection

            NetworkConnection connectionToClient = Substitute.For<NetworkConnection>(Substitute.For<IConnection>());

            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();

            serverObjectManager.HideForConnection(identity, connectionToClient);

            connectionToClient.Received().Send(Arg.Is<ObjectHideMessage>(msg => msg.netId == identity.NetId));

            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.Destroy(identity.gameObject);
        }

        [Test]
        public void ValidateSceneObject()
        {
            identity.sceneId = 42;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.True);
            identity.sceneId = 0;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.False);
        }

        [Test]
        public void HideFlagsTest()
        {
            // shouldn't be valid for certain hide flags
            playerGO.hideFlags = HideFlags.NotEditable;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.False);
            playerGO.hideFlags = HideFlags.HideAndDontSave;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.False);
        }

        [Test]
        public void UnSpawn()
        {
            // unspawn
            serverObjectManager.UnSpawn(playerGO);

            // it should have been marked for reset now
            Assert.That(identity.NetId, Is.Zero);
        }
    }

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
