using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncFieldNetworkBehaviourTests : MirrorTest
    {
        NetworkIdentity identity;
        EmptyBehaviour component;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();

            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out identity, out component);
            Assert.That(identity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void Pack()
        {
            ulong packed = SyncFieldNetworkBehaviour.Pack(0xAABBCCDD, 0x12);
            Assert.That(packed, Is.EqualTo(0xAABBCCDD00000012));
        }

        [Test]
        public void Unpack()
        {
            SyncFieldNetworkBehaviour.Unpack(0xAABBCCDD00000012, out uint netId, out byte componentIndex);
            Assert.That(netId, Is.EqualTo(0xAABBCCDD));
            Assert.That(componentIndex, Is.EqualTo(0x12));
        }

        // make sure the NetworkBehaviour ctor works, even though base is uint
        [Test]
        public void Constructor_NetworkBehaviour()
        {
            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(component);
            Assert.That(field.Value, Is.EqualTo(component));
        }

        // make sure the NetworkBehaviour .Value works, even though base is uint
        [Test]
        public void Value_NetworkBehaviour()
        {
            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(null);
            field.Value = component;
            Assert.That(field.Value, Is.EqualTo(component));
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with NetworkBehaviour
            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(component);

            // remove from spawned, shouldn't be found anymore
            NetworkServer.spawned.Remove(identity.netId);
            Assert.That(field.Value, Is.EqualTo(null));

            // add to spawned again, should be found again
            NetworkServer.spawned[identity.netId] = identity;
            Assert.That(field.Value, Is.EqualTo(component));
        }

        // make sure the NetworkBehaviour hook works, even though base is uint.
        [Test]
        public void Hook()
        {
            int called = 0;
            void OnChanged(NetworkBehaviour oldValue, NetworkBehaviour newValue)
            {
                ++called;
                Assert.That(oldValue, Is.Null);
                Assert.That(newValue, Is.EqualTo(component));
            }

            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(null, OnChanged);
            field.Value = component;
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void SerializeAllWritesNetIdAndComponentIndex()
        {
            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(component);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(component.netId));
            Assert.That(reader.ReadByte(), Is.EqualTo(component.ComponentIndex));
        }

        [Test]
        public void SerializeDeltaWritesNetIdAndComponentIndex()
        {
            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(component);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeDelta(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(component.netId));
            Assert.That(reader.ReadByte(), Is.EqualTo(component.ComponentIndex));
        }

        [Test]
        public void DeserializeAllReadsNetIdAndComponentIndex()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(component.netId);
            writer.WriteByte((byte)component.ComponentIndex);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(null);
            field.OnDeserializeAll(reader);
            Assert.That(field.Value, Is.EqualTo(component));
        }

        [Test]
        public void DeserializeDeltaReadsNetIdAndComponentIndex()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(component.netId);
            writer.WriteByte((byte)component.ComponentIndex);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncFieldNetworkBehaviour field = new SyncFieldNetworkBehaviour(null);
            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(component));
        }
    }
}
