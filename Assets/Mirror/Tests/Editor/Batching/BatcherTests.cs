using System;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests.Batching
{
    public class BatcherTests
    {
        Batcher batcher;
        NetworkWriter writer;

        // threshold to test batcher with multiple batches.
        // each batch can be 8 bytes timestamp + 8 bytes data
        const int Threshold = 8 + 6;

        // timestamp and serialized timestamp for convenience
        const double TimeStamp = Math.PI;

        [SetUp]
        public void SetUp()
        {
            batcher = new Batcher(Threshold);
            writer = new NetworkWriter();
        }

        // helper function to create a batch prefixed by timestamp
        public static byte[] MakeBatch(double tickTimeStamp, byte[] message)
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteDouble(tickTimeStamp);

            Compression.CompressVarUInt(writer, (ulong)message.Length);
            writer.WriteBytes(message, 0, message.Length);

            return writer.ToArray();
        }

        public static byte[] MakeBatch(double tickTimeStamp, byte[] messageA, byte[] messageB)
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteDouble(tickTimeStamp);

            Compression.CompressVarUInt(writer, (ulong)messageA.Length);
            writer.WriteBytes(messageA, 0, messageA.Length);

            Compression.CompressVarUInt(writer, (ulong)messageB.Length);
            writer.WriteBytes(messageB, 0, messageB.Length);

            return writer.ToArray();
        }

        [Test]
        public void AddMessage()
        {
            byte[] message = {0x01, 0x02};
            batcher.AddMessage(new ArraySegment<byte>(message), TimeStamp);
        }

        // test to prevent the following issue from ever happening again:
        //
        // - NetworkEarlyUpdate @ t=1 processes transport messages
        //   - a handler replies by sending a message
        //     - a new batch is started @ t=1, timestamp is encoded
        // - NetworkLateUpdate @ t=2 decides it's time to broadcast
        //   - NetworkTransform sends @ t=2
        //     - we add to the above batch which already encoded t=1
        // - Client receives the batch which timestamp t=1
        //   - NetworkTransform uses remoteTime for interpolation
        //     remoteTime is the batch timestamp which is t=1
        //     - the NetworkTransform message is actually t=2
        // => smooth interpolation would be impossible!
        //    NT thinks the position was @ t=1 but actually it was @ t=2 !
        [Test]
        public void AddMessage_TimestampMismatch()
        {
            // add message @ t=1
            byte[] message1 = {0x01, 0x01};
            batcher.AddMessage(new ArraySegment<byte>(message1), 1);

            // add message @ t=2
            byte[] message2 = {0x02, 0x02};
            batcher.AddMessage(new ArraySegment<byte>(message2), 2);

            // call getbatch: this should only contain the message @ t=1 !
            // <<tickTimeStamp:8, message>>
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(1, message1)));

            // call getbatch: this should now contain the message @ t=2 !
            // <<tickTimeStamp:8, message>>
            writer.Position = 0;
            result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(2, message2)));
        }

        [Test]
        public void MakeNextBatch_OnlyAcceptsFreshWriter()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01}), TimeStamp);

            writer.WriteByte(0);
            Assert.Throws<ArgumentException>(() => {
                batcher.GetBatch(writer);
            });
        }

        [Test]
        public void MakeNextBatch_NoMessage()
        {
            // make batch with no message
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(false));
        }

        [Test]
        public void MakeNextBatch_OneMessage()
        {
            // add message
            byte[] message = {0x01, 0x02};
            batcher.AddMessage(new ArraySegment<byte>(message), TimeStamp);

            // make batch
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, message)));
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_AlmostFullBatch()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01, 0x02}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03}), TimeStamp);

            // make batch
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x01, 0x02}, new byte[]{0x03})));

            // there should be no more batches to make
            Assert.That(batcher.GetBatch(writer), Is.False);
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_ExactlyFullBatch()
        {
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01, 0x02}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03, 0x04}), TimeStamp);

            // make batch
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x01, 0x02}, new byte[]{0x03, 0x04})));

            // there should be no more batches to make
            Assert.That(batcher.GetBatch(writer), Is.False);
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_MoreThanOneBatch()
        {
            // with header, that's 3 bytes per message = 8 bytes = over threshold
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01, 0x02}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03, 0x04}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x05}), TimeStamp);

            // first batch
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x01, 0x02}, new byte[]{0x03, 0x04})));

            // reset writer
            writer.Position = 0;

            // second batch
            result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x05})));
        }

        [Test]
        public void MakeNextBatch_MultipleMessages_Small_Giant_Small()
        {
            // small, too big to include in batch, small
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x02, 0x03, 0x04, 0x05}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x06, 0x07}), TimeStamp);

            // first batch
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x01})));

            // reset writer
            writer.Position = 0;

            // second batch
            result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x02, 0x03, 0x04, 0x05})));

            // reset writer
            writer.Position = 0;

            // third batch
            result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));

            // check result: <<tickTimeStamp:8, message>>
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x06, 0x07})));
        }

        // messages > threshold should simply be single batches.
        // those need to be supported too, for example:
        //   kcp prefers MTU sized batches
        //   but we still allow up to 144 KB max message size
        [Test]
        public void MakeNextBatch_LargerThanThreshold()
        {
            // make a larger than threshold message
            byte[] large = new byte[Threshold + 1];
            for (int i = 0; i < Threshold + 1; ++i)
                large[i] = (byte)i;
            batcher.AddMessage(new ArraySegment<byte>(large), TimeStamp);

            // result should be only the large message
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, large)));
        }

        // messages > threshold should simply be single batches.
        // those need to be supported too, for example:
        //   kcp prefers MTU sized batches
        //   but we still allow up to 144 KB max message size
        [Test]
        public void MakeNextBatch_LargerThanThreshold_BetweenSmallerMessages()
        {
            // make a larger than threshold message
            byte[] large = new byte[Threshold + 1];
            for (int i = 0; i < Threshold + 1; ++i)
                large[i] = (byte)i;

            // add two small, one large, two small messages.
            // to make sure everything around it is still batched,
            // and the large one is a separate batch.
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x02}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(large), TimeStamp + 1);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03}), TimeStamp + 2);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x04}), TimeStamp + 2);

            // first batch should be the two small messages with size headers
            bool result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp, new byte[]{0x01}, new byte[]{0x02})));

            // reset writer
            writer.Position = 0;

            // second batch should be only the large message
            result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp + 1, large)));

            // reset writer
            writer.Position = 0;

            // third batch be the two small messages
            result = batcher.GetBatch(writer);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(writer.ToArray().SequenceEqual(MakeBatch(TimeStamp + 2, new byte[]{0x03}, new byte[]{0x04})));
        }

        // if a batch contains ABC,
        // and unbatching only deserializes half of B,
        // then C will end up corrupted,
        // and nothing will indicate which message caused it.
        // days & weeks were lost on this.
        [Test]
        public void MessageSerializationMismatch()
        {
            // batch with correct size
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{1}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{2}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{3}), TimeStamp);
            Assert.That(batcher.GetBatch(writer), Is.True);

            // feed batch to unbatcher
            Unbatcher unbatcher = new Unbatcher();
            unbatcher.AddBatch(writer);

            // read A correctly
            Assert.That(unbatcher.GetNextMessage(out ArraySegment<byte> message, out _), Is.True);
            NetworkReader reader = new NetworkReader(message);
            Assert.That(reader.ReadByte(), Is.EqualTo(1));

            // read B only partially.
            // this can happen if a NetworkMessage does custom serialization,
            // and does early return in Deserialize.
            // for example, SmoothSync.
            Assert.That(unbatcher.GetNextMessage(out message, out _), Is.True);
            // reader = new NetworkReader(message);
            // Assert.That(reader.ReadByte(), Is.EqualTo(2));

            // read C. this will be corrupted
            Assert.That(unbatcher.GetNextMessage(out message, out _), Is.True);
            reader = new NetworkReader(message);
            Assert.That(reader.ReadByte(), Is.EqualTo(3));
        }

        [Test]
        public void ClearReturnsToPool()
        {
            int previousCount = NetworkWriterPool.Count;

            // add a few messages
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x01}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x02}), TimeStamp);
            batcher.AddMessage(new ArraySegment<byte>(new byte[]{0x03}), TimeStamp);
            Assert.That(NetworkWriterPool.Count, Is.LessThan(previousCount));

            // clear
            batcher.Clear();
            Assert.That(NetworkWriterPool.Count, Is.EqualTo(previousCount));
        }
    }
}
