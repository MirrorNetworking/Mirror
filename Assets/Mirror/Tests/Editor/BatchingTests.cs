using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class BatchingTests : MirrorTest
    {
        NetworkConnectionToClient connection;
        NetworkConnectionToClient.Batch batch;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // connect transport
            transport.ServerStart();
            transport.ClientConnect("localhost");

            // eat transport connect messages
            transport.clientIncoming.Clear();
            transport.serverIncoming.Clear();

            // need a connection to client with batching enabled
            connection = new NetworkConnectionToClient(42, true, 0);

            // need a batch too
            batch = new NetworkConnectionToClient.Batch();
        }

        [Test]
        public void SendEmptyBatch()
        {
            // empty batch - nothing should be sent
            connection.SendBatch(Channels.Reliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(0));
        }

        [Test]
        public void SendAlmostMaxBatchSizedMessageBatch()
        {
            // create a message < max batch size
            int max = transport.GetMaxBatchSize(Channels.Reliable);
            byte[] message = new byte[max-1];

            // add to batch queue
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            batch.writer.WriteBytes(message, 0, message.Length);

            // send batch - client should receive that exact message
            connection.SendBatch(Channels.Reliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(message.Length));
        }

        [Test]
        public void SendMaxBatchSizedMessageBatch()
        {
            // create a message == max batch size
            int max = transport.GetMaxBatchSize(Channels.Reliable);
            byte[] message = new byte[max];

            // add to batch queue
            connection.AddMessageToBatch(batch, new ArraySegment<byte>(message), Channels.Reliable);

            // send batch - client should receive that exact message
            connection.SendBatch(Channels.Reliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(message.Length));
        }

        [Test]
        public void SendTwoSmallMessagesBatch()
        {
            // create two small messages
            byte[] A = {0x01, 0x02};
            byte[] B = {0x03, 0x04};

            // add A to batch
            connection.AddMessageToBatch(batch, new ArraySegment<byte>(A), Channels.Reliable);

            // add B to batch
            connection.AddMessageToBatch(batch, new ArraySegment<byte>(B), Channels.Reliable);

            // send batch - client should receive one message that contains A, B
            connection.SendBatch(Channels.Reliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            MemoryTransport.Message msg = transport.clientIncoming.Dequeue();
            Assert.That(msg.data.Length, Is.EqualTo(4));
            Assert.That(msg.data[0], Is.EqualTo(0x01));
            Assert.That(msg.data[1], Is.EqualTo(0x02));
            Assert.That(msg.data[2], Is.EqualTo(0x03));
            Assert.That(msg.data[3], Is.EqualTo(0x04));
        }

        [Test]
        public void SendAlmostMaxBatchSizedAndSmallMessageBatch()
        {
            // create a message < max batch size
            int max = transport.GetMaxBatchSize(Channels.Reliable);
            byte[] almost = new byte[max-1];

            // create small message
            byte[] small = {0x01, 0x02};

            // add first to batch queue
            connection.AddMessageToBatch(batch, new ArraySegment<byte>(almost), Channels.Reliable);

            // add second to batch queue
            connection.AddMessageToBatch(batch, new ArraySegment<byte>(small), Channels.Reliable);

            // send batch - should send the first one and then the second one
            // because both together would've been > max
            connection.SendBatch(Channels.Reliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(2));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(almost.Length));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(small.Length));
        }

        // test to avoid the bug where sending a message > MaxBatchSize would
        // accidentally flush empty batches.
        // => sending > MaxBatchSize is allowed because transport also has a
        //    GetMaxMessageSize property.
        [Test]
        public void SendLargerMaxBatchSizedMessageBatch()
        {
            int maxBatch = transport.GetMaxBatchSize(Channels.Reliable);
            int maxPacket = transport.GetMaxPacketSize(Channels.Reliable);

            // we can only tested if transport max batch < max message
            Assert.That(maxBatch < maxPacket, Is.True);

            // create a message > batch size
            byte[] message = new byte[maxPacket];

            // add to batch queue
            connection.AddMessageToBatch(batch, new ArraySegment<byte>(message), Channels.Reliable);

            // send batch - client should receive that exact message
            connection.SendBatch(Channels.Reliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(message.Length));
        }
    }
}
