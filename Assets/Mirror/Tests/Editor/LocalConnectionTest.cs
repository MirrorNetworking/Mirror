using NUnit.Framework;

namespace Mirror.Tests
{
    public class LocalConnectionTest
    {
        struct TestMessage : NetworkMessage {}

        LocalConnectionToClient connectionToClient;
        LocalConnectionToServer connectionToServer;

        [SetUp]
        public void SetUp()
        {
            connectionToServer = new LocalConnectionToServer();
            connectionToClient = new LocalConnectionToClient();

            connectionToClient.connectionToServer = connectionToServer;
            connectionToServer.connectionToClient = connectionToClient;
        }

        [TearDown]
        public void TearDown()
        {
            connectionToServer.Disconnect();
        }

        [Test]
        public void ClientToServerTest()
        {
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(NetworkConnection conn, TestMessage message)
            {
                invoked = true;
            }

            // set up handler on the server connection
            NetworkServer.RegisterHandler<TestMessage>(Handler, false);

            connectionToServer.Send(new TestMessage());
            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void ServerToClient()
        {
            Assert.That(connectionToServer.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(TestMessage message)
            {
                invoked = true;
            }

            // set up handler on the client connection
            NetworkClient.RegisterHandler<TestMessage>(Handler, false);

            connectionToClient.Send(new TestMessage());
            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }
    }
}
