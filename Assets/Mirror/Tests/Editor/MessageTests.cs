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
            CommandMessage message = new CommandMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
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
        public void ConnectMessageTest()
        {
            // try setting value with constructor
            ConnectMessage message = new ConnectMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ConnectMessage fresh = new ConnectMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void DisconnectMessageTest()
        {
            // try setting value with constructor
            DisconnectMessage message = new DisconnectMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            DisconnectMessage fresh = new DisconnectMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void ErrorMessageTest()
        {
            // try setting value with constructor
            ErrorMessage message = new ErrorMessage(42);

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ErrorMessage fresh = new ErrorMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.value, Is.EqualTo(message.value));
        }

        [Test]
        public void NetworkPingMessageTest()
        {
            // try setting value with constructor
            NetworkPingMessage message = new NetworkPingMessage(DateTime.Now.ToOADate());

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            NetworkPingMessage fresh = new NetworkPingMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.clientTime, Is.EqualTo(message.clientTime));
        }

        [Test]
        public void NetworkPongMessageTest()
        {
            // try setting value with constructor
            NetworkPongMessage message = new NetworkPongMessage
            {
                clientTime = DateTime.Now.ToOADate(),
                serverTime = DateTime.Now.ToOADate(),
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            NetworkPongMessage fresh = new NetworkPongMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.clientTime, Is.EqualTo(message.clientTime));
            Assert.That(fresh.serverTime, Is.EqualTo(message.serverTime));
        }

        [Test]
        public void NotReadyMessageTest()
        {
            // try setting value with constructor
            NotReadyMessage message = new NotReadyMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            NotReadyMessage fresh = new NotReadyMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void ObjectDestroyMessageTest()
        {
            // try setting value with constructor
            ObjectDestroyMessage message = new ObjectDestroyMessage
            {
                netId = 42,
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ObjectDestroyMessage fresh = new ObjectDestroyMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
        }

        [Test]
        public void ObjectHideMessageTest()
        {
            // try setting value with constructor
            ObjectHideMessage message = new ObjectHideMessage
            {
                netId = 42,
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ObjectHideMessage fresh = new ObjectHideMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
        }

        [Test]
        public void ObjectSpawnFinishedMessageTest()
        {
            // try setting value with constructor
            ObjectSpawnFinishedMessage message = new ObjectSpawnFinishedMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ObjectSpawnFinishedMessage fresh = new ObjectSpawnFinishedMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void ObjectSpawnStartedMessageTest()
        {
            // try setting value with constructor
            ObjectSpawnStartedMessage message = new ObjectSpawnStartedMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ObjectSpawnStartedMessage fresh = new ObjectSpawnStartedMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void ReadyMessageTest()
        {
            // try setting value with constructor
            ReadyMessage message = new ReadyMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            ReadyMessage fresh = new ReadyMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void RemovePlayerMessageTest()
        {
            // try setting value with constructor
            RemovePlayerMessage message = new RemovePlayerMessage();

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            RemovePlayerMessage fresh = new RemovePlayerMessage();
            fresh.Deserialize(new NetworkReader(writerData));
        }

        [Test]
        public void RpcMessageTest()
        {
            // try setting value with constructor
            RpcMessage message = new RpcMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
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
        public void SpawnMessageTest()
        {
            DoTest(0);
            DoTest(42);

            void DoTest(ulong testSceneId)
            {
                // try setting value with constructor
                SpawnMessage message = new SpawnMessage
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
                NetworkWriter writer = new NetworkWriter();
                message.Serialize(writer);
                byte[] writerData = writer.ToArray();

                // deserialize the same data - do we get the same result?
                SpawnMessage fresh = new SpawnMessage();
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
            SyncEventMessage message = new SyncEventMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
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

        [Test]
        public void UpdateVarsMessageTest()
        {
            // try setting value with constructor
            UpdateVarsMessage message = new UpdateVarsMessage
            {
                netId = 42,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };

            // serialize
            NetworkWriter writer = new NetworkWriter();
            message.Serialize(writer);
            byte[] writerData = writer.ToArray();

            // deserialize the same data - do we get the same result?
            UpdateVarsMessage fresh = new UpdateVarsMessage();
            fresh.Deserialize(new NetworkReader(writerData));
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }
    }
}
