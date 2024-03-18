using NUnit.Framework;

namespace Mirror.Tests.NetworkManagers
{
    class NetworkManagerOnServerDisconnect : NetworkManager
    {
        public int called;
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            ++called;
        }
}

    [TestFixture]
    public class NetworkManagerStopHostOnServerDisconnectTest : MirrorEditModeTest
    {
        NetworkManagerOnServerDisconnect manager;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            manager = transport.gameObject.AddComponent<NetworkManagerOnServerDisconnect>();
            manager.transport = transport;
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/1515
        [Test]
        public void StopHostCallsOnServerDisconnectForHostClient()
        {
            // OnServerDisconnect is always called when a client disconnects.
            // it should also be called for the host client when we stop the host
            Assert.That(manager.called, Is.EqualTo(0));
            manager.StartHost();
            manager.StopHost();
            Assert.That(manager.called, Is.EqualTo(1));
        }

        [Test]
        public void StopClientCallsOnServerDisconnectForHostClient()
        {
            // OnServerDisconnect is always called when a client disconnects.
            // it should also be called for the host client when we stop the host
            Assert.That(manager.called, Is.EqualTo(0));
            manager.StartHost();
            manager.StopClient();
            Assert.That(manager.called, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.localConnection, Is.Null);
            Assert.That(NetworkClient.connection, Is.Null);
        }
    }
}
