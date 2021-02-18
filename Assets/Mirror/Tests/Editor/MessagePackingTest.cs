using System;
using NUnit.Framework;
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

                int id = networkReader.ReadUInt16();
                if (id != msgType)
                    throw new FormatException("Invalid message,  could not unpack " + typeof(T).FullName);

                return networkReader.Read<T>();
            }
        }

        [Test]
        public void TestPacking()
        {
            SceneMessage message = new SceneMessage()
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = PackToByteArray(message);

            SceneMessage unpacked = UnpackFromByteArray<SceneMessage>(data);

            Assert.That(unpacked.sceneName, Is.EqualTo("Hello world"));
            Assert.That(unpacked.sceneOperation, Is.EqualTo(SceneOperation.LoadAdditive));
        }

        [Test]
        public void UnpackWrongMessage()
        {
            SpawnMessage message = new SpawnMessage();

            byte[] data = PackToByteArray(message);

            Assert.Throws<FormatException>(() =>
            {
                UpdateVarsMessage unpacked = UnpackFromByteArray<UpdateVarsMessage>(data);
            });
        }

        [Test]
        public void TestUnpackIdMismatch()
        {
            // Unpack<T> has a id != msgType case that throws a FormatException.
            // let's try to trigger it.

            SceneMessage message = new SceneMessage()
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = PackToByteArray(message);

            // overwrite the id
            data[0] = 0x01;
            data[1] = 0x02;

            Assert.Throws<FormatException>(() =>
            {
                SceneMessage unpacked = UnpackFromByteArray<SceneMessage>(data);
            });
        }

        [Test]
        public void TestUnpackMessageNonGeneric()
        {
            // try a regular message
            SceneMessage message = new SceneMessage()
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = PackToByteArray(message);
            NetworkReader reader = new NetworkReader(data);

            bool result = MessagePacking.Unpack(reader, out int msgType);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(msgType, Is.EqualTo(BitConverter.ToUInt16(data, 0)));
        }

        [Test]
        public void UnpackInvalidMessage()
        {
            // try an invalid message
            NetworkReader reader2 = new NetworkReader(new byte[0]);
            bool result2 = MessagePacking.Unpack(reader2, out int msgType2);
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
