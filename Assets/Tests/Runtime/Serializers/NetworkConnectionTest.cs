using System.IO;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkConnectionTest 
    {
        [Test]
        public void NoHandler()
        {
            var connection = new NetworkConnection(Substitute.For<IConnection>());

            int messageId = MessagePacker.GetId<SceneMessage>();
            var reader = new NetworkReader(new byte[] { 1, 2, 3, 4 });
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            {
                connection.InvokeHandler(messageId, reader, 0);
            });

            Assert.That(exception.Message, Does.StartWith("Unexpected message Mirror.SceneMessage received"));
        }

        [Test]
        public void UnknownMessage()
        {
            var connection = new NetworkConnection(Substitute.For<IConnection>());

            _ = MessagePacker.GetId<SceneMessage>();
            var reader = new NetworkReader(new byte[] { 1, 2, 3, 4 });
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            {
                // some random id with no message
                connection.InvokeHandler(1234, reader, 0);
            });

            Assert.That(exception.Message, Does.StartWith("Unexpected message ID 1234 received"));
        }
    }
}