using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class LocalConnectionBufferTest
    {
        readonly LocalConnectionBuffer buffer = new LocalConnectionBuffer();

        [TearDown]
        public void TearDown()
        {
            buffer.ResetBuffer();
        }

        [Test]
        public void BufferHasPacketsAfterWriter()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                writer.WriteString("Some Message");

                buffer.Write(writer.ToArraySegment());
            }

            Assert.IsTrue(buffer.HasPackets());
        }
        [Test]
        public void BufferHasNoPacketsAfterWriteAndReading()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                writer.WriteString("Some Message");

                buffer.Write(writer.ToArraySegment());
            }
            ArraySegment<byte> package = buffer.GetNextPacket();


            Assert.IsFalse(buffer.HasPackets());
        }
        [Test]
        public void BufferCanWriteAndReadPackages()
        {
            const string expectedMessage = "Some Message";
            const float expectedValue = 46.8f;
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                writer.WriteString(expectedMessage);
                writer.WriteSingle(expectedValue);

                buffer.Write(writer.ToArraySegment());
            }
            ArraySegment<byte> package = buffer.GetNextPacket();

            string message;
            float value;
            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(package))
            {
                message = reader.ReadString();
                value = reader.ReadSingle();
            }

            Assert.That(message, Is.EqualTo(expectedMessage));
            Assert.That(value, Is.EqualTo(expectedValue));
        }
        [Test]
        public void BufferReturnsMutliplePacketsInTheOrderTheyWereWriten()
        {
            const string expectedMessage1 = "first Message";
            const string expectedMessage2 = "second Message";
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                writer.WriteString(expectedMessage1);

                buffer.Write(writer.ToArraySegment());
            }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                writer.WriteString(expectedMessage2);

                buffer.Write(writer.ToArraySegment());
            }

            string message1;
            string message2;
            ArraySegment<byte> package1 = buffer.GetNextPacket();

            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(package1))
            {
                message1 = reader.ReadString();
            }

            Assert.IsTrue(buffer.HasPackets());
            ArraySegment<byte> package2 = buffer.GetNextPacket();

            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(package2))
            {
                message2 = reader.ReadString();
            }

            Assert.That(message1, Is.EqualTo(expectedMessage1));
            Assert.That(message2, Is.EqualTo(expectedMessage2));
        }
        [Test]
        public void BufferCanWriteReadMorePackageAfterCallingReset()
        {
            const string expectedMessage = "Some Message";
            const float expectedValue = 46.8f;

            for (int i = 0; i < 5; i++)
            {
                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    writer.WriteInt32(i);
                    writer.WriteString(expectedMessage);
                    writer.WriteSingle(expectedValue);

                    buffer.Write(writer.ToArraySegment());
                }
                ArraySegment<byte> package = buffer.GetNextPacket();

                int index;
                string message;
                float value;
                using (PooledNetworkReader reader = NetworkReaderPool.GetReader(package))
                {
                    index = reader.ReadInt32();
                    message = reader.ReadString();
                    value = reader.ReadSingle();
                }

                Assert.That(index, Is.EqualTo(i));
                Assert.That(message, Is.EqualTo(expectedMessage));
                Assert.That(value, Is.EqualTo(expectedValue));

                buffer.ResetBuffer();
            }
        }
    }
}
