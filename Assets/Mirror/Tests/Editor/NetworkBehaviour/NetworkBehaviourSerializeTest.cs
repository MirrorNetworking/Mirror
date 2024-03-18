using NUnit.Framework;
using UnityEngine;

// Note: Weaver doesn't run on nested class so use namespace to group classes instead
namespace Mirror.Tests.NetworkBehaviours
{
    #region No OnSerialize/OnDeserialize override
    abstract class AbstractBehaviour : NetworkBehaviour
    {
        public readonly SyncList<bool> syncListInAbstract = new SyncList<bool>();

        [SyncVar]
        public int SyncFieldInAbstract;
    }

    class BehaviourWithSyncVar : NetworkBehaviour
    {
        public readonly SyncList<bool> syncList = new SyncList<bool>();

        [SyncVar]
        public int SyncField;
    }

    class OverrideBehaviourFromSyncVar : AbstractBehaviour {}

    class OverrideBehaviourWithSyncVarFromSyncVar : AbstractBehaviour
    {
        public readonly SyncList<bool> syncListInOverride = new SyncList<bool>();

        [SyncVar]
        public int SyncFieldInOverride;
    }

    class MiddleClass : AbstractBehaviour
    {
        // class with no sync var
    }

    class SubClass : MiddleClass
    {
        // class with sync var
        // this is to make sure that override works correctly if base class doesn't have sync vars
        [SyncVar]
        public Vector3 anotherSyncField;
    }

    class MiddleClassWithSyncVar : AbstractBehaviour
    {
        // class with sync var
        [SyncVar]
        public string syncFieldInMiddle;
    }

    class SubClassFromSyncVar : MiddleClassWithSyncVar
    {
        // class with sync var
        // this is to make sure that override works correctly if base class doesn't have sync vars
        [SyncVar]
        public Vector3 syncFieldInSub;
    }
    #endregion

    #region OnSerialize/OnDeserialize override

    class BehaviourWithSyncVarWithOnSerialize : NetworkBehaviour
    {
        public readonly SyncList<bool> syncList = new SyncList<bool>();

        [SyncVar]
        public int SyncField;

        public float customSerializeField;

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteFloat(customSerializeField);
            base.OnSerialize(writer, initialState);
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            customSerializeField = reader.ReadFloat();
            base.OnDeserialize(reader, initialState);
        }
    }

    class OverrideBehaviourFromSyncVarWithOnSerialize : AbstractBehaviour
    {
        public float customSerializeField;

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteFloat(customSerializeField);
            base.OnSerialize(writer, initialState);
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            customSerializeField = reader.ReadFloat();
            base.OnDeserialize(reader, initialState);
        }
    }

    class OverrideBehaviourWithSyncVarFromSyncVarWithOnSerialize : AbstractBehaviour
    {
        public readonly SyncList<bool> syncListInOverride = new SyncList<bool>();

        [SyncVar]
        public int SyncFieldInOverride;

        public float customSerializeField;

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteFloat(customSerializeField);
            base.OnSerialize(writer, initialState);
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            customSerializeField = reader.ReadFloat();
            base.OnDeserialize(reader, initialState);
        }
    }
    #endregion

    public class NetworkBehaviourSerializeTest : MirrorEditModeTest
    {
        public static void SyncNetworkBehaviour(NetworkBehaviour source, NetworkBehaviour target, bool initialState)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                source.OnSerialize(writer, initialState);

                using (NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                {
                    target.OnDeserialize(reader, initialState);
                }
            }
        }

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
            CreateNetworkedAndSpawn(out _, out _, out BehaviourWithSyncVar source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out BehaviourWithSyncVar target,
                                    out _, out _, out _);

            source.SyncField = 10;
            source.syncList.Add(true);

            SyncNetworkBehaviour(source, target, initialState);

            Assert.That(target.SyncField, Is.EqualTo(10));
            Assert.That(target.syncList.Count, Is.EqualTo(1));
            Assert.That(target.syncList[0], Is.True);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OverrideBehaviourFromSyncVarTest(bool initialState)
        {
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourFromSyncVar source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourFromSyncVar target,
                                    out _, out _, out _);

            source.SyncFieldInAbstract = 12;
            source.syncListInAbstract.Add(true);
            source.syncListInAbstract.Add(false);

            SyncNetworkBehaviour(source, target, initialState);

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
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourWithSyncVarFromSyncVar source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourWithSyncVarFromSyncVar target,
                                    out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.SyncFieldInOverride = 52;
            source.syncListInOverride.Add(false);
            source.syncListInOverride.Add(true);

            SyncNetworkBehaviour(source, target, initialState);

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
            CreateNetworkedAndSpawn(out _, out _, out SubClass source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out SubClass target,
                                    out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.anotherSyncField = new Vector3(40, 20, 10);

            SyncNetworkBehaviour(source, target, initialState);

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
            CreateNetworkedAndSpawn(out _, out _, out SubClassFromSyncVar source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out SubClassFromSyncVar target,
                                    out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.syncFieldInMiddle = "Hello World!";
            source.syncFieldInSub = new Vector3(40, 20, 10);

            SyncNetworkBehaviour(source, target, initialState);

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
            CreateNetworkedAndSpawn(out _, out _, out BehaviourWithSyncVarWithOnSerialize source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out BehaviourWithSyncVarWithOnSerialize target,
                                    out _, out _, out _);

            source.SyncField = 10;
            source.syncList.Add(true);

            source.customSerializeField = 20.5f;

            SyncNetworkBehaviour(source, target, initialState);

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
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourFromSyncVarWithOnSerialize source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourFromSyncVarWithOnSerialize target,
                                    out _, out _, out _);

            source.SyncFieldInAbstract = 12;
            source.syncListInAbstract.Add(true);
            source.syncListInAbstract.Add(false);

            source.customSerializeField = 20.5f;

            SyncNetworkBehaviour(source, target, initialState);

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
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourWithSyncVarFromSyncVarWithOnSerialize source,
                                    out _, out _, out _);
            CreateNetworkedAndSpawn(out _, out _, out OverrideBehaviourWithSyncVarFromSyncVarWithOnSerialize target,
                                    out _, out _, out _);

            source.SyncFieldInAbstract = 10;
            source.syncListInAbstract.Add(true);

            source.SyncFieldInOverride = 52;
            source.syncListInOverride.Add(false);
            source.syncListInOverride.Add(true);

            source.customSerializeField = 20.5f;

            SyncNetworkBehaviour(source, target, initialState);

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
