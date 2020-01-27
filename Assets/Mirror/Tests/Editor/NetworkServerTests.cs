using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkServerTests : NetworkBehaviour
    {
        NetworkServer testServer;

        [Test]
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
        public void ShutdownTest()
        {
            testServer.Shutdown();

            Assert.That(testServer.active == false);
        }
    }
}
