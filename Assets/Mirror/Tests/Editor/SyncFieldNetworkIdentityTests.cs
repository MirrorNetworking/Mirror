using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncFieldNetworkIdentityTests : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // need a connected client & server so we can have spawned identities
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // make sure the NetworkIdentity ctor works, even though base is uint
        [Test]
        public void Constructor_NetworkIdentity()
        {
            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity);
            SyncFieldNetworkIdentity field = new SyncFieldNetworkIdentity(identity);
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        // make sure the NetworkIdentity .Value works, even though base is uint
        [Test]
        public void Value_NetworkIdentity()
        {
            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity);

            SyncFieldNetworkIdentity field = new SyncFieldNetworkIdentity(null);
            field.Value = identity;
            Assert.That(field.Value, Is.EqualTo(identity));
        }

        // make sure the NetworkIdentity hook works, even though base is uint.
        [Test]
        public void Hook()
        {
            // need a spawned NetworkIdentity with a netId (we store by netId)
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity);

            int called = 0;
            void OnChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
            {
                ++called;
                Assert.That(oldValue, Is.Null);
                Assert.That(newValue, Is.EqualTo(identity));
            }

            SyncFieldNetworkIdentity field = new SyncFieldNetworkIdentity(null, OnChanged);
            field.Value = identity;
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
