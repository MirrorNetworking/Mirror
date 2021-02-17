// default = no component = everyone sees everyone
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class InterestManagementTests_SpatialHashing : InterestManagementTests_Common
    {
        SpatialHashingInterestManagement aoi;

        [SetUp]
        public override void SetUp()
        {
            GameObject go = new GameObject();
            aoi = go.AddComponent<SpatialHashingInterestManagement>();
            aoi.visRange = 10;
            // setup server aoi since InterestManagement Awake isn't called
            NetworkServer.aoi = aoi;
            base.SetUp();

            // rebuild grid once so the two connections are in there
            aoi.Update();
        }

        [TearDown]
        public override void TearDown()
        {
            GameObject.DestroyImmediate(aoi.gameObject);
            base.TearDown();
            // clear server aoi again
            NetworkServer.aoi = null;
        }

        // brute force interest management
        // => forceHidden should still work
        [Test]
        public override void ForceHidden_Initial()
        {
            // A and B are at (0,0,0) so within range!

            // force hide A
            identityA.visible = Visibility.ForceHidden;

            // rebuild for both
            // initial rebuild while both are within range
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // A should not be seen by B because A is force hidden
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.False);
            // B should be seen by A because
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // brute force interest management
        // => forceShown should still work
        [Test]
        public override void ForceShown_Initial()
        {
            // A and B are too far from each other
            identityB.transform.position = Vector3.right * (aoi.visRange + 1);

            // force show A
            identityA.visible = Visibility.ForceShown;

            // update grid now that positions were changed
            aoi.Update();

            // rebuild for both
            // initial rebuild while both are within range
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // A should see B because A is force shown
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.True);
            // B should not be seen by A because they are too far from each other
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.False);
        }

        // brute force interest management
        // => everyone should see everyone if in range
        [Test]
        public void InRange_Initial()
        {
            // A and B are at (0,0,0) so within range!

            // rebuild for both
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // both should see each other because they are in range
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.True);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // brute force interest management
        // => everyone should see everyone if in range
        [Test]
        public void InRange_NotInitial()
        {
            // A and B are at (0,0,0) so within range!

            // rebuild for both
            NetworkServer.RebuildObservers(identityA, false);
            NetworkServer.RebuildObservers(identityB, false);

            // both should see each other because they are in range
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.True);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // brute force interest management
        // => everyone should see everyone if in range
        [Test]
        public void OutOfRange_Initial()
        {
            // A and B are too far from each other
            identityB.transform.position = Vector3.right * (aoi.visRange + 1);

            // update grid now that positions were changed
            aoi.Update();

            // rebuild for boths
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // both should not see each other
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.False);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.False);
        }

        // brute force interest management
        // => everyone should see everyone if in range
        [Test]
        public void OutOfRange_NotInitial()
        {
            // A and B are too far from each other
            identityB.transform.position = Vector3.right * (aoi.visRange + 1);

            // update grid now that positions were changed
            aoi.Update();

            // rebuild for boths
            NetworkServer.RebuildObservers(identityA, false);
            NetworkServer.RebuildObservers(identityB, false);

            // both should not see each other
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.False);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.False);
        }

        // TODO add tests to make sure old observers are removed etc.
    }
}
