using NUnit.Framework;

namespace Mirror.Tests.StructMessages
{
    public struct SomeStructMessage : NetworkMessage
    {
        public int someValue;

        // weaver populates (de)serialize automatically
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }

    [TestFixture]
    public class StructMessagesTests
    {
        [Test]
        public void SerializeAreAddedWhenEmptyInStruct()
        {
            NetworkWriter writer = new NetworkWriter();

            const int someValue = 3;
            SomeStructMessage send = new SomeStructMessage
            {
                someValue = someValue,
            };
            send.Serialize(writer);

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            SomeStructMessage received = new SomeStructMessage();
            received.Deserialize(reader);

            Assert.AreEqual(someValue, received.someValue);

            int writeLength = writer.Length;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }
    }
}
