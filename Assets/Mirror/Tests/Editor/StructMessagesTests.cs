using NUnit.Framework;

namespace Mirror.Tests.StructMessages
{
    public struct SomeStructMessage : IMessageBase
    {
        public int someValue;

        // Mirror will automatically implement message that are empty
        public void Serialize(NetworkWriter writer) { }
        public void Deserialize(NetworkReader reader) { }
    }

    [TestFixture]
    public class StructMessagesTests
    {
        [Test]
        public void SerializeAreAddedWhenEmptyInStruct()
        {
            NetworkWriter writer = new NetworkWriter();

            const int someValue = 3;
            writer.WriteMessage(new SomeStructMessage
            {
                someValue = someValue,
            });

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
