using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class BasicAuthenticatorTest
    {
        public class AuthRequestMessage : MessageBase
        {
            public string authUsername;
            public string authPassword;
        }

        public class AuthResponseMessage : MessageBase
        {
            public byte code;
            public string message;
        }

        [Test]
        public void AuthRequestMessageTest()
        {
            // try setting value with constructor
            AuthRequestMessage message = new AuthRequestMessage
            {
                authUsername = "abc",
                authPassword = "123"
            };
            Assert.That(message.authUsername, Is.EqualTo("abc"));
            Assert.That(message.authPassword, Is.EqualTo("123"));

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // try deserialize
            AuthRequestMessage fresh = new AuthRequestMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.authUsername, Is.EqualTo("abc"));
            Assert.That(fresh.authPassword, Is.EqualTo("123"));
        }

        [Test]
        public void AuthResponseMessageTest()
        {
            // try setting value with constructor
            AuthResponseMessage message = new AuthResponseMessage
            {
                code = 123,
                message = "abc"
            };
            Assert.That(message.code, Is.EqualTo(123));
            Assert.That(message.message, Is.EqualTo("abc"));

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // try deserialize
            AuthResponseMessage fresh = new AuthResponseMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.code, Is.EqualTo(123));
            Assert.That(fresh.message, Is.EqualTo("abc"));
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

        public delegate void AuthRequestMessageDelegate(AuthRequestMessage netMsg);
        public delegate void AuthResponseMessageDelegate(AuthResponseMessage netMsg);

        [Test]
        public void OnClientAuthenticateTest()
        {
            AuthRequestMessage authRequestMessage = new AuthRequestMessage
            {
                authUsername = "abc",
                authPassword = "123"
            };

            bool invoked = false;

            void handler(AuthRequestMessage msg)
            {
                Assert.That(msg.authUsername, Is.EqualTo("abc"));
                Assert.That(msg.authPassword, Is.EqualTo("123"));
                invoked = true;
            }

            //Dictionary<int, AuthRequestMessageDelegate> handlers = new Dictionary<int, AuthRequestMessageDelegate>();
            //handlers.Add(MessagePacker.GetId<AuthRequestMessage>(), handler);
            //connectionToClient.SetHandlers(handlers);

            // This is wrong...what should be done here?
            NetworkServer.RegisterHandler<AuthRequestMessage>(handler);
            connectionToClient.Send(authRequestMessage);

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void OnAuthRequestMessage()
        {
            AuthResponseMessage authResponseMessage = new AuthResponseMessage
            {
                code = 123,
                message = "abc"
            };

            bool invoked = false;

            void handler(AuthResponseMessage msg)
            {
                Assert.That(msg.code, Is.EqualTo(123));
                Assert.That(msg.message, Is.EqualTo("abc"));
                invoked = true;
            }

            //Dictionary<int, AuthResponseMessageDelegate> handlers = new Dictionary<int, AuthResponseMessageDelegate>();
            //handlers.Add(MessagePacker.GetId<AuthResponseMessage>(), handler);
            //connectionToClient.SetHandlers(handlers);

            // This is wrong...what should be done here?
            NetworkClient.RegisterHandler<AuthResponseMessage>(handler);
            connectionToServer.Send(authResponseMessage);

            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }
    }
}
