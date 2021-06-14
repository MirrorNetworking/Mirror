using System;
using NUnit.Framework;

namespace Mirror.Tests.Batching
{
    public class UnbatcherTests
    {
        Unbatcher unbatcher;

        [SetUp]
        public void SetUp()
        {
            unbatcher = new Unbatcher();
        }

        [Test]
        public void GetNextMessage_NoBatches()
        {
            bool result = unbatcher.GetNextMessage(out NetworkReader _);
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetNextMessage_OneBatch()
        {
            // add one batch
            byte[] batch = {0x01, 0x02};
            unbatcher.AddBatch(new ArraySegment<byte>(batch));

            // get next message, read first byte
            bool result = unbatcher.GetNextMessage(out NetworkReader reader);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x01));

            // get next message, read last byte
            result = unbatcher.GetNextMessage(out reader);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x02));

            // there should be no more messages
            result = unbatcher.GetNextMessage(out _);
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetNextMessage_MultipleBatches()
        {
            // add first batch
            byte[] firstBatch = {0x01, 0x02};
            unbatcher.AddBatch(new ArraySegment<byte>(firstBatch));

            // add second batch
            byte[] secondBatch = {0x03, 0x04};
            unbatcher.AddBatch(new ArraySegment<byte>(secondBatch));

            // get next message, read everything
            bool result = unbatcher.GetNextMessage(out NetworkReader reader);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x01));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x02));

            // get next message, should point to next batch
            result = unbatcher.GetNextMessage(out reader);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x03));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x04));

            // there should be no more messages
            result = unbatcher.GetNextMessage(out _);
            Assert.That(result, Is.False);
        }

        // make sure that retiring a batch, then adding a new batch works.
        // previously there was a bug where the batch was retired,
        // the reader still pointed to the old batch with pos=len,
        // a new batch was added
        // GetNextMessage() still returned false because reader still pointed to
        // the old batch with pos=len.
        [Test]
        public void RetireBatchAndTryNewBatch()
        {
            // add first batch
            byte[] firstBatch = {0x01, 0x02};
            unbatcher.AddBatch(new ArraySegment<byte>(firstBatch));

            // read everything
            bool result = unbatcher.GetNextMessage(out NetworkReader reader);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x01));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x02));

            // try to read again.
            // reader will be at limit, which should retire the batch.
            result = unbatcher.GetNextMessage(out reader);
            Assert.That(result, Is.False);

            // add new batch
            byte[] secondBatch = {0x03, 0x04};
            unbatcher.AddBatch(new ArraySegment<byte>(secondBatch));

            // read everything
            result = unbatcher.GetNextMessage(out reader);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x03));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x04));
        }
    }
}
