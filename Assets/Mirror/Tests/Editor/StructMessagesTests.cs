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
            NetworkWriter writer = new NetworkWriter(1024);

            const int someValue = 3;
            writer.Write(new SomeStructMessage
            {
                someValue = someValue,
            });

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            SomeStructMessage received = reader.Read<SomeStructMessage>();

            Assert.AreEqual(someValue, received.someValue);

            Assert.That(writer.Position == reader.Position, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writer.Position}\n    readLength={reader.Position}");
        }
    }
}
