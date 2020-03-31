using System;
using System.IO;
using NUnit.Framework;
namespace Mirror.Tests
{
    [TestFixture]
    public class MessagePackerTest
    {
        [Test]
        public void TestPacking()
        {
            var message = new SceneMessage
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = MessagePacker.Pack(message);

            SceneMessage unpacked = MessagePacker.Unpack<SceneMessage>(data);

            Assert.That(unpacked.sceneName, Is.EqualTo("Hello world"));
            Assert.That(unpacked.sceneOperation, Is.EqualTo(SceneOperation.LoadAdditive));
        }

        [Test]
        public void UnpackWrongMessage()
        {
            var message = new ReadyMessage();

            byte[] data = MessagePacker.Pack(message);

            Assert.Throws<FormatException>(() =>
            {
                AddPlayerMessage unpacked = MessagePacker.Unpack<AddPlayerMessage>(data);
            });
        }

        [Test]
        public void TestUnpackIdMismatch()
        {
            // Unpack<T> has a id != msgType case that throws a FormatException.
            // let's try to trigger it.

            var message = new SceneMessage()
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = MessagePacker.Pack(message);

            // overwrite the id
            data[0] = 0x01;
            data[1] = 0x02;

            Assert.Throws<FormatException>(delegate
            {
                _ = MessagePacker.Unpack<SceneMessage>(data);

            });
        }

        [Test]
        public void TestUnpackMessageNonGeneric()
        {
            // try a regular message
            var message = new SceneMessage()
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = MessagePacker.Pack(message);
            var reader = new NetworkReader(data);

            int msgType = MessagePacker.UnpackId(reader);
            Assert.That(msgType, Is.EqualTo(BitConverter.ToUInt16(data, 0)));
        }

        [Test]
        public void UnpackInvalidMessage()
        {
            // try an invalid message
            Assert.Throws<EndOfStreamException>(() =>
            {
                var reader2 = new NetworkReader(new byte[0]);
                int msgType2 = MessagePacker.UnpackId(reader2);
            });
        }
    }
}
