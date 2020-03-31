using System;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class LocalConnectionTest
    {

        /*class MyMessage : MessageBase
        {
            public int id;
            public string name;
        }*/

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

        /*[Test]
        public void ServerToClientTest()
        {
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));

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

            connectionToClient.SetHandlers(handlers);
            connectionToServer.Send(myMessage);

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }*/

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

        [Test]
        public void ClientToServerFailTest()
        {
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            bool result = connectionToServer.Send(new ArraySegment<byte>(new byte[0]));
            LogAssert.ignoreFailingMessages = false;

            Assert.That(result, Is.False);
        }
    }
}
