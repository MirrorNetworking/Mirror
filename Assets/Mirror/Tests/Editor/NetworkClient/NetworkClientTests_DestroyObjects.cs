using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_DestroyObjects : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);
        }

        // ── OnObjectDestroy ───────────────────────────────────────────────────

        [Test]
        public void OnObjectDestroy_RemovesIdentityFromSpawned()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 100;
            identity.netId = netId;
            // give it a sceneId so it is disabled rather than destroyed,
            // avoiding a double-free with the instantiated tracker in TearDown.
            identity.sceneId = 10;
            NetworkClient.spawned[netId] = identity;

            NetworkClient.OnObjectDestroy(new ObjectDestroyMessage { netId = netId });

            Assert.That(NetworkClient.spawned.ContainsKey(netId), Is.False);
        }

        [Test]
        public void OnObjectDestroy_SceneObjectIsDisabledAndReturnedToSpawnableObjects()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 101;
            const ulong sceneId = 55;
            identity.netId = netId;
            identity.sceneId = sceneId;
            identity.gameObject.SetActive(true);
            NetworkClient.spawned[netId] = identity;

            NetworkClient.OnObjectDestroy(new ObjectDestroyMessage { netId = netId });

            Assert.That(identity.gameObject.activeSelf, Is.False);
            Assert.That(NetworkClient.spawnableObjects.ContainsKey(sceneId), Is.True);
        }

        [Test]
        public void OnObjectDestroy_CallsUnspawnHandlerWhenRegistered()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 102;
            const uint assetId = 77;
            identity.netId = netId;
            identity.assetId = assetId;
            identity.sceneId = 0;
            NetworkClient.spawned[netId] = identity;

            bool unspawnCalled = false;
            NetworkClient.unspawnHandlers[assetId] = _ => unspawnCalled = true;

            NetworkClient.OnObjectDestroy(new ObjectDestroyMessage { netId = netId });

            Assert.That(unspawnCalled, Is.True);
        }

        [Test]
        public void OnObjectDestroy_DoesNothingForUnknownNetId()
        {
            // Should silently skip — no error, no exception.
            NetworkClient.OnObjectDestroy(new ObjectDestroyMessage { netId = 9999 });
        }

        [Test]
        public void OnObjectDestroy_NonSceneObjectWithNoUnspawnHandler_IsRemovedFromSpawned()
        {
            // sceneId == 0 and no unspawn handler → DestroyObject calls GameObject.Destroy.
            // The tracker's null-guard in TearDown handles the deferred destruction safely.
            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 105;
            identity.netId = netId;
            // sceneId defaults to 0 — this is the non-scene object path
            NetworkClient.spawned[netId] = identity;

            NetworkClient.OnObjectDestroy(new ObjectDestroyMessage { netId = netId });

            Assert.That(NetworkClient.spawned.ContainsKey(netId), Is.False);
        }

        // ── DestroyAllClientObjects ───────────────────────────────────────────

        [Test]
        public void DestroyAllClientObjects_ClearsSpawnedDictionary()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            identity.netId = 200;
            identity.sceneId = 20; // scene object: disabled rather than destroyed
            NetworkClient.spawned[200] = identity;

            NetworkClient.DestroyAllClientObjects();

            Assert.That(NetworkClient.spawned, Is.Empty);
        }

        [Test]
        public void DestroyAllClientObjects_SceneObjectIsDisabledNotDestroyed()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            identity.netId = 201;
            identity.sceneId = 21;
            identity.gameObject.SetActive(true);
            NetworkClient.spawned[201] = identity;

            NetworkClient.DestroyAllClientObjects();

            Assert.That(identity != null);
            Assert.That(identity.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DestroyAllClientObjects_CallsUnspawnHandlerForRegisteredAsset()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            const uint assetId = 300;
            identity.netId = 202;
            identity.assetId = assetId;
            identity.sceneId = 22; // scene object to avoid destroy issues
            NetworkClient.spawned[202] = identity;

            bool unspawnCalled = false;
            NetworkClient.unspawnHandlers[assetId] = _ => unspawnCalled = true;

            NetworkClient.DestroyAllClientObjects();

            Assert.That(unspawnCalled, Is.True);
        }

        [Test]
        public void DestroyAllClientObjects_ClearsConnectionOwned()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            identity.netId = 205;
            identity.sceneId = 25; // scene object — disabled rather than destroyed
            NetworkClient.spawned[205] = identity;
            NetworkClient.connection.owned.Add(identity);

            NetworkClient.DestroyAllClientObjects();

            Assert.That(NetworkClient.connection.owned, Is.Empty);
        }
    }
}