using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class BasicAuthenticatorTest
    {
        public class AuthRequestMessage
        {
            public string authUsername;
            public string authPassword;
        }

        public class AuthResponseMessage
        {
            public byte code;
            public string message;
        }

        [Test]
        public void AuthRequestMessageTest()
        {
            // try setting value with constructor
            var message = new AuthRequestMessage
            {
                authUsername = "abc",
                authPassword = "123"
            };
            Assert.That(message.authUsername, Is.EqualTo("abc"));
            Assert.That(message.authPassword, Is.EqualTo("123"));

            // serialize
            var writer = new NetworkWriter();
            writer.WriteMessage(message);
            byte[] writerData = writer.ToArray();

            // try deserialize
            NetworkReader reader = new NetworkReader(writerData);
            var fresh = reader.ReadMessage<AuthRequestMessage>();
            Assert.That(fresh.authUsername, Is.EqualTo("abc"));
            Assert.That(fresh.authPassword, Is.EqualTo("123"));
        }

        [Test]
        public void AuthResponseMessageTest()
        {
            // try setting value with constructor
            var message = new AuthResponseMessage
            {
                code = 123,
                message = "abc"
            };
            Assert.That(message.code, Is.EqualTo(123));
            Assert.That(message.message, Is.EqualTo("abc"));

            // serialize
            var writer = new NetworkWriter();
            writer.WriteMessage(message);
            byte[] writerData = writer.ToArray();

            // try deserialize
            var reader = new NetworkReader(writerData);
            var fresh = reader.ReadMessage<AuthResponseMessage>();
            Assert.That(fresh.code, Is.EqualTo(123));
            Assert.That(fresh.message, Is.EqualTo("abc"));
        }
    }
}
