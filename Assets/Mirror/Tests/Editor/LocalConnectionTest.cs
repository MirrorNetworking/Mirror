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
        public void SetUpConnections()
        {
            connectionToServer = new LocalConnectionToServer();
            connectionToClient = new LocalConnectionToClient();

            connectionToClient.connectionToServer = connectionToServer;
            connectionToServer.connectionToClient = connectionToClient;
        }

        [TearDown]
        public void Disconnect()
        {
            connectionToServer.Disconnect();
        }

        [Test]
        public void ServerToClient()
        {
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(NetworkConnection conn, NetworkReader reader, int channelId)
            {
                invoked = true;
            }

            Dictionary<ushort, NetworkMessageDelegate> handlers = new Dictionary<ushort, NetworkMessageDelegate>();
            handlers.Add(MessagePacking.GetId<TestMessage>(), Handler);

            connectionToClient.SetHandlers(handlers);
            connectionToServer.Send(new TestMessage());

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void ClientToServerTest()
        {
            Assert.That(connectionToServer.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(NetworkConnection conn, NetworkReader reader, int channelId)
            {
                invoked = true;
            }

            Dictionary<ushort, NetworkMessageDelegate> handlers = new Dictionary<ushort, NetworkMessageDelegate>();
            handlers.Add(MessagePacking.GetId<TestMessage>(), Handler);

            connectionToServer.SetHandlers(handlers);
            connectionToClient.Send(new TestMessage());

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }
    }
}
