using NUnit.Framework;

namespace Mirror.Tests.StructMessages
{
    public struct SomeStructMessage : NetworkMessage
    {
        public int someValue;
    }

    [TestFixture]
    public class StructMessagesTests
    {
        [Test]
        public void SerializeAreAddedWhenEmptyInStruct()
        {
            NetworkWriter writer = new NetworkWriter();

            const int someValue = 3;
            writer.Write(new SomeStructMessage
            {
                someValue = someValue,
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            SomeStructMessage received = reader.Read<SomeStructMessage>();

            Assert.AreEqual(someValue, received.someValue);

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }
    }
}
