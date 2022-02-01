using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class SyncVarNetworkIdentityTests : MirrorTest
    {
        NetworkIdentity serverIdentity;
        NetworkIdentity clientIdentity;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out serverIdentity, out _, out clientIdentity);
            Assert.That(serverIdentity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // make sure the NetworkIdentity ctor works, even though base is uint
        [Test]
        public void Constructor_NetworkIdentity()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(serverIdentity);
            Assert.That(field.Value, Is.EqualTo(serverIdentity));
        }

        // make sure the NetworkIdentity .Value works, even though base is uint
        [Test]
        public void Value_NetworkIdentity()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverIdentity;
            Assert.That(field.Value, Is.EqualTo(serverIdentity));
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with identity
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(serverIdentity);

            // remove from spawned, shouldn't be found anymore
            NetworkServer.spawned.Remove(serverIdentity.netId);
            Assert.That(field.Value, Is.EqualTo(null));

            // add to spawned again, should be found again
            NetworkServer.spawned[serverIdentity.netId] = serverIdentity;
            Assert.That(field.Value, Is.EqualTo(serverIdentity));
        }

        [Test]
        public void ImplicitTo()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(serverIdentity);
            // T = field implicit conversion should get .Value
            NetworkIdentity value = field;
            Assert.That(value, Is.EqualTo(serverIdentity));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVarNetworkIdentity field = serverIdentity;
            Assert.That(field.Value, Is.EqualTo(serverIdentity));
        }

        [Test]
        public void OperatorEquals()
        {
            // != null
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(serverIdentity);

            // NOTE: this throws a compilation error, which is good!
            // we don't want users to do 'player.target == null'.
            // better to not compile than to fail silently.
            // Assert.That(field != null, Is.True);

            // different SyncVar<T>, same .Value
            SyncVarNetworkIdentity fieldSame = new SyncVarNetworkIdentity(serverIdentity);
            Assert.That(field == fieldSame, Is.True);
            Assert.That(field != fieldSame, Is.False);

            // different SyncVar<T>, different .Value
            SyncVarNetworkIdentity fieldNull = new SyncVarNetworkIdentity(null);
            Assert.That(field == fieldNull, Is.False);
            Assert.That(field != fieldNull, Is.True);

            // same NetworkIdentity
            Assert.That(field == serverIdentity, Is.True);
            Assert.That(field != serverIdentity, Is.False);

            // different NetworkIdentity
            NetworkIdentity other = new GameObject().AddComponent<NetworkIdentity>();
            Assert.That(field == other, Is.False);
            Assert.That(field != other, Is.True);
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
                Assert.That(newValue, Is.EqualTo(serverIdentity));
            }

            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);
            field.Callback += OnChanged;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverIdentity;
            Assert.That(called, Is.EqualTo(1));
        }

        // SyncField should check .Value for equality.
        // two syncfields with same NetworkIdentity should be equal.
        [Test]
        public void EqualsTest()
        {
            SyncVarNetworkIdentity fieldA = new SyncVarNetworkIdentity(serverIdentity);
            SyncVarNetworkIdentity fieldB = new SyncVarNetworkIdentity(serverIdentity);
            SyncVarNetworkIdentity fieldC = new SyncVarNetworkIdentity(null);
            Assert.That(fieldA.Equals(fieldB), Is.True);
            Assert.That(fieldA.Equals(fieldC), Is.False);
        }

        [Test]
        public void SerializeAllWritesNetId()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(serverIdentity);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(serverIdentity.netId));
        }

        [Test]
        public void SerializeDeltaWritesNetId()
        {
            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(serverIdentity);
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

            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeAll(reader);
            Assert.That(field.Value, Is.EqualTo(serverIdentity));
        }

        [Test]
        public void DeserializeDeltaReadsNetId()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(serverIdentity.netId);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarNetworkIdentity field = new SyncVarNetworkIdentity(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(serverIdentity));
        }
    }
}
