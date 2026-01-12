using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    public class SyncSetActionTest_HostMode : MirrorTest
    {
        DistanceInterestManagement aoi;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            aoi = holder.AddComponent<DistanceInterestManagement>();
            aoi.visRange = 10;
            NetworkServer.aoi = aoi;

            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkServer.aoi = null;
            base.TearDown();
        }

        [Test]
        public void HostMode_ActionNotCalledForOutOfRangeObject()
        {
            CreateNetworkedAndSpawn(out _, out _, out SyncSetHostModeBehaviour comp);
            comp.transform.position = Vector3.right * (aoi.visRange + 1);
            NetworkServer.RebuildObservers(comp.netIdentity, true);

            int actionCallCount = 0;
            comp.set.OnAdd += (item) => actionCallCount++;

            comp.set.Add("test");

            Assert.That(actionCallCount, Is.EqualTo(0));
        }

        [Test]
        public void HostMode_ActionsCalledWhenEnteringRange()
        {
            CreateNetworkedAndSpawn(out _, out _, out SyncSetHostModeBehaviour comp);
            comp.transform.position = Vector3.right * (aoi.visRange + 1);
            NetworkServer.RebuildObservers(comp.netIdentity, true);

            comp.set.Add("first");
            comp.set.Add("second");

            int addCallCount = 0;
            comp.set.OnAdd += (item) => addCallCount++;

            comp.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(comp.netIdentity, false);
            ProcessMessages();

            Assert.That(addCallCount, Is.EqualTo(2));
        }
    }

    public class SyncSetHostModeBehaviour : NetworkBehaviour
    {
        public readonly SyncHashSet<string> set = new SyncHashSet<string>();
    }
}