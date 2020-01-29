using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkServerTests : NetworkBehaviour
    {
        NetworkServer testServer;

        [SetUp]
        public void SetupNetworkServer()
        {
            var gameObject = new GameObject();
            testServer = gameObject.AddComponent<NetworkServer>();
            gameObject.AddComponent<NetworkClient>();
            Transport transport = gameObject.AddComponent<TelepathyTransport>();

            Transport.activeTransport = transport;
            testServer.Listen(1);
        }

        [Test]
        public void InitializeTest()
        {
            Assert.That(testServer.localClient != null);
            Assert.That(testServer.connections.Count == 0);
            Assert.That(testServer.active);
        }

        [Test]
        public void AddConnectionsTest()
        {
            for (int i = 0; i < 10; i++)
            {
                var connToClient = new NetworkConnectionToClient(i);
                testServer.AddConnection(connToClient);
            }
            Assert.That(testServer.active);
            Assert.That(testServer.connections.Count == 10);
        }

        [Test]
        public void ClearConnectionsTest()
        {
            for (int i = 0; i < 10; i++)
                testServer.RemoveConnection(i);

            Assert.That(testServer.connections.Count == 0);
        }

        [Test]
        public void SpawnTest()
        {
            var gameObject = new GameObject();
            gameObject.AddComponent<NetworkIdentity>();
            testServer.Spawn(gameObject);

            Assert.That(gameObject.GetComponent<NetworkIdentity>().server == testServer);
        }

        [Test]
        public void SpawnWithAuthorityTest()
        {
            var player = new GameObject();
            player.AddComponent<NetworkIdentity>();
            var connToClient = new NetworkConnectionToClient(0);
            player.GetComponent<NetworkIdentity>().connectionToClient = connToClient;

            var gameObject = new GameObject();
            gameObject.AddComponent<NetworkIdentity>();

            testServer.Spawn(gameObject, player);

            NetworkIdentity networkIdentity = gameObject.GetComponent<NetworkIdentity>();
            Assert.That(networkIdentity.server == testServer);
            Assert.That(networkIdentity.connectionToClient == connToClient);
        }

        [Test]
        public void ShutdownTest()
        {
            testServer.Shutdown();
        }

        [OneTimeTearDown]
        public void ShutdownNetworkServer()
        {
            testServer.Shutdown();
            Assert.That(testServer.active == false);
        }
    }
}
