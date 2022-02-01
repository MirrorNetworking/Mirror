using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    // SyncVarNetworkBehaviour for a class that inherits from NetworkBehaviour
    public class SyncVarNetworkBehaviourInheritedTests : MirrorTest
    {
        NetworkIdentity serverIdentity;
        NetworkIdentity clientIdentity;
        EmptyBehaviour serverComponent;
        EmptyBehaviour clientComponent;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out serverIdentity, out serverComponent, out _, out clientIdentity, out clientComponent);
            Assert.That(serverIdentity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void Pack()
        {
            ulong packed = SyncVarNetworkBehaviour<EmptyBehaviour>.Pack(0xAABBCCDD, 0x12);
            Assert.That(packed, Is.EqualTo(0xAABBCCDD00000012));
        }

        [Test]
        public void Unpack()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour>.Unpack(0xAABBCCDD00000012, out uint netId, out byte componentIndex);
            Assert.That(netId, Is.EqualTo(0xAABBCCDD));
            Assert.That(componentIndex, Is.EqualTo(0x12));
        }

        // make sure the NetworkBehaviour ctor works, even though base is uint
        [Test]
        public void Constructor_NetworkBehaviour()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        // make sure the NetworkBehaviour .Value works, even though base is uint
        [Test]
        public void Value_NetworkBehaviour()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverComponent;
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with NetworkBehaviour
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);

            // remove from spawned, shouldn't be found anymore
            NetworkServer.spawned.Remove(serverIdentity.netId);
            Assert.That(field.Value, Is.EqualTo(null));

            // add to spawned again, should be found again
            NetworkServer.spawned[serverIdentity.netId] = serverIdentity;
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void ImplicitTo()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            // T = field implicit conversion should get .Value
            EmptyBehaviour value = field;
            Assert.That(value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVarNetworkBehaviour<EmptyBehaviour> field = serverComponent;
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void OperatorEquals()
        {
            // != null
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);

            // NOTE: this throws a compilation error, which is good!
            // we don't want users to do 'player.target == null'.
            // better to not compile than to fail silently.
            // Assert.That(field != null, Is.True);

            // different SyncVar<T>, same .Value
            SyncVarNetworkBehaviour<EmptyBehaviour> fieldSame = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            Assert.That(field == fieldSame, Is.True);
            Assert.That(field != fieldSame, Is.False);

            // different SyncVar<T>, different .Value
            SyncVarNetworkBehaviour<EmptyBehaviour> fieldNull = new SyncVarNetworkBehaviour<EmptyBehaviour>(null);
            Assert.That(field == fieldNull, Is.False);
            Assert.That(field != fieldNull, Is.True);

            // same NetworkBehaviour<EmptyBehaviour>
            Assert.That(field == serverComponent, Is.True);
            Assert.That(field != serverComponent, Is.False);

            // different NetworkBehaviour<EmptyBehaviour>
            CreateNetworked(out GameObject otherGo, out NetworkIdentity otherIdentity, out EmptyBehaviour otherComp);
            Assert.That(field == otherComp, Is.False);
            Assert.That(field != otherComp, Is.True);
        }

        // make sure the NetworkBehaviour hook works, even though base is uint.
        [Test]
        public void Hook()
        {
            int called = 0;
            void OnChanged(EmptyBehaviour oldValue, EmptyBehaviour newValue)
            {
                ++called;
                Assert.That(oldValue, Is.Null);
                Assert.That(newValue, Is.EqualTo(serverComponent));
            }

            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(null);
            field.Callback += OnChanged;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverComponent;
            Assert.That(called, Is.EqualTo(1));
        }

        // SyncField should check .Value for equality.
        // two syncfields with same NetworkBehaviour should be equal.
        [Test]
        public void EqualsTest()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour> fieldA = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            SyncVarNetworkBehaviour<EmptyBehaviour> fieldB = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            SyncVarNetworkBehaviour<EmptyBehaviour> fieldC = new SyncVarNetworkBehaviour<EmptyBehaviour>(null);
            Assert.That(fieldA.Equals(fieldB), Is.True);
            Assert.That(fieldA.Equals(fieldC), Is.False);
        }

        [Test]
        public void SerializeAllWritesNetIdAndComponentIndex()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(serverComponent.netId));
            Assert.That(reader.ReadByte(), Is.EqualTo(serverComponent.ComponentIndex));
        }

        [Test]
        public void SerializeDeltaWritesNetIdAndComponentIndex()
        {
            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(serverComponent);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeDelta(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(serverComponent.netId));
            Assert.That(reader.ReadByte(), Is.EqualTo(serverComponent.ComponentIndex));
        }

        [Test]
        public void DeserializeAllReadsNetIdAndComponentIndex()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(serverComponent.netId);
            writer.WriteByte((byte)serverComponent.ComponentIndex);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeAll(reader);
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void DeserializeDeltaReadsNetIdAndComponentIndex()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUInt(serverComponent.netId);
            writer.WriteByte((byte)serverComponent.ComponentIndex);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            SyncVarNetworkBehaviour<EmptyBehaviour> field = new SyncVarNetworkBehaviour<EmptyBehaviour>(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }
    }
}
