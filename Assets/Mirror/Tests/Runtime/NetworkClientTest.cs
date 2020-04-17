using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    public class TestClientAuthenticator : NetworkAuthenticator
    {
        public int called;

        public override void OnClientAuthenticate(INetworkConnection conn)
        {
            ++called;
        }
    }

    [TestFixture]
    public class NetworkClientTest : HostSetup<MockComponent>
    {
        [Test]
        public void IsConnectedTest()
        {
            Assert.That(client.IsConnected);
        }

        [Test]
        public void ConnectionTest()
        {
            Assert.That(client.Connection != null);
        }

        [UnityTest]
        public IEnumerator LocalPlayerTest()
        {
            Assert.That(client.LocalPlayer == null);

            PlayerSpawner spawner = networkManagerGo.AddComponent<PlayerSpawner>();

            spawner.server = server;
            spawner.client = client;
            spawner.playerPrefab = identity;
            spawner.Start();

            client.ConnectHost(server);

            yield return null;

            Assert.That(client.LocalPlayer != null);
        }

        [Test]
        public void CurrentTest()
        {
            Assert.That(NetworkClient.Current == null);
        }

        [Test]
        public void ReadyTest()
        {
            client.Ready(client.Connection);
            Assert.That(client.ready);
            Assert.That(client.Connection.IsReady);
        }

        [Test]
        public void ReadyTwiceTest()
        {
            client.Ready(client.Connection);

            Assert.Throws<InvalidOperationException>(() =>
            {
                client.Ready(client.Connection);
            });
        }

        [Test]
        public void ReadyNull()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                client.Ready(null);
            });
        }

        [UnityTest]
        public IEnumerator RemovePlayerTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = client.RemovePlayer();
            });

            PlayerSpawner spawner = networkManagerGo.AddComponent<PlayerSpawner>();

            spawner.server = server;
            spawner.client = client;
            spawner.playerPrefab = identity;
            spawner.Start();

            client.ConnectHost(server);

            yield return null;

            Assert.That(client.LocalPlayer != null);

            Assert.That(client.RemovePlayer());
            Assert.That(identity == null);
            Assert.That(client.LocalPlayer == null);
        }

        [Test]
        public void RegisterPrefabExceptionTest()
        {
            var gameObject = new GameObject();
            Assert.Throws<InvalidOperationException>(() =>
            {
                client.RegisterPrefab(gameObject);
            });
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void RegisterPrefabGuidExceptionTest()
        {
            var guid = Guid.NewGuid();
            var gameObject = new GameObject();

            Assert.Throws<InvalidOperationException>(() =>
            {
                client.RegisterPrefab(gameObject, guid);
            });
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnSpawnAssetSceneIDFailureExceptionTest()
        {
            var msg = new SpawnMessage();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                client.OnSpawn(msg);
            });

            Assert.That(ex.Message, Is.EqualTo("OnObjSpawn netId: " + msg.netId + " has invalid asset Id"));
        }

        [Test]
        public void UnregisterPrefabExceptionTest()
        {
            var gameObject = new GameObject();
            Assert.Throws<InvalidOperationException>(() =>
            {
                client.UnregisterPrefab(gameObject);
            });
            Object.DestroyImmediate(gameObject);
        }

        [UnityTest]
        public IEnumerator GetPrefabTest()
        {
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));

            client.RegisterPrefab(prefabObject, guid);

            yield return null;

            client.GetPrefab(guid, out GameObject result);

            Assert.That(result, Is.SameAs(prefabObject));

            Object.Destroy(prefabObject);
        }

        [UnityTest]
        public IEnumerator AuthenticatorTest()
        {
            Assert.That(client.authenticator == null);
            TestClientAuthenticator comp = networkManagerGo.AddComponent<TestClientAuthenticator>();

            yield return null;

            Assert.That(client.authenticator != null);
            client.ConnectHost(server);

            Assert.That(comp.called, Is.EqualTo(1));
        }
    }
}
