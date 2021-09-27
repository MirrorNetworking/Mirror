using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncVarNetworkIdentityTests : MirrorTest
    {
        NetworkIdentity identity;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();

            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out identity);
            Assert.That(identity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // make sure the NetworkIdentity ctor works, even though base is uint
        [Test]
        public void Constructor_NetworkIdentity()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(identity);
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        // make sure the NetworkIdentity .Value works, even though base is uint
        [Test]
        public void Value_NetworkIdentity()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = identity;
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with identity
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(identity);

            // remove from spawned, shouldn't be found anymore
            NetworkServer.spawned.Remove(identity.netId);
            Assert.That(field.Value, Is.EqualTo(null));

            // add to spawned again, should be found again
            NetworkServer.spawned[identity.netId] = identity;
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        [Test]
        public void ImplicitTo()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(identity);
            // T = field implicit conversion should get .Value
            NetworkIdentity value = field;
            Assert.That(value, Is.EqualTo(identity));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVarNetworkIdentity field = identity;
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        // make sure the NetworkIdentity hook works, even though base is uint.
        [Test]
        public void Hook()
        {
            int called = 0;
            void OnChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
            {
                ++called;
                Assert.That(oldValue, Is.Null);
                Assert.That(newValue, Is.EqualTo(identity));
            }

            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null, OnChanged);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = identity;
            Assert.That(called, Is.EqualTo(1));
        }

        // SyncField should check .Value for equality.
        // two syncfields with same NetworkIdentity should be equal.
        [Test]
        public void EqualsTest()
        {
            SyncVarNetworkIdentity fieldA = new SyncVarNetworkIdentity(identity);
            SyncVarNetworkIdentity fieldB = new SyncVarNetworkIdentity(identity);
            SyncVarNetworkIdentity fieldC = new SyncVarNetworkIdentity(null);
            Assert.That(fieldA.Equals(fieldB), Is.True);
            Assert.That(fieldA.Equals(fieldC), Is.False);
        }

        [Test]
        public void SerializeAllWritesNetId()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(identity);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(identity.netId));
        }

        [Test]
        public void SerializeDeltaWritesNetId()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(identity);
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

            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeAll(reader);
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        [Test]
        public void DeserializeDeltaReadsNetId()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(identity.netId);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(identity));
        }
    }
}
