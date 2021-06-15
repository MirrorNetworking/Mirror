using System;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests.Batching
{
    public class BatcherTests
    {
        Batcher batcher;
        const int MaxBatchSize = 4;
        NetworkWriter writer;

        [SetUp]
        public void SetUp()
        {
            batcher = new Batcher(MaxBatchSize);
            writer = new NetworkWriter();
        }

        [Test]
        public void AddMessage_AddsToQueue()
        {
            byte[] message = {0x01, 0x02};
            bool result = batcher.AddMessage(new ArraySegment<byte>(message));
            Assert.That(result, Is.True);
        }

        [Test]
        public void AddMessage_DetectsTooBig()
        {
            byte[] message = new byte[MaxBatchSize + 1];
            bool result = batcher.AddMessage(new ArraySegment<byte>(message));
            Assert.That(result, Is.False);
        }

        [Test]
        public void MakeNextBatch_OnlyAcceptsFreshWriter()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01}));

            writer.WriteByte(0);
            Assert.Throws<ArgumentException>(() => {
                batcher.MakeNextBatch(writer);
            });
        }

        [Test]
        public void MakeNextBatch_NoMessage()
        {
            // make batch with no message
            bool result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(false));
        }

        [Test]
        public void MakeNextBatch_OneMessage()
        {
            // add message
            byte[] message = {0x01, 0x02};
            batcher.AddMessage(new ArraySegment<byte>(message));

            // make batch
            bool result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(message));
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_AlmostMaxBatchSize()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01, 0x02}));
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03}));

            // make batch
            bool result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x01, 0x02, 0x03}));

            // there should be no more batches to make
            Assert.That(batcher.MakeNextBatch(writer), Is.False);
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_ExactlyMaxBatchSize()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01, 0x02}));
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03, 0x04}));

            // make batch
            bool result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x01, 0x02, 0x03, 0x04}));

            // there should be no more batches to make
            Assert.That(batcher.MakeNextBatch(writer), Is.False);
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_LargerMaxBatchSize()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01, 0x02}));
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03, 0x04}));
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x05}));

            // first batch
            bool result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x01, 0x02, 0x03, 0x04}));

            // reset writer
            writer.Position = 0;

            // second batch
            result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x05}));
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_Small_Giant_Small()
        {
            // small, too big to include in batch, small
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01}));
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x02, 0x03, 0x04, 0x05}));
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x06, 0x07}));

            // first batch
            bool result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x01}));

            // reset writer
            writer.Position = 0;

            // second batch
            result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x02, 0x03, 0x04, 0x05}));

            // reset writer
            writer.Position = 0;

            // third batch
            result = batcher.MakeNextBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(new byte[]{0x06, 0x07}));
        }
    }
}
