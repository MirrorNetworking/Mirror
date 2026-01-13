using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    // Test SyncSet Action deferral to eliminate race conditions
    public class SyncSetActionTest_Deferral : MirrorTest
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
            CreateNetworked(out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncSetDeferralBehaviour serverComp);
            CreateNetworked(out GameObject clientGO, out NetworkIdentity clientIdentity, out SyncSetDeferralBehaviour clientComp);

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
            serverComp.set.Add("first");
            serverComp.set.Add("second");

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(connectionToClient, serverGO);
            ProcessMessages();

            // double check isServer/isClient.  avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // make sure the client really spawned it.
            Assert.That(clientGO.activeSelf, Is.True);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));

            Assert.That(clientComp.executionOrder.Count, Is.GreaterThanOrEqualTo(3), $"Should have OnStartClient + Actions");
            Assert.That(clientComp.executionOrder[0], Is.EqualTo("OnStartClient"));
            // Note: SyncSet order isn't guaranteed, so just check they both fired
            Assert.That(clientComp.executionOrder.Contains("OnAdd:first"));
            Assert.That(clientComp.executionOrder.Contains("OnAdd:second"));
        }

        [Test]
        public void PureClient_CrossObjectReferencesWork()
        {
            // Create object B and manually spawn it first
            CreateNetworked(out GameObject serverGOB, out NetworkIdentity serverIdentityB);
            CreateNetworked(out GameObject clientGOB, out NetworkIdentity clientIdentityB);

            // give both a scene id and register it on client for spawnables
            clientIdentityB.sceneId = serverIdentityB.sceneId = (ulong)serverGOB.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentityB.sceneId] = clientIdentityB;
            clientGOB.SetActive(false);

            // Create player object A with SyncSet
            CreateNetworked(out GameObject serverGOA, out NetworkIdentity serverIdentityA, out SyncSetIdentityBehaviour serverCompA);
            CreateNetworked(out GameObject clientGOA, out NetworkIdentity clientIdentityA, out SyncSetIdentityBehaviour clientCompA);

            // give both a scene id and register it on client for spawnables
            clientIdentityA.sceneId = serverIdentityA.sceneId = (ulong)serverGOA.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentityA.sceneId] = clientIdentityA;
            clientGOA.SetActive(false);

            // Track what we find in OnStartClient
            NetworkIdentity foundIdentity = null;
            clientCompA.onStartClientCalled = () =>
            {
                // Check if cross-reference is valid BEFORE wiring up actions
                if (clientCompA.identities.Count > 0)
                {
                    foreach (var identity in clientCompA.identities)
                    {
                        foundIdentity = identity;
                        break; // just grab the first one
                    }
                }
            };

            // Spawn B on server
            NetworkServer.Spawn(serverGOB);

            // Add reference to B BEFORE spawning player (goes in spawn payload)
            serverCompA.identities.Add(serverIdentityB);

            // Add as player & process spawn messages on client
            NetworkServer.AddPlayerForConnection(connectionToClient, serverGOA);
            ProcessMessages();

            // double check both spawned
            Assert.That(serverIdentityA.isServer, Is.True);
            Assert.That(clientIdentityA.isClient, Is.True);
            Assert.That(clientGOA.activeSelf, Is.True);
            Assert.That(clientGOB.activeSelf, Is.True);

            // Cross-reference should be valid in OnStartClient
            Assert.That(foundIdentity, Is.Not.Null, "Should have valid NetworkIdentity reference");
            Assert.That(foundIdentity.netId, Is.EqualTo(clientIdentityB.netId), "Should reference correct NetworkIdentity");
        }

        [Test]
        public void PureClient_MultipleCollectionsDeferred()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out MultiSyncSetBehaviour serverComp,
                out _, out _, out MultiSyncSetBehaviour clientComp,
                connectionToClient);

            serverComp.setA.Add("A1");
            serverComp.setB.Add(42);

            List<string> executionOrder = new List<string>();
            clientComp.setA.OnAdd += (item) => executionOrder.Add($"SetA:Add:{item}");
            clientComp.setB.OnAdd += (item) => executionOrder.Add($"SetB:Add:{item}");

            ProcessMessages();

            // both Actions should fire
            Assert.That(executionOrder.Count, Is.EqualTo(2));
            Assert.That(executionOrder.Contains("SetA:Add:A1"));
            Assert.That(executionOrder.Contains("SetB:Add:42"));
        }
    }

    // Test NetworkBehaviours for deferral testing
    public class SyncSetDeferralBehaviour : NetworkBehaviour
    {
        public readonly SyncHashSet<string> set = new SyncHashSet<string>();
        public System.Action onStartClientCalled;

        // Track execution order
        public List<string> executionOrder = new List<string>();

        public override void OnStartClient()
        {
            set.OnAdd += (item) => executionOrder.Add($"OnAdd:{item}");

            onStartClientCalled?.Invoke();

            // Invoke OnAdd for all items in set
            foreach (string item in set)
                set.OnAdd?.Invoke(item);
        }
    }

    public class SyncSetIdentityBehaviour : NetworkBehaviour
    {
        public readonly SyncHashSet<NetworkIdentity> identities = new SyncHashSet<NetworkIdentity>();
        public System.Action onStartClientCalled;

        public override void OnStartClient()
        {
            onStartClientCalled?.Invoke();
        }
    }

    public class MultiSyncSetBehaviour : NetworkBehaviour
    {
        public readonly SyncHashSet<string> setA = new SyncHashSet<string>();
        public readonly SyncHashSet<int> setB = new SyncHashSet<int>();
    }
}