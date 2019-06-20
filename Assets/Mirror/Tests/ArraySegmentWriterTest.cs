using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class ArraySegmentWriterTest
    {
        class ArrayMessage : MessageBase
        {
            public ArraySegment<byte> array;
        }

        [Test]
        public void TestEmptyByteArray()
        {
            ArrayMessage message = new ArrayMessage
            {
                array = new ArraySegment<byte>(new byte[0])
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.IsNotNull(unpacked.array.Array);
            Assert.That(unpacked.array.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestNullByteArray()
        {
            ArrayMessage message = new ArrayMessage
            {
                array = default
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.IsNull(unpacked.array.Array);
            Assert.That(unpacked.array.Offset, Is.EqualTo(0));
            Assert.That(unpacked.array.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestSegmentByteArray()
        {
            byte[] sourcedata = { 0, 1, 2, 3, 4, 5, 6 };

            ArrayMessage message = new ArrayMessage
            {
                array = new ArraySegment<byte>(sourcedata, 3, 2)
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.IsNotNull(unpacked.array.Array);
            Assert.That(unpacked.array.Count, Is.EqualTo(2));
            Assert.That(unpacked.array, Is.EquivalentTo(new byte[] { 3, 4 }));
        }
    }
}
