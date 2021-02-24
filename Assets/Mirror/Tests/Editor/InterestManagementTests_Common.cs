// default = no component = everyone sees everyone
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public abstract class InterestManagementTests_Common
    {
        protected GameObject gameObjectA;
        protected NetworkIdentity identityA;
        protected NetworkConnectionToClient connectionA;

        protected GameObject gameObjectB;
        protected NetworkIdentity identityB;
        protected NetworkConnectionToClient connectionB;

        [SetUp]
        public virtual void SetUp()
        {
            // A with connectionId = 0x0A, netId = 0xAA
            gameObjectA = new GameObject();
            identityA = gameObjectA.AddComponent<NetworkIdentity>();
            connectionA = new NetworkConnectionToClient(0x0A, false, 0);
            connectionA.isAuthenticated = true;
            connectionA.isReady = true;
            connectionA.identity = identityA;
            NetworkIdentity.spawned[0xAA] = identityA;

            // B
            gameObjectB = new GameObject();
            identityB = gameObjectB.AddComponent<NetworkIdentity>();
            connectionB = new NetworkConnectionToClient(0x0B, false, 0);
            connectionB.isAuthenticated = true;
            connectionB.isReady = true;
            connectionB.identity = identityB;
            NetworkIdentity.spawned[0xBB] = identityB;

            // need to start server so that interest management works
            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();
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
        public virtual void TearDown()
        {
            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            identityA.isServer = false;
            GameObject.DestroyImmediate(gameObjectA);

            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            identityB.isServer = false;
            GameObject.DestroyImmediate(gameObjectB);

            // clean so that null entries are not in dictionary
            NetworkIdentity.spawned.Clear();

            // clear connections first. calling OnDisconnect wouldn't work since
            // we have no real clients.
            NetworkServer.connections.Clear();

            // stop server
            NetworkServer.Shutdown();
            GameObject.DestroyImmediate(Transport.activeTransport.gameObject);
            Transport.activeTransport = null;
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
