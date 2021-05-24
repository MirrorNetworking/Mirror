using System.Collections.Generic;
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
            void Handler(NetworkConnection conn, NetworkReader reader, int channelId)
            {
                invoked = true;
            }

            // set up handler on the server connection
            Dictionary<ushort, NetworkMessageDelegate> handlers = new Dictionary<ushort, NetworkMessageDelegate>();
            handlers.Add(MessagePacking.GetId<TestMessage>(), Handler);
            connectionToClient.SetHandlers(handlers);

            connectionToServer.Send(new TestMessage());
            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void ServerToClient()
        {
            Assert.That(connectionToServer.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(NetworkConnection conn, NetworkReader reader, int channelId)
            {
                invoked = true;
            }

            // set up handler on the client connection
            Dictionary<ushort, NetworkMessageDelegate> handlers = new Dictionary<ushort, NetworkMessageDelegate>();
            handlers.Add(MessagePacking.GetId<TestMessage>(), Handler);
            connectionToServer.SetHandlers(handlers);

            connectionToClient.Send(new TestMessage());
            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }
    }
}
