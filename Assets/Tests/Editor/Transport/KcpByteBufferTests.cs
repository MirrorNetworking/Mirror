using Mirror.KCP;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class ByteBufferTests
    {
        ByteBuffer buffer;

        [SetUp]
        public void SetUp()
        {
            buffer = new ByteBuffer();
        }

        [Test]
        public void WriteBytes()
        {
            byte[] bytes = {0xAA, 0xBB, 0xCC, 0xDD};
            buffer.WriteBytes(bytes, 0, bytes.Length);
            Assert.That(buffer.Position, Is.EqualTo(4));
            Assert.That(buffer.RawBuffer[0], Is.EqualTo(0xAA));
            Assert.That(buffer.RawBuffer[1], Is.EqualTo(0xBB));
        }

        // need to make sure that multiple writes to same buffer still work fine
        [Test]
        public void WriteBytesTwice()
        {
            // first half
            byte[] bytes = {0xAA, 0xBB};
            buffer.WriteBytes(bytes, 0, 2);
            Assert.That(buffer.Position, Is.EqualTo(2));
            Assert.That(buffer.RawBuffer[0], Is.EqualTo(0xAA));
            Assert.That(buffer.RawBuffer[1], Is.EqualTo(0xBB));

            // second half
            byte[] bytes2 = {0xCC, 0xDD};
            buffer.WriteBytes(bytes2, 0, 2);
            Assert.That(buffer.Position, Is.EqualTo(4));
            Assert.That(buffer.RawBuffer[0], Is.EqualTo(0xAA));
            Assert.That(buffer.RawBuffer[1], Is.EqualTo(0xBB));
            Assert.That(buffer.RawBuffer[2], Is.EqualTo(0xCC));
            Assert.That(buffer.RawBuffer[3], Is.EqualTo(0xDD));
        }
    }
}
