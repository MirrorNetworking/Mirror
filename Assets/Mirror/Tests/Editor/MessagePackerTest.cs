using System;
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
        public void TestUnpackIdMismatch()
        {
            // Unpack<T> has a id != msgType case that throws a FormatException.
            // let's try to trigger it.

            SceneMessage message = new SceneMessage()
            {
                sceneName = "Hello world",
                sceneOperation = SceneOperation.LoadAdditive
            };

            byte[] data = MessagePacker.Pack(message);

            // overwrite the id
            data[0] = 0x01;
            data[1] = 0x02;

            try
            {
                SceneMessage unpacked = MessagePacker.Unpack<SceneMessage>(data);
                // BAD: IF WE GET HERE THEN NO EXCEPTION WAS THROWN
                Assert.Fail();
            }
            catch (FormatException)
            {
                // GOOD
            }
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

            byte[] data = MessagePacker.Pack(message);
            NetworkReader reader = new NetworkReader(data);

            int msgType = MessagePacker.UnpackId(reader);
            Assert.That(msgType, Is.EqualTo(BitConverter.ToUInt16(data, 0)));

            // try an invalid message
            NetworkReader reader2 = new NetworkReader(new byte[0]);
            int msgType2 = MessagePacker.UnpackId(reader2);
            Assert.That(msgType2, Is.EqualTo(0));
        }
    }
}
