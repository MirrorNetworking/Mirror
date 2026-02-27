using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // we need a server to connect to
            Mirror.NetworkServer.Listen(10);
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
        public void ConnectString()
        {
            NetworkClient.Connect("127.0.0.1");
            UpdateTransport();
            Assert.That(NetworkClient.isConnected, Is.True);
        }

        [Test]
        public void DisconnectInHostMode()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
            Assert.That(Mirror.NetworkServer.localConnection, !Is.Null);

            NetworkClient.Disconnect();
            Assert.That(NetworkClient.isConnected, Is.False);
            Assert.That(Mirror.NetworkServer.localConnection, Is.Null);
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
        public void OwnedObjects()
        {
            // SetUp already called Listen(10) — no second Listen needed here.
            ConnectHostClientBlockingAuthenticatedAndReady();

            // spawn main player. should be added to .owned.
            CreateNetworkedAndSpawnPlayer(out _, out NetworkIdentity player, Mirror.NetworkServer.localConnection);
            Assert.That(NetworkClient.connection.owned.Count, Is.EqualTo(1));
            Assert.That(NetworkClient.connection.owned.Contains(NetworkClient.localPlayer));

            // spawn an object which is not owned. shouldn't add anything.
            CreateNetworkedAndSpawn(out _, out NetworkIdentity other);
            Assert.That(NetworkClient.connection.owned.Count, Is.EqualTo(1));

            // spawn an owned object. should add to client's .owned.
            CreateNetworkedAndSpawn(out _, out NetworkIdentity pet, Mirror.NetworkServer.localConnection);
            Assert.That(NetworkClient.connection.owned.Count, Is.EqualTo(2));

            // despawn should remove from .owned
            Mirror.NetworkServer.Destroy(pet.gameObject);
            ProcessMessages();
            Assert.That(NetworkClient.connection.owned.Count, Is.EqualTo(1));
            Assert.That(NetworkClient.connection.owned.Contains(NetworkClient.localPlayer));
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

            Assert.That(NetworkClient.exceptionsDisconnect, Is.True);

            Assert.That(NetworkClient.OnConnectedEvent, Is.Null);
            Assert.That(NetworkClient.OnDisconnectedEvent, Is.Null);
            Assert.That(NetworkClient.OnErrorEvent, Is.Null);
            Assert.That(NetworkClient.OnTransportExceptionEvent, Is.Null);
        }

        // test to prevent a bug where host mode scene transitions would
        // still receive a previous scene's data.
        [Test]
        public void ConnectHostResetsUnbatcher()
        {
            // SetUp already called Listen(10) — no second Listen needed here.
            ConnectHostClientBlockingAuthenticatedAndReady();

            // add some data to unbatcher, disconnect.
            // need at least batcher.HeaderSize for it to be counted as batch
            NetworkClient.isLoadingScene = true;
            byte[] data = new byte[]{1,2,3,4,5,6,7,8};
            NetworkClient.OnTransportData(new ArraySegment<byte>(data), Channels.Reliable);
            NetworkClient.Disconnect();
            Mirror.NetworkServer.DisconnectAll();
            Assert.That(NetworkClient.unbatcher.BatchesCount, Is.EqualTo(1));

            // batches should be cleared when connecting again.
            // otherwise we would get invalid messages from last time.
            ConnectHostClientBlockingAuthenticatedAndReady();
            Assert.That(NetworkClient.unbatcher.BatchesCount, Is.EqualTo(0));
        }

        // ?? connectState / derived property coverage ?????????????????????????

        [Test]
        public void Active_TrueWhenConnecting()
        {
            NetworkClient.connectState = ConnectState.Connecting;
            Assert.That(NetworkClient.active, Is.True);
        }

        [Test]
        public void Active_TrueWhenConnected()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.active, Is.True);
        }

        [Test]
        public void Active_FalseWhenNone()
        {
            // default state after TearDown/before any connection
            Assert.That(NetworkClient.active, Is.False);
        }

        [Test]
        public void Active_FalseWhenDisconnecting()
        {
            NetworkClient.connectState = ConnectState.Disconnecting;
            Assert.That(NetworkClient.active, Is.False);
        }

        [Test]
        public void IsConnecting_TrueOnlyWhenConnectingState()
        {
            NetworkClient.connectState = ConnectState.Connecting;
            Assert.That(NetworkClient.isConnecting, Is.True);
            Assert.That(NetworkClient.isConnected, Is.False);
        }

        [Test]
        public void ActiveHost_TrueInHostMode()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.activeHost, Is.True);
        }

        [Test]
        public void ActiveHost_FalseInRemoteMode()
        {
            NetworkClient.Connect("127.0.0.1");
            UpdateTransport();
            Assert.That(NetworkClient.activeHost, Is.False);
        }
    }
}
