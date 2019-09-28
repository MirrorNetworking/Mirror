using NUnit.Framework;
namespace Mirror.Tests
{
    [TestFixture]
    public class ArrayWriterTest
    {
        class ArrayByteMessage : MessageBase
        {
            public byte[] array;
        }

        [Test]
        public void TestNullByterray()
        {
            ArrayByteMessage intMessage = new ArrayByteMessage
            {
                array = null
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ArrayByteMessage unpacked = MessagePacker.Unpack<ArrayByteMessage>(data);

            Assert.IsNull(unpacked.array);
        }

        [Test]
        public void TestEmptyByteArray()
        {
            ArrayByteMessage intMessage = new ArrayByteMessage
            {
                array = new byte[] { }
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ArrayByteMessage unpacked = MessagePacker.Unpack<ArrayByteMessage>(data);

            Assert.IsNotNull(unpacked.array);
            Assert.IsEmpty(unpacked.array);
            Assert.That(unpacked.array, Is.EquivalentTo(new int[] { }));
        }

        [Test]
        public void TestDataByteArray()
        {
            ArrayByteMessage intMessage = new ArrayByteMessage
            {
                array = new byte[] { 3, 4, 5 }
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ArrayByteMessage unpacked = MessagePacker.Unpack<ArrayByteMessage>(data);

            Assert.IsNotNull(unpacked.array);
            Assert.IsNotEmpty(unpacked.array);
            Assert.That(unpacked.array, Is.EquivalentTo(new byte[] { 3, 4, 5 }));
        }

        class ArrayIntMessage : MessageBase
        {
            public int[] array;
        }

        [Test]
        public void TestNullIntArray()
        {
            ArrayIntMessage intMessage = new ArrayIntMessage
            {
                array = null
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ArrayIntMessage unpacked = MessagePacker.Unpack<ArrayIntMessage>(data);

            Assert.That(unpacked.array, Is.Null);
        }

        [Test]
        public void TestEmptyIntArray()
        {
            ArrayIntMessage intMessage = new ArrayIntMessage
            {
                array = new int [] { }
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ArrayIntMessage unpacked = MessagePacker.Unpack<ArrayIntMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new int[] {}));
        }

        [Test]
        public void TestDataIntArray()
        {
            ArrayIntMessage intMessage = new ArrayIntMessage
            {
                array = new[] { 3, 4, 5}
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ArrayIntMessage unpacked = MessagePacker.Unpack<ArrayIntMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new int[] {3, 4, 5 }));
        }
    }
}
