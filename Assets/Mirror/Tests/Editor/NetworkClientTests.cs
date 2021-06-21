using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkClientTests : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // we need a server to connect to
            NetworkServer.Listen(10);
        }

        [Test]
        public void ServerIp()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.serverIp, Is.EqualTo("localhost"));
        }

        [Test]
        public void IsConnected()
        {
            Assert.That(NetworkClient.isConnected, Is.False);
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
        }

        [Test]
        public void ConnectUri()
        {
            NetworkClient.Connect(new Uri("memory://localhost"));
            // update transport so connect event is processed
            UpdateTransport();
            Assert.That(NetworkClient.isConnected, Is.True);
        }

        [Test]
        public void DisconnectInHostMode()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
            Assert.That(NetworkServer.localConnection, !Is.Null);

            NetworkClient.Disconnect();
            Assert.That(NetworkClient.isConnected, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        [Test, Ignore("NetworkServerTest.SendClientToServerMessage does it already")]
        public void Send() {}

        [Test]
        public void ShutdownCleanup()
        {
            // add some test event hooks to make sure they are cleaned up.
            // there used to be a bug where they wouldn't be cleaned up.
            NetworkClient.OnConnectedEvent = () => {};
            NetworkClient.OnDisconnectedEvent = () => {};

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.OnConnectedEvent, Is.Null);
            Assert.That(NetworkClient.OnDisconnectedEvent, Is.Null);
        }
    }
}
