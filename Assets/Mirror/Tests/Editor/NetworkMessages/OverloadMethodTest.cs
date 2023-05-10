using NUnit.Framework;

namespace Mirror.Tests.NetworkMessagesTests
{
    struct NoArgMethodMessage : NetworkMessage
    {
        public int someValue;

        // Weaver should ignore these methods because they have no args
        public void Serialize() { /* method with no arg */ }
        public void Deserialize() { /* method with no arg */ }
    }

    struct TwoArgMethodMessage : NetworkMessage
    {
        public int someValue;

        // Weaver should ignore these methods because they have two args
        public void Serialize(NetworkWriter writer, int AnotherValue) {}
        public void Deserialize(NetworkReader reader, int AnotherValue) {}
    }

    public class OverloadMethodTest
    {
        [Test]
        public void MethodsWithNoArgs()
        {
            const int value = 10;
            NoArgMethodMessage intMessage = new NoArgMethodMessage {someValue = value};

            byte[] data = NetworkMessagesTest.PackToByteArray(intMessage);
            NoArgMethodMessage unpacked = NetworkMessagesTest.UnpackFromByteArray<NoArgMethodMessage>(data);
            Assert.That(unpacked.someValue, Is.EqualTo(value));
        }

        [Test]
        public void MethodsWithTwoArgs()
        {
            const int value = 10;
            TwoArgMethodMessage intMessage = new TwoArgMethodMessage {someValue = value};

            byte[] data = NetworkMessagesTest.PackToByteArray(intMessage);
            TwoArgMethodMessage unpacked = NetworkMessagesTest.UnpackFromByteArray<TwoArgMethodMessage>(data);
            Assert.That(unpacked.someValue, Is.EqualTo(value));
        }
    }
}
