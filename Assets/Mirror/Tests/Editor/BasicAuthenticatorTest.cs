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
    }
}
