using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class SyncVarGameObjectTests : MirrorTest
    {
        GameObject go;
        NetworkIdentity identity;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();

            // need a spawned GameObject with a netId (we store by netId)
            CreateNetworkedAndSpawn(out go, out identity);
            Assert.That(identity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // make sure the GameObject ctor works, even though base is uint
        [Test]
        public void Constructor_GameObject()
        {
            SyncVarGameObject field = new SyncVarGameObject(go);
            Assert.That(field.Value, Is.EqualTo(go));
        }

        // make sure the GameObject .Value works, even though base is uint
        [Test]
        public void Value_GameObject()
        {
            SyncVarGameObject field = new SyncVarGameObject(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = go;
            Assert.That(field.Value, Is.EqualTo(go));
        }

        [Test]
        public void ImplicitTo()
        {
            SyncVarGameObject field = new SyncVarGameObject(go);
            // T = field implicit conversion should get .Value
            GameObject value = field;
            Assert.That(value, Is.EqualTo(go));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVarGameObject field = go;
            Assert.That(field.Value, Is.EqualTo(go));
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
                Assert.That(newValue, Is.EqualTo(go));
            }

            SyncVarGameObject field = new SyncVarGameObject(null, OnChanged);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = go;
            Assert.That(called, Is.EqualTo(1));
        }

        // SyncField should check .Value for equality.
        // two syncfields with same GameObject should be equal.
        [Test]
        public void EqualsTest()
        {
            SyncVarGameObject fieldA = new SyncVarGameObject(go);
            SyncVarGameObject fieldB = new SyncVarGameObject(go);
            SyncVarGameObject fieldC = new SyncVarGameObject(null);
            Assert.That(fieldA.Equals(fieldB), Is.True);
            Assert.That(fieldA.Equals(fieldC), Is.False);
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with identity
            SyncVarGameObject field = new SyncVarGameObject(go);

            // remove from spawned, shouldn't be found anymore
            NetworkServer.spawned.Remove(identity.netId);
            Assert.That(field.Value, Is.EqualTo(null));

            // add to spawned again
            // add to spawned again, should be found again
            NetworkServer.spawned[identity.netId] = identity;
            Assert.That(field.Value, Is.EqualTo(go));
        }

        [Test]
        public void SerializeAllWritesNetId()
        {
            SyncVarGameObject field = new SyncVarGameObject(go);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(identity.netId));
        }

        [Test]
        public void SerializeDeltaWritesNetId()
        {
            SyncVarGameObject field = new SyncVarGameObject(go);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeDelta(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(identity.netId));
        }

        [Test]
        public void DeserializeAllReadsNetId()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(identity.netId);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarGameObject field = new SyncVarGameObject(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeAll(reader);
            Assert.That(field.Value, Is.EqualTo(go));
        }

        [Test]
        public void DeserializeDeltaReadsNetId()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(identity.netId);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarGameObject field = new SyncVarGameObject(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(go));
        }
    }
}
