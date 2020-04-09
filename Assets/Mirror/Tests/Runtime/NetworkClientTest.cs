using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    public class TestClientAuthenticator : NetworkAuthenticator
    {
        public int called;

        public override void OnClientAuthenticate(NetworkConnection conn)
        {
            ++called;
        }
    }

    [TestFixture]
    public class NetworkClientTest
    {
        NetworkServer server;
        GameObject serverGO;
        NetworkClient client;

        GameObject gameObject;
        NetworkIdentity identity;

        [UnitySetUp]
        public IEnumerator SetUp() => RunAsync(async () =>
        {
            serverGO = new GameObject();
            serverGO.AddComponent<MockTransport>();

            server = serverGO.AddComponent<NetworkServer>();
            client = serverGO.AddComponent<NetworkClient>();

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();

            await server.ListenAsync();
        });

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);

            // reset all state
            server.Disconnect();
            Object.DestroyImmediate(serverGO);
        }

        [Test]
        public void IsConnectedTest()
        {
            Assert.That(!client.IsConnected);

            client.ConnectHost(server);

            Assert.That(client.IsConnected);
        }

        [Test]
        public void ConnectionTest()
        {
            Assert.That(client.Connection == null);

            client.ConnectHost(server);

            Assert.That(client.Connection != null);
        }

        [UnityTest]
        public IEnumerable LocalPlayerTest()
        {
            Assert.That(client.LocalPlayer == null);

            PlayerSpawner spawner = serverGO.AddComponent<PlayerSpawner>();

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
            Assert.That(!client.ready);

            client.ConnectHost(server);

            Assert.That(client.Ready(client.Connection));
            Assert.That(client.ready);
            Assert.That(client.Connection.isReady);
        }

        [UnityTest]
        public IEnumerable RemovePlayerTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = client.RemovePlayer();
            });

            PlayerSpawner spawner = serverGO.AddComponent<PlayerSpawner>();

            spawner.server = server;
            spawner.client = client;
            spawner.playerPrefab = identity;
            spawner.Start();

            client.ConnectHost(server);

            yield return null;

            Assert.That(client.Connection.Identity != null);
            Assert.That(client.LocalPlayer != null);

            Assert.That(client.RemovePlayer());
            Assert.That(identity == null);
            Assert.That(client.Connection.Identity == null);
            Assert.That(client.LocalPlayer == null);
        }

        [Test]
        public void RegisterPrefabTest()
        {
            Guid guid = Guid.NewGuid();
            client.RegisterPrefab(gameObject, guid);

            Assert.That(gameObject.GetComponent<NetworkIdentity>().AssetId == guid);
        }

        [UnityTest]
        public IEnumerable GetPrefabTest()
        {
            Guid guid = Guid.NewGuid();
            client.RegisterPrefab(gameObject, guid);

            yield return null;

            client.GetPrefab(guid, out GameObject result);

            Assert.That(result != null);
            Assert.That(result.GetComponent<NetworkIdentity>().AssetId == guid);
        }

        [UnityTest]
        public IEnumerable AuthenticatorTest()
        {
            Assert.That(client.authenticator == null);
            TestClientAuthenticator comp = serverGO.AddComponent<TestClientAuthenticator>();

            yield return null;

            Assert.That(client.authenticator != null);
            client.ConnectHost(server);

            Assert.That(comp.called, Is.EqualTo(1));
        }
    }
}
