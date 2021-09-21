using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class SyncFieldGameObjectTests : MirrorTest
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
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // make sure the GameObject ctor works, even though base is uint
        [Test]
        public void Constructor_GameObject()
        {
            SyncFieldGameObject field = new SyncFieldGameObject(go);
            Assert.That(field.Value, Is.EqualTo(go));
        }

        // make sure the GameObject .Value works, even though base is uint
        [Test]
        public void Value_GameObject()
        {
            SyncFieldGameObject field = new SyncFieldGameObject(null);
            field.Value = go;
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

            SyncFieldGameObject field = new SyncFieldGameObject(null, OnChanged);
            field.Value = go;
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
