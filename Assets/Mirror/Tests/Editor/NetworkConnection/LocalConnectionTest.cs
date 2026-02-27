using NUnit.Framework;

namespace Mirror.Tests.NetworkConnections
{
    public class LocalConnectionTest : MirrorTest
    {
        struct TestMessage : NetworkMessage {}

        LocalConnectionToClient connectionToClient;
        LocalConnectionToServer connectionToServer;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            Utils.CreateLocalConnections(out connectionToClient, out connectionToServer);

            // set up server/client connections so message handling works
            NetworkClient.connection = connectionToServer;
            NetworkServer.connections[connectionToClient.connectionId] = connectionToClient;
        }

        [TearDown]
        public override void TearDown()
        {
            connectionToServer.Disconnect();
            base.TearDown();
        }

        // ---- properties -------------------------------------------------------------

        [Test]
        public void LocalConnectionToClient_AddressIsLocalhost()
        {
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));
        }

        [Test]
        public void LocalConnectionToClient_ConnectionId_IsLocalConnectionId()
        {
            Assert.That(connectionToClient.connectionId, Is.EqualTo(NetworkConnection.LocalConnectionId));
        }

        // ---- IsAlive ----------------------------------------------------------------

        [Test]
        public void LocalConnectionToClient_IsAlive_AlwaysTrue()
        {
            // local connections never time out regardless of the timeout value
            Assert.That(connectionToClient.IsAlive(0), Is.True);
        }

        [Test]
        public void LocalConnectionToServer_IsAlive_AlwaysTrue()
        {
            // local connections never time out regardless of the timeout value
            Assert.That(connectionToServer.IsAlive(0), Is.True);
        }

        // ---- message delivery -------------------------------------------------------

        [Test]
        public void ClientToServerTest()
        {
            // address check kept here as a sanity guard for local connection setup
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(NetworkConnectionToClient conn, TestMessage message) => invoked = true;

            NetworkServer.RegisterHandler<TestMessage>(Handler, false);

            // client sends → data is enqueued in connectionToClient.queue
            connectionToServer.Send(new TestMessage());

            // connectionToClient.Update() processes the client→server queue and
            // delivers to NetworkServer. connectionToServer.Update() is NOT
            // needed for this direction (it processes the server→client queue).
            connectionToClient.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void ServerToClient()
        {
            bool invoked = false;
            void Handler(TestMessage message) => invoked = true;

            NetworkClient.RegisterHandler<TestMessage>(Handler, false);

            // server sends → data is enqueued in connectionToServer.queue
            connectionToClient.Send(new TestMessage());

            // connectionToServer.Update() processes the server→client queue and
            // delivers to NetworkClient
            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void MultipleMessages_ClientToServer_AllDelivered()
        {
            int invokeCount = 0;
            void Handler(NetworkConnectionToClient conn, TestMessage message) => invokeCount++;

            NetworkServer.RegisterHandler<TestMessage>(Handler, false);

            connectionToServer.Send(new TestMessage());
            connectionToServer.Send(new TestMessage());
            connectionToServer.Send(new TestMessage());

            // each message is independently dequeued and batched in Update(),
            // producing 3 separate OnTransportData calls → handler invoked 3×
            connectionToClient.Update();

            Assert.That(invokeCount, Is.EqualTo(3));
        }

        [Test]
        public void MultipleMessages_ServerToClient_AllDelivered()
        {
            int invokeCount = 0;
            void Handler(TestMessage message) => invokeCount++;

            NetworkClient.RegisterHandler<TestMessage>(Handler, false);

            connectionToClient.Send(new TestMessage());
            connectionToClient.Send(new TestMessage());
            connectionToClient.Send(new TestMessage());

            connectionToServer.Update();

            Assert.That(invokeCount, Is.EqualTo(3));
        }
    }
}
