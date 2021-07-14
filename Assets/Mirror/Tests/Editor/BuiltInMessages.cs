using System;
using NUnit.Framework;

namespace Mirror.Tests.MessageTests
{
    [TestFixture]
    public class BuiltInMessages
    {
        [Test]
        public void CommandMessage()
        {
            // try setting value with constructor
            CommandMessage message = new CommandMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            // deserialize the same data - do we get the same result?
            CommandMessage fresh = MessagePackingTest.UnpackFromByteArray<CommandMessage>(arr);
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.componentIndex, Is.EqualTo(message.componentIndex));
            Assert.That(fresh.functionHash, Is.EqualTo(message.functionHash));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }

        [Test]
        public void NetworkPingMessage()
        {
            NetworkPingMessage message = new NetworkPingMessage(DateTime.Now.ToOADate());
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            NetworkPingMessage fresh = MessagePackingTest.UnpackFromByteArray<NetworkPingMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void NetworkPongMessage()
        {
            NetworkPongMessage message = new NetworkPongMessage
            {
                clientTime = DateTime.Now.ToOADate(),
                serverTime = DateTime.Now.ToOADate(),
            };
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            NetworkPongMessage fresh = MessagePackingTest.UnpackFromByteArray<NetworkPongMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void NotReadyMessage()
        {
            NotReadyMessage message = new NotReadyMessage();
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            NotReadyMessage fresh = MessagePackingTest.UnpackFromByteArray<NotReadyMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void ObjectDestroyMessage()
        {
            // try setting value with constructor
            ObjectDestroyMessage message = new ObjectDestroyMessage{ netId = 42 };
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            ObjectDestroyMessage fresh = MessagePackingTest.UnpackFromByteArray<ObjectDestroyMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void ObjectHideMessage()
        {
            // try setting value with constructor
            ObjectHideMessage message = new ObjectHideMessage{ netId = 42};
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            ObjectHideMessage fresh = MessagePackingTest.UnpackFromByteArray<ObjectHideMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void ObjectSpawnFinishedMessage()
        {
            ObjectSpawnFinishedMessage message = new ObjectSpawnFinishedMessage();
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            ObjectSpawnFinishedMessage fresh = MessagePackingTest.UnpackFromByteArray<ObjectSpawnFinishedMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void ObjectSpawnStartedMessage()
        {
            ObjectSpawnStartedMessage message = new ObjectSpawnStartedMessage();
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            ObjectSpawnStartedMessage fresh = MessagePackingTest.UnpackFromByteArray<ObjectSpawnStartedMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void ReadyMessage()
        {
            ReadyMessage message = new ReadyMessage();
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            ReadyMessage fresh = MessagePackingTest.UnpackFromByteArray<ReadyMessage>(arr);
            Assert.That(fresh, Is.EqualTo(message));
        }

        [Test]
        public void RpcMessage()
        {
            // try setting value with constructor
            RpcMessage message = new RpcMessage
            {
                netId = 42,
                componentIndex = 4,
                functionHash = 0xABCDEF,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };
            byte[] arr = MessagePackingTest.PackToByteArray(message);

            RpcMessage fresh = MessagePackingTest.UnpackFromByteArray<RpcMessage>(arr);
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.componentIndex, Is.EqualTo(message.componentIndex));
            Assert.That(fresh.functionHash, Is.EqualTo(message.functionHash));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }

        [Test]
        public void SpawnMessage()
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
                byte[] arr = MessagePackingTest.PackToByteArray(message);
                SpawnMessage fresh = MessagePackingTest.UnpackFromByteArray<SpawnMessage>(arr);
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
        public void UpdateVarsMessage()
        {
            // try setting value with constructor
            EntityStateMessage message = new EntityStateMessage
            {
                netId = 42,
                payload = new ArraySegment<byte>(new byte[] { 0x01, 0x02 })
            };
            byte[] arr = MessagePackingTest.PackToByteArray(message);
            EntityStateMessage fresh = MessagePackingTest.UnpackFromByteArray<EntityStateMessage>(arr);
            Assert.That(fresh.netId, Is.EqualTo(message.netId));
            Assert.That(fresh.payload.Count, Is.EqualTo(message.payload.Count));
            for (int i = 0; i < fresh.payload.Count; ++i)
                Assert.That(fresh.payload.Array[fresh.payload.Offset + i],
                    Is.EqualTo(message.payload.Array[message.payload.Offset + i]));
        }
    }
}
