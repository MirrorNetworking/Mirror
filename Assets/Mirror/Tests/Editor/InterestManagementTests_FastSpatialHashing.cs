// default = no component = everyone sees everyone

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class InterestManagementTests_FastSpatialHashing : InterestManagementTests_Common
    {
        FastSpatialInterestManagement aoi;

        [SetUp]
        public override void SetUp()
        {

            // TODO: these are just copied from the base Setup methods since the aoi expects "normal" operation
            // for example: OnSpawned to be called for spawning identities, late adding the aoi does not work currently
            // the setup also adds each identity to spawned twice so that also causes some issues during teardown
            instantiated = new List<GameObject>();

            // need a holder GO. with name for easier debugging.
            holder = new GameObject("MirrorTest.holder");

            // need a transport to send & receive
            Transport.active = transport = holder.AddComponent<MemoryTransport>();

            // A with connectionId = 0x0A, netId = 0xAA
            CreateNetworked(out gameObjectA, out identityA);
            connectionA = new NetworkConnectionToClient(0x0A);
            connectionA.isAuthenticated = true;
            connectionA.isReady = true;
            connectionA.identity = identityA;
            //NetworkServer.spawned[0xAA] = identityA; // TODO: this causes two the identities to end up in spawned twice

            // B
            CreateNetworked(out gameObjectB, out identityB);
            connectionB = new NetworkConnectionToClient(0x0B);
            connectionB.isAuthenticated = true;
            connectionB.isReady = true;
            connectionB.identity = identityB;
            //NetworkServer.spawned[0xBB] = identityB; // TODO: this causes two the identities to end up in spawned twice

            // need to start server so that interest management works
            NetworkServer.Listen(10);

            // add both connections
            NetworkServer.connections[connectionA.connectionId] = connectionA;
            NetworkServer.connections[connectionB.connectionId] = connectionB;

            aoi = holder.AddComponent<FastSpatialInterestManagement>();
            aoi.visRange = 10;
            // setup server aoi since InterestManagement Awake isn't called
            NetworkServer.aoi = aoi;

            // spawn both so that .observers is created
            NetworkServer.Spawn(gameObjectA, connectionA);
            NetworkServer.Spawn(gameObjectB, connectionB);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            // clear server aoi again
            NetworkServer.aoi = null;
        }

        public override void ForceHidden_Initial()
        {
            // doesnt support changing visibility at runtime
        }

        public override void ForceShown_Initial()
        {
            // doesnt support changing visibility at runtime
        }

        // brute force interest management
        // => everyone should see everyone if in range
        [Test]
        public void InRange_Initial()
        {
            // A and B are at (0,0,0) so within range!

            aoi.LateUpdate();
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

            aoi.LateUpdate();
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

            aoi.LateUpdate();
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

            aoi.LateUpdate();
            // both should not see each other
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.False);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.False);
        }

        // TODO add tests to make sure old observers are removed etc.
    }
}
