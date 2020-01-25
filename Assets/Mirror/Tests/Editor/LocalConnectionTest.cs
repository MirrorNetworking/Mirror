using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class LocalConnectionTest
    {

        class MyMessage : MessageBase
        {
            public int id;
            public string name;
        }

        ULocalConnectionToClient connectionToClient;
        ULocalConnectionToServer connectionToServer;

        [SetUp]
        public void SetUpConnections()
        {
            connectionToServer = new ULocalConnectionToServer();
            connectionToClient = new ULocalConnectionToClient();

            connectionToClient.connectionToServer = connectionToServer;
            connectionToServer.connectionToClient = connectionToClient;
        }

        [TearDown]
        public void Disconnect()
        {
            connectionToServer.Disconnect();
        }

        [Test]
        public void ServerToClientTest()
        {
            MyMessage myMessage = new MyMessage()
            {
                id = 3,
                name = "hello"
            };

            bool invoked = false;

            void handler(NetworkMessage msg)
            {
                MyMessage received = msg.ReadMessage<MyMessage>();
                Assert.That(received.id, Is.EqualTo(3));
                Assert.That(received.name, Is.EqualTo("hello"));
                invoked = true;
            }

            Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();
            handlers.Add(MessagePacker.GetId<MyMessage>(), handler);

            connectionToClient.SetHandlers(handlers);
            connectionToServer.Send(myMessage);

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void ClientToServerTest()
        {
            MyMessage myMessage = new MyMessage()
            {
                id = 3,
                name = "hello"
            };

            bool invoked = false;

            void handler(NetworkMessage msg)
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
        }

    }
}
