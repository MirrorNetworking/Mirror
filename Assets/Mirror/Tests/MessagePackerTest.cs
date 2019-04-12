using NUnit.Framework;
namespace Mirror.Tests
{
    [TestFixture]
    public class MessagePackerTest
    {
        [Test]
        public void TestPacking()
        {
            SceneMessage message = new SceneMessage()
            {
                value = "Hello world"
            };

            byte[] data = MessagePacker.Pack(message);

            SceneMessage unpacked = MessagePacker.Unpack<SceneMessage>(data);

            Assert.That(unpacked.value, Is.EqualTo("Hello world"));
        }
    }
}
