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

        /*[Test]
        public void ClientToServerTest()
        {
            Assert.That(connectionToServer.address, Is.EqualTo("localhost"));

            MyMessage myMessage = new MyMessage()
            {
                id = 3,
                name = "hello"
            };

            bool invoked = false;

            void handler(NetworkConnection conn, NetworkReader reader, int channelId)
            {
                MyMessage received = msg.ReadMessage<MyMessage>();
                Assert.That(received.id, Is.EqualTo(3));
                Assert.That(received.name, Is.EqualTo("hello"));
                invoked = true;
            }

            Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();
            handlers.Add(MessagePacker.GetId<MyMessage>(), handler);

            connectionToServer.SetHandlers(handlers);
            connectionToClient.Send(myMessage);

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }*/
    }
}
