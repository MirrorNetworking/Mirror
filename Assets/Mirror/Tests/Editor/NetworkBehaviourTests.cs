using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    class EmptyBehaviour : NetworkBehaviour
    {
    }

    // test class inherits from NetworkBehaviour so that we can tested protected
    // methods too
    public class NetworkBehaviourTests : NetworkBehaviour
    {
        GameObject gameObject;
        NetworkIdentity identity;
        EmptyBehaviour emptyBehaviour; // useful in most tests, but not necessarily all tests

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();

            // add a behaviour for testing
            emptyBehaviour = gameObject.AddComponent<EmptyBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void IsServerOnly()
        {
            // start server and assign netId so that isServer is true
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1);
            identity.netId = 42;

            // isServerOnly should be true when isServer = true && isClient = false
            Assert.That(emptyBehaviour.isServer, Is.True);
            Assert.That(emptyBehaviour.isClient, Is.False);
            Assert.That(emptyBehaviour.isServerOnly, Is.True);

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        [Test]
        public void IsClientOnly()
        {
            // isClientOnly should be true when isServer = false && isClient = true
            identity.isClient = true;
            Assert.That(emptyBehaviour.isServer, Is.False);
            Assert.That(emptyBehaviour.isClient, Is.True);
            Assert.That(emptyBehaviour.isClientOnly, Is.True);
        }

        [Test]
        public void HasNoAuthorityByDefault()
        {
            // no authority by default
            Assert.That(emptyBehaviour.hasAuthority, Is.False);
        }

        [Test]
        public void HasIdentitysNetId()
        {
            identity.netId = 42;
            Assert.That(emptyBehaviour.netId, Is.EqualTo(42));
        }

        [Test]
        public void HasIdentitysConnectionToServer()
        {
            identity.connectionToServer = new ULocalConnectionToServer();
            Assert.That(emptyBehaviour.connectionToServer, Is.EqualTo(identity.connectionToServer));
        }

        [Test]
        public void HasIdentitysConnectionToClient()
        {
            identity.connectionToClient = new ULocalConnectionToClient();
            Assert.That(emptyBehaviour.connectionToClient, Is.EqualTo(identity.connectionToClient));
        }

        [Test]
        public void ComponentIndex()
        {
            // add one extra component
            EmptyBehaviour extra = gameObject.AddComponent<EmptyBehaviour>();

            // original one is first networkbehaviour, so index is 0
            Assert.That(emptyBehaviour.ComponentIndex, Is.EqualTo(0));

            // extra one is second networkbehaviour, so index is 1
            Assert.That(extra.ComponentIndex, Is.EqualTo(1));
        }

        [Test]
        public void OnCheckObserverTrueByDefault()
        {
            Assert.That(emptyBehaviour.OnCheckObserver(null), Is.True);
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourHookGuardTester : NetworkBehaviour
    {
        [Test]
        public void HookGuard()
        {
            // set hook guard for some bits
            for (int i = 0; i < 10; ++i)
            {
                ulong bit = 1ul << i;

                // should be false by default
                Assert.That(getSyncVarHookGuard(bit), Is.False);

                // set true
                setSyncVarHookGuard(bit, true);
                Assert.That(getSyncVarHookGuard(bit), Is.True);

                // set false again
                setSyncVarHookGuard(bit, false);
                Assert.That(getSyncVarHookGuard(bit), Is.False);
            }
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourInitSyncObjectTester : NetworkBehaviour
    {
        [Test]
        public void InitSyncObject()
        {
            SyncObject syncObject = new SyncListBool();
            InitSyncObject(syncObject);
            Assert.That(syncObjects.Count, Is.EqualTo(1));
            Assert.That(syncObjects[0], Is.EqualTo(syncObject));
        }
    }
}
