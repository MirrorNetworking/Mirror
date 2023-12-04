// test coverage for child NetworkBehaviour components
using NUnit.Framework;
using UnityEngine;

// Note: Weaver doesn't run on nested class so use namespace to group classes instead
namespace Mirror.Tests.NetworkBehaviours
{
    public class NetworkBehaviourChildSerializeTest : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // SyncLists are only set dirty while owner has observers.
            // need a connection.
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void BehaviourWithSyncVarTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out BehaviourWithSyncVar source,
                                         out _, out _,  out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out BehaviourWithSyncVar target,
                                         out _, out _, out _, out _);

            source.SyncField = 10;
            source.syncList.Add(true);

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncField, Is.EqualTo(10));
            Assert.That(target.syncList.Count, Is.EqualTo(1));
            Assert.That(target.syncList[0], Is.True);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OverrideBehaviourFromSyncVarTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourFromSyncVar source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourFromSyncVar target,
                                         out _, out _, out _, out _);

            source.SyncFieldInAbstract = 12;
            source.syncListInAbstract.Add(true);
            source.syncListInAbstract.Add(false);

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncFieldInAbstract, Is.EqualTo(12));
            Assert.That(target.syncListInAbstract.Count, Is.EqualTo(2));
            Assert.That(target.syncListInAbstract[0], Is.True);
            Assert.That(target.syncListInAbstract[1], Is.False);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OverrideBehaviourWithSyncVarFromSyncVarTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourWithSyncVarFromSyncVar source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourWithSyncVarFromSyncVar target,
                                         out _, out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.SyncFieldInOverride = 52;
            source.syncListInOverride.Add(false);
            source.syncListInOverride.Add(true);

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncFieldInAbstract, Is.EqualTo(10));
            Assert.That(target.syncListInAbstract.Count, Is.EqualTo(1));
            Assert.That(target.syncListInAbstract[0], Is.True);


            Assert.That(target.SyncFieldInOverride, Is.EqualTo(52));
            Assert.That(target.syncListInOverride.Count, Is.EqualTo(2));
            Assert.That(target.syncListInOverride[0], Is.False);
            Assert.That(target.syncListInOverride[1], Is.True);
        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SubClassTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out SubClass source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out SubClass target,
                                         out _, out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.anotherSyncField = new Vector3(40, 20, 10);

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncFieldInAbstract, Is.EqualTo(10));
            Assert.That(target.syncListInAbstract.Count, Is.EqualTo(1));
            Assert.That(target.syncListInAbstract[0], Is.True);

            Assert.That(target.anotherSyncField, Is.EqualTo(new Vector3(40, 20, 10)));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SubClassFromSyncVarTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out SubClassFromSyncVar source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out SubClassFromSyncVar target,
                                         out _, out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.syncFieldInMiddle = "Hello World!";
            source.syncFieldInSub = new Vector3(40, 20, 10);

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncFieldInAbstract, Is.EqualTo(10));
            Assert.That(target.syncListInAbstract.Count, Is.EqualTo(1));
            Assert.That(target.syncListInAbstract[0], Is.True);

            Assert.That(target.syncFieldInMiddle, Is.EqualTo("Hello World!"));
            Assert.That(target.syncFieldInSub, Is.EqualTo(new Vector3(40, 20, 10)));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void BehaviourWithSyncVarWithOnSerializeTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out BehaviourWithSyncVarWithOnSerialize source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out BehaviourWithSyncVarWithOnSerialize target,
                                         out _, out _, out _, out _);

            source.SyncField = 10;
            source.syncList.Add(true);

            source.customSerializeField = 20.5f;

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncField, Is.EqualTo(10));
            Assert.That(target.syncList.Count, Is.EqualTo(1));
            Assert.That(target.syncList[0], Is.True);

            Assert.That(target.customSerializeField, Is.EqualTo(20.5f));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OverrideBehaviourFromSyncVarWithOnSerializeTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourFromSyncVarWithOnSerialize source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourFromSyncVarWithOnSerialize target,
                                         out _, out _, out _, out _);

            source.SyncFieldInAbstract = 12;
            source.syncListInAbstract.Add(true);
            source.syncListInAbstract.Add(false);

            source.customSerializeField = 20.5f;

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncFieldInAbstract, Is.EqualTo(12));
            Assert.That(target.syncListInAbstract.Count, Is.EqualTo(2));
            Assert.That(target.syncListInAbstract[0], Is.True);
            Assert.That(target.syncListInAbstract[1], Is.False);

            Assert.That(target.customSerializeField, Is.EqualTo(20.5f));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OverrideBehaviourWithSyncVarFromSyncVarWithOnSerializeTest(bool initialState)
        {
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourWithSyncVarFromSyncVarWithOnSerialize source,
                                         out _, out _, out _, out _);
            CreateNetworkedChildAndSpawn(out _, out _, out _, out OverrideBehaviourWithSyncVarFromSyncVarWithOnSerialize target,
                                         out _, out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.SyncFieldInOverride = 52;
            source.syncListInOverride.Add(false);
            source.syncListInOverride.Add(true);

            source.customSerializeField = 20.5f;

            NetworkBehaviourSerializeTest.SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncFieldInAbstract, Is.EqualTo(10));
            Assert.That(target.syncListInAbstract.Count, Is.EqualTo(1));
            Assert.That(target.syncListInAbstract[0], Is.True);


            Assert.That(target.SyncFieldInOverride, Is.EqualTo(52));
            Assert.That(target.syncListInOverride.Count, Is.EqualTo(2));
            Assert.That(target.syncListInOverride[0], Is.False);
            Assert.That(target.syncListInOverride[1], Is.True);

            Assert.That(target.customSerializeField, Is.EqualTo(20.5f));
        }
    }
}
