using NUnit.Framework;
using UnityEngine;

using static Mirror.Tests.LocalConnections;

namespace Mirror.Tests.Host
{
    public class SampleBehavior : NetworkBehaviour
    {
    }

    public class NetworkBehaviourTests : HostSetup<SampleBehavior>
    {
        #region Component flags
        [Test]
        public void IsServerOnly()
        {
            Assert.That(component.IsServerOnly, Is.False);
        }

        [Test]
        public void IsServer()
        {
            Assert.That(component.IsServer, Is.True);
        }

        [Test]
        public void IsClient()
        {
            Assert.That(component.IsClient, Is.True);
        }

        [Test]
        public void IsClientOnly()
        {
            Assert.That(component.IsClientOnly, Is.False);
        }

        [Test]
        public void PlayerHasAuthorityByDefault()
        {
            // no authority by default
            Assert.That(component.HasAuthority, Is.True);
        }

        #endregion

        class OnStartServerTestComponent : NetworkBehaviour
        {
            public bool called;

            public void OnStartServer()
            {
                Assert.That(IsClient, Is.True);
                Assert.That(IsLocalPlayer, Is.False);
                Assert.That(IsServer, Is.True);
                called = true;
            }
        };

        // check isClient/isServer/isLocalPlayer in server-only mode
        [Test]
        public void OnStartServer()
        {
            var gameObject = new GameObject();
            NetworkIdentity netIdentity = gameObject.AddComponent<NetworkIdentity>();
            OnStartServerTestComponent comp = gameObject.AddComponent<OnStartServerTestComponent>();
            netIdentity.OnStartServer.AddListener(comp.OnStartServer);

            Assert.That(comp.called, Is.False);
            serverObjectManager.Spawn(gameObject);

            Assert.That(comp.called, Is.True);

            Object.Destroy(gameObject);
        }


        [Test]
        public void SpawnedObjectNoAuthority()
        {
            var gameObject2 = new GameObject();
            gameObject2.AddComponent<NetworkIdentity>();
            SampleBehavior behaviour2 = gameObject2.AddComponent<SampleBehavior>();

            serverObjectManager.Spawn(gameObject2);

            client.Update();

            // no authority by default
            Assert.That(behaviour2.HasAuthority, Is.False);
        }

        [Test]
        public void HasIdentitysNetId()
        {
            identity.NetId = 42;
            Assert.That(component.NetId, Is.EqualTo(42));
        }

        [Test]
        public void TimeTest()
        {
            SampleBehavior behaviour1 = playerGO.AddComponent<SampleBehavior>();
            Assert.That(behaviour1.NetworkTime, Is.EqualTo(client.Time));
        }

        [Test]
        public void HasIdentitysConnectionToServer()
        {
            (identity.ConnectionToServer, _) = PipedConnections();
            Assert.That(component.ConnectionToServer, Is.EqualTo(identity.ConnectionToServer));
        }

        [Test]
        public void HasIdentitysConnectionToClient()
        {
            (_, identity.ConnectionToClient) = PipedConnections();
            Assert.That(component.ConnectionToClient, Is.EqualTo(identity.ConnectionToClient));
        }

        [Test]
        public void ComponentIndex()
        {
            var extraObject = new GameObject();

            extraObject.AddComponent<NetworkIdentity>();

            SampleBehavior behaviour1 = extraObject.AddComponent<SampleBehavior>();
            SampleBehavior behaviour2 = extraObject.AddComponent<SampleBehavior>();

            // original one is first networkbehaviour, so index is 0
            Assert.That(behaviour1.ComponentIndex, Is.EqualTo(0));
            // extra one is second networkbehaviour, so index is 1
            Assert.That(behaviour2.ComponentIndex, Is.EqualTo(1));

            Object.Destroy(extraObject);
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
                Assert.That(GetSyncVarHookGuard(bit), Is.False);

                // set true
                SetSyncVarHookGuard(bit, true);
                Assert.That(GetSyncVarHookGuard(bit), Is.True);

                // set false again
                SetSyncVarHookGuard(bit, false);
                Assert.That(GetSyncVarHookGuard(bit), Is.False);
            }
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourInitSyncObjectTester : NetworkBehaviour
    {
        [Test]
        public void InitSyncObject()
        {
            ISyncObject syncObject = new SyncList<bool>();
            InitSyncObject(syncObject);
            Assert.That(syncObjects.Count, Is.EqualTo(1));
            Assert.That(syncObjects[0], Is.EqualTo(syncObject));
        }
    }
}
