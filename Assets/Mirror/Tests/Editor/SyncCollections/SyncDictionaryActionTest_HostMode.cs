using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    // Test SyncDictionary Actions in Host Mode with AOI scenarios
    public class SyncDictionaryActionTest_HostMode : MirrorTest
    {
        DistanceInterestManagement aoi;
        NetworkConnectionToClient connectionToClient;

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
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkServer.aoi = null;
            base.TearDown();
        }

        [Test]
        public void ClientServer_ActionsRespectAOI()
        {
            // spawn host client
            CreateNetworkedAndSpawnPlayer(
                out GameObject serverPlayer, out NetworkIdentity serverNI,
                out _, out _, connectionToClient);

            // move host client player out of range
            serverPlayer.transform.position = Vector3.right * (aoi.visRange + 1);

            // spawn object (should be out of range of moved host client)
            CreateNetworked(out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncDictionaryHostModeBehaviour serverComp);
            CreateNetworked(out GameObject clientGO, out NetworkIdentity clientIdentity, out SyncDictionaryHostModeBehaviour clientComp);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // IMPORTANT: OnSpawn finds 'sceneId' in .spawnableObjects.
            // only those who are ConsiderForSpawn() are in there.
            // for scene objects to be considered, they need to be disabled.
            // (it'll be active by the time we return here)
            clientGO.SetActive(false);

            // setup Action tracking
            int actionCallCount = 0;
            clientComp.dictionary.OnAdd += (key) => actionCallCount++;

            // add on server before spawn to include in payload
            // Action should NOT fire on client because out of AOI range
            serverComp.dictionary.Add("key1", "first");

            // spawn
            NetworkServer.Spawn(serverGO);
            ProcessMessages();

            // double check isServer. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);

            // host client player out of range...thus isClient should be false
            Assert.That(clientIdentity.isClient, Is.False);

            // rebuild observers - should not be visible
            NetworkServer.RebuildObservers(serverNI, true);
            ProcessMessages();

            Assert.That(actionCallCount, Is.EqualTo(0), "Action should not fire for out-of-range object");

            // add item while out of range
            serverComp.dictionary.Add("key2", "second");
            ProcessMessages();

            Assert.That(actionCallCount, Is.EqualTo(0), "Actions should not fire for out-of-range object");

            // move host client player into range
            serverPlayer.transform.position = Vector3.zero;

            // rebuild observers - should be visible now
            NetworkServer.RebuildObservers(serverComp.netIdentity, false);
            ProcessMessages();

            Assert.That(clientComp.actionsReceived.Count, Is.EqualTo(2), "Two Actions invoked by OnStartClient");
            Assert.That(clientComp.actionsReceived[0], Is.EqualTo("Add:key1:first"));
            Assert.That(clientComp.actionsReceived[1], Is.EqualTo("Add:key2:second"));

            // add item while in range
            serverComp.dictionary.Add("key3", "third");
            ProcessMessages();

            Assert.That(clientComp.actionsReceived.Count, Is.EqualTo(3), "Total of 3 Actions invoked");
            Assert.That(clientComp.actionsReceived[2], Is.EqualTo("Add:key3:third"));
        }
    }

    // Test NetworkBehaviour with SyncDictionary for host mode testing
    public class SyncDictionaryHostModeBehaviour : NetworkBehaviour
    {
        internal readonly SyncDictionary<string, string> dictionary = new SyncDictionary<string, string>();

        internal List<string> actionsReceived = new List<string>();

        public override void OnStartClient()
        {
            dictionary.OnAdd += (key) => actionsReceived.Add($"Add:{key}:{dictionary[key]}");

            // Invoke OnAdd for all items in dictionary
            foreach (var kvp in dictionary)
                dictionary.OnAdd?.Invoke(kvp.Key);
        }
    }
}
