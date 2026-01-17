using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    // Test SyncDictionary Action deferral to eliminate race conditions
    public class SyncDictionaryActionTest_Deferral : MirrorTest
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
            CreateNetworked(out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncDictionaryDeferralBehaviour serverComp);
            CreateNetworked(out GameObject clientGO, out NetworkIdentity clientIdentity, out SyncDictionaryDeferralBehaviour clientComp);

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
            serverComp.dictionary.Add("key1", "first");
            serverComp.dictionary.Add("key2", "second");

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
            Assert.That(clientComp.executionOrder.Contains("OnAdd:key1"));
            Assert.That(clientComp.executionOrder.Contains("OnAdd:key2"));
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

            // Create player object A with SyncDictionary
            CreateNetworked(out GameObject serverGOA, out NetworkIdentity serverIdentityA, out SyncDictionaryIdentityBehaviour serverCompA);
            CreateNetworked(out GameObject clientGOA, out NetworkIdentity clientIdentityA, out SyncDictionaryIdentityBehaviour clientCompA);

            // give both a scene id and register it on client for spawnables
            clientIdentityA.sceneId = serverIdentityA.sceneId = (ulong)serverGOA.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentityA.sceneId] = clientIdentityA;
            clientGOA.SetActive(false);

            // Track what we find in OnStartClient
            NetworkIdentity foundIdentity = null;
            clientCompA.onStartClientCalled = () =>
            {
                // Check if cross-reference is valid BEFORE wiring up actions
                if (clientCompA.identities.ContainsKey("objectB"))
                    foundIdentity = clientCompA.identities["objectB"];
            };

            // Spawn B on server
            NetworkServer.Spawn(serverGOB);

            // Add reference to B BEFORE spawning player (goes in spawn payload)
            serverCompA.identities.Add("objectB", serverIdentityB);

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
                out _, out _, out MultiSyncDictionaryBehaviour serverComp,
                out _, out _, out MultiSyncDictionaryBehaviour clientComp,
                connectionToClient);

            serverComp.dictionaryA.Add("keyA", "A1");
            serverComp.dictionaryB.Add("keyB", 42);

            List<string> executionOrder = new List<string>();
            clientComp.dictionaryA.OnAdd += (key) => executionOrder.Add($"DictA:Add:{key}");
            clientComp.dictionaryB.OnAdd += (key) => executionOrder.Add($"DictB:Add:{key}");

            ProcessMessages();

            // both Actions should fire
            Assert.That(executionOrder.Count, Is.EqualTo(2));
            Assert.That(executionOrder.Contains("DictA:Add:keyA"));
            Assert.That(executionOrder.Contains("DictB:Add:keyB"));
        }
    }

    // Test NetworkBehaviours for deferral testing
    public class SyncDictionaryDeferralBehaviour : NetworkBehaviour
    {
        public readonly SyncDictionary<string, string> dictionary = new SyncDictionary<string, string>();
        public System.Action onStartClientCalled;

        // Track execution order
        public List<string> executionOrder = new List<string>();

        public override void OnStartClient()
        {
            dictionary.OnAdd += (key) => executionOrder.Add($"OnAdd:{key}");

            onStartClientCalled?.Invoke();

            // Invoke OnAdd for all items in dictionary
            foreach (var kvp in dictionary)
                dictionary.OnAdd?.Invoke(kvp.Key);
        }
    }

    public class SyncDictionaryIdentityBehaviour : NetworkBehaviour
    {
        public readonly SyncDictionary<string, NetworkIdentity> identities = new SyncDictionary<string, NetworkIdentity>();
        public System.Action onStartClientCalled;

        public override void OnStartClient()
        {
            onStartClientCalled?.Invoke();
        }
    }

    public class MultiSyncDictionaryBehaviour : NetworkBehaviour
    {
        public readonly SyncDictionary<string, string> dictionaryA = new SyncDictionary<string, string>();
        public readonly SyncDictionary<string, int> dictionaryB = new SyncDictionary<string, int>();
    }
}