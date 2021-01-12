using NUnit.Framework;

namespace Mirror.Tests.StructMessages
{
    public struct SomeStructMessage
    {
        public int someValue;
    }

    [TestFixture]
    public class StructMessagesTests
    {
        [Test]
        public void SerializeAreAddedWhenEmptyInStruct()
        {
            var writer = new NetworkWriter();

            const int someValue = 3;
            writer.Write(new SomeStructMessage
            {
                someValue = someValue,
            });

            byte[] arr = writer.ToArray();

            var reader = new NetworkReader(arr);
            SomeStructMessage received = reader.Read<SomeStructMessage>();

            Assert.AreEqual(someValue, received.someValue);

            int writeLength = writer.Length;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }
    }
}
