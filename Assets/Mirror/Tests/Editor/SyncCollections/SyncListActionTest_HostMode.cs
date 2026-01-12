using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    // Test SyncList Actions in Host Mode with AOI scenarios
    // Following pattern from InterestManagementTests_Distance
    public class SyncListActionTest_HostMode : MirrorTest
    {
        DistanceInterestManagement aoi;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // setup AOI component
            aoi = holder.AddComponent<DistanceInterestManagement>();
            aoi.visRange = 10;
            NetworkServer.aoi = aoi;

            // Use client/server, NOT host mode
            NetworkServer.Listen(10);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkServer.aoi = null;
            base.TearDown();
        }

        [Test]
        public void ClientServer_ActionNotCalledForOutOfRangeObject()
        {
            // spawn object far away (out of AOI range)
            CreateNetworkedAndSpawn(
                out _, out _, out SyncListHostModeBehaviour serverComp,
                out _, out _, out SyncListHostModeBehaviour clientComp);

            serverComp.transform.position = Vector3.right * (aoi.visRange + 1);

            // rebuild observers - should not be visible
            NetworkServer.RebuildObservers(serverComp.netIdentity, true);

            int actionCallCount = 0;
            clientComp.list.OnAdd += (index) => actionCallCount++;

            // add on server - Action should NOT fire on client because out of AOI range
            serverComp.list.Add("test");
            ProcessMessages();

            Assert.That(actionCallCount, Is.EqualTo(0), "Action should not fire for out-of-range object");
        }

        [Test]
        public void ClientServer_ActionCalledForInRangeObject()
        {
            // spawn object within AOI range (at origin)
            CreateNetworkedAndSpawn(
                out _, out _, out SyncListHostModeBehaviour serverComp,
                out _, out _, out SyncListHostModeBehaviour clientComp);

            // rebuild observers - should be visible
            NetworkServer.RebuildObservers(serverComp.netIdentity, true);

            int actionCallCount = 0;
            clientComp.list.OnAdd += (index) => actionCallCount++;

            // add on server - Action SHOULD fire on client because in AOI range  
            serverComp.list.Add("test");
            ProcessMessages();

            Assert.That(actionCallCount, Is.EqualTo(1), "Action should fire for in-range object");
        }

        [Test]
        public void ClientServer_ActionsCalledWhenEnteringRange()
        {
            // spawn object far away
            CreateNetworkedAndSpawn(
                out _, out _, out SyncListHostModeBehaviour serverComp,
                out _, out _, out SyncListHostModeBehaviour clientComp);

            serverComp.transform.position = Vector3.right * (aoi.visRange + 1);

            // rebuild - not visible yet
            NetworkServer.RebuildObservers(serverComp.netIdentity, true);

            // add items while out of range
            serverComp.list.Add("first");
            serverComp.list.Add("second");
            serverComp.list.Add("third");
            ProcessMessages();

            // Track Actions
            List<string> actionsReceived = new List<string>();
            clientComp.list.OnAdd += (index) => actionsReceived.Add($"Add:{clientComp.list[index]}");

            // move into range
            serverComp.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(serverComp.netIdentity, false);
            ProcessMessages();

            Assert.That(actionsReceived.Count, Is.EqualTo(3), "All three Actions should fire when entering range");
            Assert.That(actionsReceived[0], Is.EqualTo("Add:first"));
            Assert.That(actionsReceived[1], Is.EqualTo("Add:second"));
            Assert.That(actionsReceived[2], Is.EqualTo("Add:third"));
        }
    }

    // Test NetworkBehaviour with SyncList for host mode testing
    public class SyncListHostModeBehaviour : NetworkBehaviour
    {
        public readonly SyncList<string> list = new SyncList<string>();
    }
}