using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void CommandMessageTest()
        {
            // try setting value with constructor
            var message = new CommandMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };

            // serialize
            var writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            var fresh = new CommandMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.componentIndex, Is.EqualTo(message.componentIndex));
            Assert.That(fresh.functionHash, Is.EqualTo(message.functionHash));
            Assert.That(fresh.payload, Has.Count.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }

        private void TestSerializeDeserialize<T>(T message) where T : IMessageBase, new()
        {
            // serialize
            var writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            var fresh = new T();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void DisconnectMessageTest()
        {
            TestSerializeDeserialize(new DisconnectMessage());
        }

        [Test]
        public void ErrorMessageTest()
        {
            TestSerializeDeserialize(new ErrorMessage(42));
        }

        [Test]
        public void NetworkPingMessageTest()
        {
            TestSerializeDeserialize(new NetworkPingMessage(DateTime.Now.ToOADate()));
        }

        [Test]
        public void NetworkPongMessageTest()
        {
            TestSerializeDeserialize(new NetworkPongMessage
            {
                clientTime = DateTime.Now.ToOADate(),
                serverTime = DateTime.Now.ToOADate(),
            });
        }

        [Test]
        public void NotReadyMessageTest()
        {
            TestSerializeDeserialize(new NotReadyMessage());
        }

        [Test]
        public void ObjectDestroyMessageTest()
        {
            TestSerializeDeserialize(new ObjectDestroyMessage
            {
                netId = 42,
            });
        }

        [Test]
        public void ObjectHideMessageTest()
        {
            TestSerializeDeserialize(new ObjectHideMessage
            {
                netId = 42,
            });
        }

        [Test]
        public void ObjectSpawnFinishedMessageTest()
        {
            TestSerializeDeserialize(new ObjectSpawnFinishedMessage());
        }

        [Test]
        public void ObjectSpawnStartedMessageTest()
        {
            // try setting value with constructor
            TestSerializeDeserialize(new ObjectSpawnStartedMessage());
        }

        [Test]
        public void ReadyMessageTest()
        {
            TestSerializeDeserialize(new ReadyMessage());
        }

        [Test]
        public void AddPlayerMessageTest()
        {
            TestSerializeDeserialize(new AddPlayerMessage());
        }

        [Test]
        public void RemovePlayerMessageTest()
        {
            TestSerializeDeserialize(new RemovePlayerMessage());
        }

        [Test]
        public void RpcMessageTest()
        {
            // try setting value with constructor
            var message = new RpcMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };

            // serialize
            var writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            var fresh = new RpcMessage();
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
        public void SpawnMessageTest()
        {
            DoTest(0);
            DoTest(42);

            void DoTest(ulong testSceneId)
            {
                // try setting value with constructor
                var message = new SpawnMessage
                {
                    netId = 42,
                    isLocalPlayer = true,
                    isOwner = true,
                    sceneId = testSceneId,
                    assetId = Guid.NewGuid(),
                    position = UnityEngine.Vector3.one,
                    rotation = UnityEngine.Quaternion.identity,
                    scale = UnityEngine.Vector3.one,
                    payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
                };

                UnityEngine.Debug.Log($"sceneId:{message.sceneId} | assetId:{message.assetId}");

                // serialize
                var writer = new NetworkWriter();
                message.Serialize(writer);
                byte[] writerData = writer.ToArray();

                // deserialize the same data - do we get the same result?
                var fresh = new SpawnMessage();
                fresh.Deserialize(new NetworkReader(writerData));
                Assert.That(fresh.netId, Is.EqualTo(message.netId));
                Assert.That(fresh.isLocalPlayer, Is.EqualTo(message.isLocalPlayer));
                Assert.That(fresh.isOwner, Is.EqualTo(message.isOwner));
                Assert.That(fresh.sceneId, Is.EqualTo(message.sceneId));
                if (fresh.sceneId == 0)
                    Assert.That(fresh.assetId, Is.EqualTo(message.assetId));
                Assert.That(fresh.position, Is.EqualTo(message.position));
                Assert.That(fresh.rotation, Is.EqualTo(message.rotation));
                Assert.That(fresh.scale, Is.EqualTo(message.scale));
                Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
                for (int i = 0; i < fresh.payload.Count; ++i)
                    Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                        Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
            }
        }

        [Test]
        public void SyncEventMessageTest()
        {
            // try setting value with constructor
            var message = new SyncEventMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };

            // serialize
            var writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            var fresh = new SyncEventMessage();
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
        public void UpdateVarsMessageTest()
        {
            // try setting value with constructor
            var message = new UpdateVarsMessage
            {
                netId = 42,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };

            // serialize
            var writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            var fresh = new UpdateVarsMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }
    }
}
