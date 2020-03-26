using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    class NetworkManagerOnClientDisconnect : NetworkManager
    {
        public int called;
        public override void OnClientDisconnect(NetworkConnection conn) { ++called; }
    }

    [TestFixture]
    public class NetworkManagerStopHostOnClientDisconnectedTest
    {
        GameObject gameObject;
        NetworkManagerOnClientDisconnect manager;
        MemoryTransport transport;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            // add transport first, so networkmanager doesn't add telepathy
            Transport.activeTransport = transport = gameObject.AddComponent<MemoryTransport>();
            manager = gameObject.AddComponent<NetworkManagerOnClientDisconnect>();
            // shouldn't automatically create a player. we don't have one.
            manager.autoCreatePlayer = false;
        }

        [TearDown]
        public void TearDown()
        {
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(gameObject);
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/1515
        [Test]
        public void StopHostCallsOnClientDisconnectForHostClient()
        {
            // OnServerDisconnect is always called when a client disconnects.
            // it should also be called for the host client when we stop the host
            Assert.That(manager.called, Is.EqualTo(0));
            manager.StartServer();
            manager.StartClient();
            transport.LateUpdate();
            Assert.That(NetworkClient.isConnected, Is.True);
            manager.StopClient();
            Assert.That(manager.called, Is.EqualTo(1));
        }
    }
}
