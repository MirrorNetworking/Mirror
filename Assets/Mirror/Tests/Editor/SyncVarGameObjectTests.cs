using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class SyncVarGameObjectTests : MirrorTest
    {
        GameObject serverGO;
        GameObject clientGO;
        NetworkIdentity serverIdentity;
        NetworkIdentity clientIdentity;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // need a spawned GameObject with a netId (we store by netId)
            CreateNetworkedAndSpawn(out serverGO, out serverIdentity, out clientGO, out clientIdentity);
            Assert.That(serverIdentity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // make sure the GameObject ctor works, even though base is uint
        [Test]
        public void Constructor_GameObject()
        {
            SyncVarGameObject field = new SyncVarGameObject(serverGO);
            Assert.That(field.Value, Is.EqualTo(serverGO));
        }

        // make sure the GameObject .Value works, even though base is uint
        [Test]
        public void Value_GameObject()
        {
            SyncVarGameObject field = new SyncVarGameObject(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverGO;
            Assert.That(field.Value, Is.EqualTo(serverGO));
        }

        [Test]
        public void ImplicitTo()
        {
            SyncVarGameObject field = new SyncVarGameObject(serverGO);
            // T = field implicit conversion should get .Value
            GameObject value = field;
            Assert.That(value, Is.EqualTo(serverGO));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVarGameObject field = serverGO;
            Assert.That(field.Value, Is.EqualTo(serverGO));
        }

        [Test]
        public void OperatorEquals()
        {
            // != null
            SyncVarGameObject field = new SyncVarGameObject(serverGO);

            // NOTE: this throws a compilation error, which is good!
            // we don't want users to do 'player.target == null'.
            // better to not compile than to fail silently.
            // Assert.That(field != null, Is.True);

            // different SyncVar<T>, same .Value
            SyncVarGameObject fieldSame = new SyncVarGameObject(serverGO);
            Assert.That(field == fieldSame, Is.True);
            Assert.That(field != fieldSame, Is.False);

            // different SyncVar<T>, different .Value
            SyncVarGameObject fieldNull = new SyncVarGameObject(null);
            Assert.That(field == fieldNull, Is.False);
            Assert.That(field != fieldNull, Is.True);

            // same GameObject
            Assert.That(field == serverGO, Is.True);
            Assert.That(field != serverGO, Is.False);

            // different GameObject
            GameObject other = new GameObject("other");
            Assert.That(field == other, Is.False);
            Assert.That(field != other, Is.True);
        }

        // make sure the GameObject hook works, even though base is uint.
        [Test]
        public void Hook()
        {
            int called = 0;
            void OnChanged(GameObject oldValue, GameObject newValue)
            {
                ++called;
                Assert.That(oldValue, Is.Null);
                Assert.That(newValue, Is.EqualTo(serverGO));
            }

            SyncVarGameObject field = new SyncVarGameObject(null);
            field.Callback += OnChanged;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverGO;
            Assert.That(called, Is.EqualTo(1));
        }

        // SyncField should check .Value for equality.
        // two syncfields with same GameObject should be equal.
        [Test]
        public void EqualsTest()
        {
            SyncVarGameObject fieldA = new SyncVarGameObject(serverGO);
            SyncVarGameObject fieldB = new SyncVarGameObject(serverGO);
            SyncVarGameObject fieldC = new SyncVarGameObject(null);
            Assert.That(fieldA.Equals(fieldB), Is.True);
            Assert.That(fieldA.Equals(fieldC), Is.False);
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with identity
            SyncVarGameObject field = new SyncVarGameObject(serverGO);

            // remove from spawned, shouldn't be found anymore
            NetworkServer.spawned.Remove(serverIdentity.netId);
            Assert.That(field.Value, Is.EqualTo(null));

            // add to spawned again
            // add to spawned again, should be found again
            NetworkServer.spawned[serverIdentity.netId] = serverIdentity;
            Assert.That(field.Value, Is.EqualTo(serverGO));
        }

        [Test]
        public void SerializeAllWritesNetId()
        {
            SyncVarGameObject field = new SyncVarGameObject(serverGO);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(serverIdentity.netId));
        }

        [Test]
        public void SerializeDeltaWritesNetId()
        {
            SyncVarGameObject field = new SyncVarGameObject(serverGO);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeDelta(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(serverIdentity.netId));
        }

        [Test]
        public void DeserializeAllReadsNetId()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(serverIdentity.netId);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarGameObject field = new SyncVarGameObject(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeAll(reader);
            Assert.That(field.Value, Is.EqualTo(serverGO));
        }

        [Test]
        public void DeserializeDeltaReadsNetId()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(serverIdentity.netId);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarGameObject field = new SyncVarGameObject(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(serverGO));
        }
    }
}
