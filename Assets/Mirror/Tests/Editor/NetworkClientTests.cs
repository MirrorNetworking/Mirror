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

        // test to guarantee Disconnect() eventually calls OnClientDisconnected.
        // prevents https://github.com/vis2k/Mirror/issues/2818 forever.
        // previously there was a bug where:
        // - Disconnect() sets state = Disconnected
        // - Transport processes it
        // - OnTransportDisconnected() early returns because
        //   state == Disconnected already, so it wouldn't call the event.
        [Test]
        public void DisconnectCallsOnClientDisconnected_Remote()
        {
            // setup hook
            bool called = false;
            NetworkClient.OnDisconnectedEvent = () => called = true;

            // connect
            ConnectClientBlocking(out _);

            // disconnect & process everything
            NetworkClient.Disconnect();
            UpdateTransport();

            // was it called?
            Assert.That(called, Is.True);
        }

        // same as above, but for host mode
        // prevents https://github.com/vis2k/Mirror/issues/2818 forever.
        [Test]
        public void DisconnectCallsOnClientDisconnected_HostMode()
        {
            // setup hook
            bool called = false;
            NetworkClient.OnDisconnectedEvent = () => called = true;

            // connect host
            NetworkClient.ConnectHost();

            // disconnect & process everything
            NetworkClient.Disconnect();
            UpdateTransport();

            // was it called?
            Assert.That(called, Is.True);
        }

        [Test]
        public void ShutdownCleanup()
        {
            // add some test event hooks to make sure they are cleaned up.
            // there used to be a bug where they wouldn't be cleaned up.
            NetworkClient.OnConnectedEvent = () => {};
            NetworkClient.OnDisconnectedEvent = () => {};

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.handlers.Count, Is.EqualTo(0));
            Assert.That(NetworkClient.spawned.Count, Is.EqualTo(0));
            Assert.That(NetworkClient.spawnableObjects.Count, Is.EqualTo(0));

            Assert.That(NetworkClient.connectState, Is.EqualTo(ConnectState.None));

            Assert.That(NetworkClient.connection, Is.Null);
            Assert.That(NetworkClient.localPlayer, Is.Null);

            Assert.That(NetworkClient.ready, Is.False);
            Assert.That(NetworkClient.isSpawnFinished, Is.False);
            Assert.That(NetworkClient.isLoadingScene, Is.False);

            Assert.That(NetworkClient.OnConnectedEvent, Is.Null);
            Assert.That(NetworkClient.OnDisconnectedEvent, Is.Null);
            Assert.That(NetworkClient.OnErrorEvent, Is.Null);
        }
    }
}
