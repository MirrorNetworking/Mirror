using NUnit.Framework;
using System;

namespace Mirror.Tests
{
    [TestFixture]
    public class ArraySegmentWriterTest
    {
        class ArrayMessage : MessageBase
        {
            public ArraySegment<int> array;
        }

        [Test]
        public void TestEmptyArray()
        {
            ArrayMessage message = new ArrayMessage()
            {
                array = default
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.That(unpacked.array.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestSegmentArray()
        {
            int[] sourcedata = { 0, 1, 2, 3, 4, 5, 6 };

            ArrayMessage message = new ArrayMessage()
            {
                array = new ArraySegment<int>(sourcedata, 3, 2)
            };

            byte[] data = MessagePacker.Pack(message);

            ArrayMessage unpacked = MessagePacker.Unpack<ArrayMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new int[] { 3, 4 }));
        }

    }
}
