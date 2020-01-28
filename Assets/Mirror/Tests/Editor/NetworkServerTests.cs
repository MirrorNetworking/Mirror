using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkServerTests : NetworkBehaviour
    {
        NetworkServer testServer;

        [Test, Order(1)]
        public void InitializeTest()
        {
            var gameObject = new GameObject();
            testServer = gameObject.AddComponent<NetworkServer>();
            Transport transport = gameObject.AddComponent<TelepathyTransport>();

            Transport.activeTransport = transport;
            testServer.Listen(1);

            Assert.That(server.localClient != null);
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

            Assert.That(testServer.connections.Count == 10);
        }

        [Test]
        public void RemoveConnectionsTest()
        {
            for (int i = 0; i < 10; i++)
                testServer.RemoveConnection(i);

            Assert.That(testServer.connections.Count == 0);
        }

        [Test]
        public void ShutdownTest()
        {
            testServer.Shutdown();

            Assert.That(testServer.active == false);
        }
    }
}
