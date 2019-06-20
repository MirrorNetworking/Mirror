using NUnit.Framework;
namespace Mirror.Tests
{
    [TestFixture]
    public class ArrayWriterTest
    {
        class ArrayMessage : MessageBase
        {
            public int[] array;
        }

        [Test]
        public void TestNullArray()
        {
            ArrayMessage message = new ArrayMessage()
            {
                array = null
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.That(unpacked.array, Is.Null);
        }

        [Test]
        public void TestEmptyArray()
        {
            ArrayMessage message = new ArrayMessage()
            {
                array = new int [] { }
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new int[] {}));
        }

        [Test]
        public void TestDataArray()
        {
            ArrayMessage message = new ArrayMessage()
            {
                array = new[] { 3, 4, 5}
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new int[] {3, 4, 5 }));
        }
    }
}
