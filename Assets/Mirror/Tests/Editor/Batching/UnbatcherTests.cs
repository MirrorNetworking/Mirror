using System;
using NUnit.Framework;

namespace Mirror.Tests.Batching
{
    public class UnbatcherTests
    {
        Unbatcher    unbatcher;
        const double TimeStamp = Math.PI;

        [SetUp]
        public void SetUp()
        {
            unbatcher = new Unbatcher();
        }

        [Test]
        public void GetNextMessage_NoBatches()
        {
            bool result = unbatcher.GetNextMessage(out _, out _);
            Assert.That(result, Is.False);
        }

        // test for nimoyd bug, where calling getnextmessage after the previous
        // call already returned false would cause an InvalidOperationException.
        [Test]
        public void GetNextMessage_True_False_False_InvalidOperationException()
        {
            // add batch
            byte[] batch = BatcherTests.MakeBatch(TimeStamp, new byte[2]);
            unbatcher.AddBatch(new ArraySegment<byte>(batch));

            // get next message, pretend we read the whole thing
            bool result = unbatcher.GetNextMessage(out ArraySegment<byte> message, out _);
            Assert.That(result, Is.True);

            // shouldn't get another one
            result = unbatcher.GetNextMessage(out _, out _);
            Assert.That(result, Is.False);

            // calling it again was causing "InvalidOperationException: Queue empty"
            result = unbatcher.GetNextMessage(out _, out _);
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetNextMessage_OneBatch()
        {
            // add one batch
            byte[] batch = BatcherTests.MakeBatch(TimeStamp, new byte[] {0x01, 0x02});
            unbatcher.AddBatch(new ArraySegment<byte>(batch));

            // get next message
            bool result = unbatcher.GetNextMessage(out ArraySegment<byte> message, out double remoteTimeStamp);
            NetworkReader reader = new NetworkReader(message);
            Assert.That(result, Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x01));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x02));
            Assert.That(remoteTimeStamp, Is.EqualTo(TimeStamp));

            // there should be no more messages
            result = unbatcher.GetNextMessage(out _, out _);
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetNextMessage_MultipleBatches()
        {
            // add first batch
            byte[] firstBatch = BatcherTests.MakeBatch(TimeStamp, new byte[] {0x01, 0x02});
            unbatcher.AddBatch(new ArraySegment<byte>(firstBatch));

            // add second batch
            byte[] secondBatch = BatcherTests.MakeBatch(TimeStamp + 1, new byte[] {0x03, 0x04});
            unbatcher.AddBatch(new ArraySegment<byte>(secondBatch));

            // get next message, read everything
            bool result = unbatcher.GetNextMessage(out ArraySegment<byte> message, out double remoteTimeStamp);
            Assert.That(result, Is.True);
            NetworkReader reader = new NetworkReader(message);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x01));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x02));
            Assert.That(remoteTimeStamp, Is.EqualTo(TimeStamp));

            // get next message, should point to next batch at Timestamp + 1
            result = unbatcher.GetNextMessage(out message, out remoteTimeStamp);
            Assert.That(result, Is.True);
            reader = new NetworkReader(message);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x03));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x04));
            Assert.That(remoteTimeStamp, Is.EqualTo(TimeStamp + 1));

            // there should be no more messages
            result = unbatcher.GetNextMessage(out _, out _);
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
            byte[] firstBatch = BatcherTests.MakeBatch(TimeStamp, new byte[] {0x01, 0x02});
            unbatcher.AddBatch(new ArraySegment<byte>(firstBatch));

            // read everything
            bool result = unbatcher.GetNextMessage(out ArraySegment<byte> message, out double remoteTimeStamp);
            Assert.That(result, Is.True);
            NetworkReader reader = new NetworkReader(message);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x01));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x02));
            Assert.That(remoteTimeStamp, Is.EqualTo(TimeStamp));

            // try to read again.
            // reader will be at limit, which should retire the batch.
            result = unbatcher.GetNextMessage(out _, out _);
            Assert.That(result, Is.False);

            // add new batch
            byte[] secondBatch = BatcherTests.MakeBatch(TimeStamp + 1, new byte[] {0x03, 0x04});
            unbatcher.AddBatch(new ArraySegment<byte>(secondBatch));

            // read everything
            result = unbatcher.GetNextMessage(out message, out remoteTimeStamp);
            Assert.That(result, Is.True);
            reader = new NetworkReader(message);
            Assert.That(reader.ReadByte(), Is.EqualTo(0x03));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x04));
            Assert.That(remoteTimeStamp, Is.EqualTo(TimeStamp + 1));
        }
    }
}
