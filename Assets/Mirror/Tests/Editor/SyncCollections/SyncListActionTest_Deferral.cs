using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    // Test SyncList Action deferral to eliminate race conditions
    public class SyncListActionTest_Deferral : MirrorTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client (not host)
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void PureClient_ActionsDeferredUntilSpawnFinished()
        {
            // create host client player and spawn on server with data
            CreateNetworked(out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncListDeferralBehaviour serverComp);
            CreateNetworked(out GameObject clientGO, out NetworkIdentity clientIdentity, out SyncListDeferralBehaviour clientComp);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // IMPORTANT: OnSpawn finds 'sceneId' in .spawnableObjects.
            // only those who are ConsiderForSpawn() are in there.
            // for scene objects to be considered, they need to be disabled.
            // (it'll be active by the time we return here)
            clientGO.SetActive(false);

            // set up tracking
            clientComp.onStartClientCalled = () => clientComp.executionOrder.Add("OnStartClient");

            // add data on server before spawn
            serverComp.list.Add("first");
            serverComp.list.Add("second");

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(connectionToClient, serverGO);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // make sure the client really spawned it.
            Assert.That(clientGO.activeSelf, Is.True);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));

            Assert.That(clientComp.executionOrder.Count, Is.GreaterThanOrEqualTo(3), $"Should have OnAdd OnStartClient + Actions");
            Assert.That(clientComp.executionOrder[0], Is.EqualTo("OnStartClient"));
            Assert.That(clientComp.executionOrder[1], Is.EqualTo("OnAdd:0"));
            Assert.That(clientComp.executionOrder[2], Is.EqualTo("OnAdd:1"));
        }

        [Test]
        public void PureClient_CrossObjectReferencesWork()
        {
            // Spawn object B first
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentityB,
                out _, out NetworkIdentity clientIdentityB,
                connectionToClient);

            // Spawn object A with SyncList<NetworkIdentity> referencing B
            CreateNetworkedAndSpawn(
                out _, out _, out SyncListIdentityBehaviour serverCompA,
                out _, out _, out SyncListIdentityBehaviour clientCompA,
                connectionToClient);

            // add reference to B
            serverCompA.identities.Add(serverIdentityB);

            // track Action - should receive valid reference
            NetworkIdentity receivedIdentity = null;
            clientCompA.identities.OnAdd += (index) =>
            {
                receivedIdentity = clientCompA.identities[index];
            };

            ProcessMessages();

            // Action should fire with correct reference (B is already spawned)
            Assert.That(receivedIdentity, Is.Not.Null, "Should receive NetworkIdentity");
            Assert.That(receivedIdentity.netId, Is.EqualTo(clientIdentityB.netId), "Should reference correct NetworkIdentity");
        }

        [Test]
        public void PureClient_MultipleCollectionsDeferred()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out MultiSyncListBehaviour serverComp,
                out _, out _, out MultiSyncListBehaviour clientComp,
                connectionToClient);

            serverComp.listA.Add("A1");
            serverComp.listB.Add(42);

            List<string> executionOrder = new List<string>();
            clientComp.listA.OnAdd += (index) => executionOrder.Add($"ListA:Add:{index}");
            clientComp.listB.OnAdd += (index) => executionOrder.Add($"ListB:Add:{index}");

            ProcessMessages();

            // both Actions should fire
            Assert.That(executionOrder.Count, Is.EqualTo(2));
            Assert.That(executionOrder[0], Is.EqualTo("ListA:Add:0"));
            Assert.That(executionOrder[1], Is.EqualTo("ListB:Add:0"));
        }
    }

    // Test NetworkBehaviours for deferral testing
    public class SyncListDeferralBehaviour : NetworkBehaviour
    {
        public readonly SyncList<string> list = new SyncList<string>();
        public System.Action onStartClientCalled;
        public int actionCallCount = 0;

        // Track execution order
        public List<string> executionOrder = new List<string>();

        public override void OnStartClient()
        {
            list.OnAdd += (index) => executionOrder.Add($"OnAdd:{index}");

            actionCallCount++;
            onStartClientCalled?.Invoke();

            // Invoke OnAdd for all items in list
            for (int index = 0; index < list.Count; index++)
                list.OnAdd?.Invoke(index);
        }
    }

    public class SyncListIdentityBehaviour : NetworkBehaviour
    {
        public readonly SyncList<NetworkIdentity> identities = new SyncList<NetworkIdentity>();
    }

    public class MultiSyncListBehaviour : NetworkBehaviour
    {
        public readonly SyncList<string> listA = new SyncList<string>();
        public readonly SyncList<int> listB = new SyncList<int>();
    }
}