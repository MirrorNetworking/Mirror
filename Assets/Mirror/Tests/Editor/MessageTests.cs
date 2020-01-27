using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void ErrorMessageTest()
        {
            // try setting value with constructor
            ErrorMessage errorMessage = new ErrorMessage(123);
            Assert.That(errorMessage.value, Is.EqualTo(123));

            // try deserialize
            byte[] data = { 123 };
            errorMessage.Deserialize(new NetworkReader(data));
            Assert.That(errorMessage.value, Is.EqualTo(123));

            // try serialize
            NetworkWriter writer = new NetworkWriter();
            errorMessage.Serialize(writer);
            Assert.That(writer.ToArray()[0], Is.EqualTo(123));
        }

        [Test]
        public void CommandMessageTest()
        {
            // try setting value with constructor
            CommandMessage message = new CommandMessage {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[]{0x01, 0x02})
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            CommandMessage fresh = new CommandMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.componentIndex, Is.EqualTo(message.componentIndex));
            Assert.That(fresh.functionHash, Is.EqualTo(message.functionHash));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }

        [Test]
        public void RpcMessageTest()
        {
            // try setting value with constructor
            RpcMessage message = new RpcMessage {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[]{0x01, 0x02})
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            RpcMessage fresh = new RpcMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.componentIndex, Is.EqualTo(message.componentIndex));
            Assert.That(fresh.functionHash, Is.EqualTo(message.functionHash));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }

        [Test]
        public void SyncEventMessageTest()
        {
            // try setting value with constructor
            SyncEventMessage message = new SyncEventMessage {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[]{0x01, 0x02})
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            SyncEventMessage fresh = new SyncEventMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.componentIndex, Is.EqualTo(message.componentIndex));
            Assert.That(fresh.functionHash, Is.EqualTo(message.functionHash));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }
    }
}
