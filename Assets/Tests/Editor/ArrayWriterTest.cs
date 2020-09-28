using NUnit.Framework;
namespace Mirror.Tests
{
    [TestFixture]
    public class ArrayWriterTest
    {
        [Test]
        public void TestNullByterray()
        {
            byte[] array = null;

            byte[] data = MessagePacker.Pack(array);

            byte[] unpacked = MessagePacker.Unpack<byte[]>(data);

            Assert.IsNull(unpacked);
        }

        [Test]
        public void TestEmptyByteArray()
        {
            byte[] array = new byte[] { };

            byte[] data = MessagePacker.Pack(array);

            byte[] unpacked = MessagePacker.Unpack<byte[]>(data);

            Assert.IsNotNull(unpacked);
            Assert.IsEmpty(unpacked);
            Assert.That(unpacked, Is.EquivalentTo(new int[] { }));
        }

        [Test]
        public void TestDataByteArray()
        {
            byte[] array = new byte[] { 3, 4, 5 };

            byte[] data = MessagePacker.Pack(array);

            byte[] unpacked = MessagePacker.Unpack<byte[]>(data);

            Assert.IsNotNull(unpacked);
            Assert.IsNotEmpty(unpacked);
            Assert.That(unpacked, Is.EquivalentTo(new byte[] { 3, 4, 5 }));
        }

        [Test]
        public void TestNullIntArray()
        {
            int[] array = null;

            byte[] data = MessagePacker.Pack(array);

            int[] unpacked = MessagePacker.Unpack<int[]>(data);

            Assert.That(unpacked, Is.Null);
        }

        [Test]
        public void TestEmptyIntArray()
        {
            int[] array = new int[] { };

            byte[] data = MessagePacker.Pack(array);

            int[] unpacked = MessagePacker.Unpack<int[]>(data);

            Assert.That(unpacked, Is.EquivalentTo(new int[] { }));
        }

        [Test]
        public void TestDataIntArray()
        {
            var array = new[] { 3, 4, 5 };

            byte[] data = MessagePacker.Pack(array);

            int[] unpacked = MessagePacker.Unpack<int[]>(data);

            Assert.That(unpacked, Is.EquivalentTo(new int[] { 3, 4, 5 }));
        }
    }
}
