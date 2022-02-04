using System;
using Mirror.Tests.MessageTests;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class MessagePackingTest
    {
        public struct EmptyMessage : NetworkMessage {}

        // helper function to pack message into a simple byte[]
        public static byte[] PackToByteArray<T>(T message)
            where T : struct, NetworkMessage
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                MessagePacking.Pack(message, writer);
                return writer.ToArray();
            }
        }

        // unpack a message we received
        public static T UnpackFromByteArray<T>(byte[] data)
            where T : struct, NetworkMessage
        {
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(data))
            {
                int msgType = MessagePacking.GetId<T>();

                int id = networkReader.ReadUShort();
                if (id != msgType)
                    throw new FormatException($"Invalid message,  could not unpack {typeof(T).FullName}");

                return networkReader.Read<T>();
            }
        }

        // message id is generated from message.FullName.
        // should be consistent across all platforms.
        [Test]
        public void GetId()
        {
            // "Mirror.Tests.MessageTests.TestMessage"
            Debug.Log(typeof(TestMessage).FullName);
            Assert.That(MessagePacking.GetId<TestMessage>(), Is.EqualTo(0x8706));
        }

        [Test]
        public void TestPacking()
        {
            TestMessage message = new TestMessage()
            {
                IntValue = 42,
                StringValue = "Hello world"
            };

            byte[] data = PackToByteArray(message);

            TestMessage unpacked = UnpackFromByteArray<TestMessage>(data);

            Assert.That(unpacked.StringValue, Is.EqualTo("Hello world"));
            Assert.That(unpacked.IntValue, Is.EqualTo(42));
        }

        [Test]
        public void UnpackWrongMessage()
        {
            SpawnMessage message = new SpawnMessage();

            byte[] data = PackToByteArray(message);

            Assert.Throws<FormatException>(() =>
            {
                ReadyMessage unpacked = UnpackFromByteArray<ReadyMessage>(data);
            });
        }

        [Test]
        public void TestUnpackIdMismatch()
        {
            // Unpack<T> has a id != msgType case that throws a FormatException.
            // let's try to trigger it.

            TestMessage message = new TestMessage()
            {
                IntValue = 42,
                StringValue = "Hello world"
            };

            byte[] data = PackToByteArray(message);

            // overwrite the id
            data[0] = 0x01;
            data[1] = 0x02;

            Assert.Throws<FormatException>(() =>
            {
                TestMessage unpacked = UnpackFromByteArray<TestMessage>(data);
            });
        }

        [Test]
        public void TestUnpackMessageNonGeneric()
        {
            // try a regular message
            TestMessage message = new TestMessage()
            {
                IntValue = 42,
                StringValue = "Hello world"
            };

            byte[] data = PackToByteArray(message);
            NetworkReader reader = new NetworkReader(data);

            bool result = MessagePacking.Unpack(reader, out ushort msgType);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(msgType, Is.EqualTo(BitConverter.ToUInt16(data, 0)));
        }

        [Test]
        public void UnpackInvalidMessage()
        {
            // try an invalid message
            NetworkReader reader2 = new NetworkReader(new byte[0]);
            bool result2 = MessagePacking.Unpack(reader2, out ushort msgType2);
            Assert.That(result2, Is.EqualTo(false));
            Assert.That(msgType2, Is.EqualTo(0));
        }

        [Test]
        public void MessageIdIsCorrectLength()
        {
            NetworkWriter writer = new NetworkWriter();
            MessagePacking.Pack(new EmptyMessage(), writer);

            ArraySegment<byte> segment = writer.ToArraySegment();

            Assert.That(segment.Count, Is.EqualTo(MessagePacking.HeaderSize), "Empty message should have same size as HeaderSize");
        }
    }
}
