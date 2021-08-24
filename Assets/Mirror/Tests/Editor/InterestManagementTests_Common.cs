// default = no component = everyone sees everyone
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public abstract class InterestManagementTests_Common : MirrorEditModeTest
    {
        protected GameObject gameObjectA;
        protected NetworkIdentity identityA;
        protected NetworkConnectionToClient connectionA;

        protected GameObject gameObjectB;
        protected NetworkIdentity identityB;
        protected NetworkConnectionToClient connectionB;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // A with connectionId = 0x0A, netId = 0xAA
            CreateNetworked(out gameObjectA, out identityA);
            connectionA = new NetworkConnectionToClient(0x0A);
            connectionA.isAuthenticated = true;
            connectionA.isReady = true;
            connectionA.identity = identityA;
            NetworkServer.spawned[0xAA] = identityA;

            // B
            CreateNetworked(out gameObjectB, out identityB);
            connectionB = new NetworkConnectionToClient(0x0B);
            connectionB.isAuthenticated = true;
            connectionB.isReady = true;
            connectionB.identity = identityB;
            NetworkServer.spawned[0xBB] = identityB;

            // need to start server so that interest management works
            NetworkServer.Listen(10);

            // add both connections
            NetworkServer.connections[connectionA.connectionId] = connectionA;
            NetworkServer.connections[connectionB.connectionId] = connectionB;

            // spawn both so that .observers is created
            NetworkServer.Spawn(gameObjectA, connectionA);
            NetworkServer.Spawn(gameObjectB, connectionB);

            // spawn already runs interest management once
            // clear observers and observing so tests can start from scratch
            identityA.observers.Clear();
            identityB.observers.Clear();
            connectionA.observing.Clear();
            connectionB.observing.Clear();
        }

        [TearDown]
        public override void TearDown()
        {
            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            identityA.isServer = false;

            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            identityB.isServer = false;

            // clear connections first. calling OnDisconnect wouldn't work since
            // we have no real clients.
            NetworkServer.connections.Clear();

            base.TearDown();
        }

        // player should always see self no matter what
        [Test]
        public void PlayerAlwaysSeesSelf_Initial()
        {
            // rebuild for A
            // initial rebuild adds all connections if no interest management available
            NetworkServer.RebuildObservers(identityA, true);

            // should see self
            Assert.That(identityA.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // forceHidden should still work
        [Test]
        public abstract void ForceHidden_Initial();

        // forceShown should still work
        [Test]
        public abstract void ForceShown_Initial();
    }
}
