using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class BatchingTests
    {
        MemoryTransport transport;
        NetworkConnectionToClient connection;
        NetworkConnectionToClient.Batch batch;

        [SetUp]
        public void SetUp()
        {
            // need a transport to send & receive
            Transport.activeTransport = transport = new GameObject().AddComponent<MemoryTransport>();
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

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(Transport.activeTransport.gameObject);
            Transport.activeTransport = null;
        }

        [Test]
        public void SendEmptyBatch()
        {
            // empty batch - nothing should be sent
            connection.SendBatch(Channels.DefaultReliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(0));
        }

        [Test]
        public void SendAlmostMaxBatchSizedMessageBatch()
        {
            // create a message < max batch size
            int max = transport.GetMaxBatchSize(Channels.DefaultReliable);
            byte[] message = new byte[max-1];

            // add to batch queue
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(message, 0, message.Length);
            batch.messages.Enqueue(writer);

            // send batch - client should receive that exact message
            connection.SendBatch(Channels.DefaultReliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(message.Length));
        }

        [Test]
        public void SendMaxBatchSizedMessageBatch()
        {
            // create a message == max batch size
            int max = transport.GetMaxBatchSize(Channels.DefaultReliable);
            byte[] message = new byte[max];

            // add to batch queue
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(message, 0, message.Length);
            batch.messages.Enqueue(writer);

            // send batch - client should receive that exact message
            connection.SendBatch(Channels.DefaultReliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(message.Length));
        }

        [Test]
        public void SendTwoSmallMessagesBatch()
        {
            // create two small messages
            byte[] A = {0x01, 0x02};
            byte[] B = {0x03, 0x04};

            // add A to batch queue
            PooledNetworkWriter writerA = NetworkWriterPool.GetWriter();
            writerA.WriteBytes(A, 0, A.Length);
            batch.messages.Enqueue(writerA);

            // add B to batch queue
            PooledNetworkWriter writerB = NetworkWriterPool.GetWriter();
            writerB.WriteBytes(B, 0, A.Length);
            batch.messages.Enqueue(writerB);

            // send batch - client should receive one message that contains A, B
            connection.SendBatch(Channels.DefaultReliable, batch);
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
            int max = transport.GetMaxBatchSize(Channels.DefaultReliable);
            byte[] almost = new byte[max-1];

            // create small message
            byte[] small = {0x01, 0x02};

            // add first to batch queue
            PooledNetworkWriter writerAlmost = NetworkWriterPool.GetWriter();
            writerAlmost.WriteBytes(almost, 0, almost.Length);
            batch.messages.Enqueue(writerAlmost);

            // add second to batch queue
            PooledNetworkWriter writerSmall = NetworkWriterPool.GetWriter();
            writerSmall.WriteBytes(small, 0, small.Length);
            batch.messages.Enqueue(writerSmall);

            // send batch - should send the first one and then the second one
            // because both together would've been > max
            connection.SendBatch(Channels.DefaultReliable, batch);
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
            int maxBatch = transport.GetMaxBatchSize(Channels.DefaultReliable);
            int maxPacket = transport.GetMaxPacketSize(Channels.DefaultReliable);

            // we can only tested if transport max batch < max message
            Assert.That(maxBatch < maxPacket, Is.True);

            // create a message > batch size
            byte[] message = new byte[maxPacket];

            // add to batch queue
            PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
            writer.WriteBytes(message, 0, message.Length);
            batch.messages.Enqueue(writer);

            // send batch - client should receive that exact message
            connection.SendBatch(Channels.DefaultReliable, batch);
            Assert.That(transport.clientIncoming.Count, Is.EqualTo(1));
            Assert.That(transport.clientIncoming.Dequeue().data.Length, Is.EqualTo(message.Length));
        }
    }
}
