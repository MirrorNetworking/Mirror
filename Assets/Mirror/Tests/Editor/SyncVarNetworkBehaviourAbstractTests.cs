using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    // SyncVarNetworkBehaviour for a abstract NetworkBehaviour
    public class SyncVarNetworkBehaviourAbstractTests : MirrorTest
    {
        NetworkIdentity serverIdentity;
        NetworkIdentity clientIdentity;
        NetworkBehaviour serverComponent;
        NetworkBehaviour clientComponent;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // need a spawned NetworkIdentity with a netId (we store by netId)
            // we need a valid NetworkBehaviour component.
            // the class is abstract so we need to use EmptyBehaviour and cast back
            CreateNetworkedAndSpawn(out _, out serverIdentity, out EmptyBehaviour inheritedServerComponent, out _, out clientIdentity, out EmptyBehaviour inheritedClientComponent);
            serverComponent = inheritedServerComponent;
            clientComponent = inheritedClientComponent;
            Assert.That(serverIdentity.netId, !Is.EqualTo(0));
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void Pack()
        {
            ulong packed = SyncVarNetworkBehaviour<NetworkBehaviour>.Pack(0xAABBCCDD, 0x12);
            Assert.That(packed, Is.EqualTo(0xAABBCCDD00000012));
        }

        [Test]
        public void Unpack()
        {
            SyncVarNetworkBehaviour<NetworkBehaviour>.Unpack(0xAABBCCDD00000012, out uint netId, out byte componentIndex);
            Assert.That(netId, Is.EqualTo(0xAABBCCDD));
            Assert.That(componentIndex, Is.EqualTo(0x12));
        }

        // make sure the NetworkBehaviour ctor works, even though base is uint
        [Test]
        public void Constructor_NetworkBehaviour()
        {
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        // make sure the NetworkBehaviour .Value works, even though base is uint
        [Test]
        public void Value_NetworkBehaviour()
        {
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = serverComponent;
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void PersistenceThroughDisappearance()
        {
            // field with NetworkBehaviour
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);

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
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
            // T = field implicit conversion should get .Value
            NetworkBehaviour value = field;
            Assert.That(value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVarNetworkBehaviour<NetworkBehaviour> field = serverComponent;
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }

        [Test]
        public void OperatorEquals()
        {
            // != null
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);

            // NOTE: this throws a compilation error, which is good!
            // we don't want users to do 'player.target == null'.
            // better to not compile than to fail silently.
            // Assert.That(field != null, Is.True);

            // different SyncVar<T>, same .Value
            SyncVarNetworkBehaviour<NetworkBehaviour> fieldSame = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
            Assert.That(field == fieldSame, Is.True);
            Assert.That(field != fieldSame, Is.False);

            // different SyncVar<T>, different .Value
            SyncVarNetworkBehaviour<NetworkBehaviour> fieldNull = new SyncVarNetworkBehaviour<NetworkBehaviour>(null);
            Assert.That(field == fieldNull, Is.False);
            Assert.That(field != fieldNull, Is.True);

            // same NetworkBehaviour<NetworkBehaviour>
            Assert.That(field == serverComponent, Is.True);
            Assert.That(field != serverComponent, Is.False);

            // different NetworkBehaviour<NetworkBehaviour>
            EmptyBehaviour other = new GameObject().AddComponent<EmptyBehaviour>();
            Assert.That(field == (NetworkBehaviour)other, Is.False); // NRE
            Assert.That(field != (NetworkBehaviour)other, Is.True);
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
                Assert.That(newValue, Is.EqualTo(serverComponent));
            }

            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(null);
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
            SyncVarNetworkBehaviour<NetworkBehaviour> fieldA = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
            SyncVarNetworkBehaviour<NetworkBehaviour> fieldB = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
            SyncVarNetworkBehaviour<NetworkBehaviour> fieldC = new SyncVarNetworkBehaviour<NetworkBehaviour>(null);
            Assert.That(fieldA.Equals(fieldB), Is.True);
            Assert.That(fieldA.Equals(fieldC), Is.False);
        }

        [Test]
        public void SerializeAllWritesNetIdAndComponentIndex()
        {
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
            NetworkWriter writer = new NetworkWriter();
            field.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadUInt(), Is.EqualTo(serverComponent.netId));
            Assert.That(reader.ReadByte(), Is.EqualTo(serverComponent.ComponentIndex));
        }

        [Test]
        public void SerializeDeltaWritesNetIdAndComponentIndex()
        {
            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(serverComponent);
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

            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(null);

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

            SyncVarNetworkBehaviour<NetworkBehaviour> field = new SyncVarNetworkBehaviour<NetworkBehaviour>(null);

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.OnDeserializeDelta(reader);
            Assert.That(field.Value, Is.EqualTo(serverComponent));
        }
    }
}
