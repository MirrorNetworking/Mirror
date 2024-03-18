using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_PrepareToSpawnSceneObjects : NetworkClientTestsBase
    {
        NetworkIdentity CreateSceneObject(ulong sceneId)
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity);
            gameObject.name = "Runtime GameObject";
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            identity.sceneId = sceneId;
            return identity;
        }

        [Test]
        public void AddsAllInactiveIdentitiesInSceneWithSceneIdToDictionary()
        {
            NetworkIdentity obj1 = CreateSceneObject(10);
            NetworkIdentity obj2 = CreateSceneObject(11);

            obj1.gameObject.SetActive(false);
            obj2.gameObject.SetActive(false);

            NetworkClient.PrepareToSpawnSceneObjects();

            Assert.That(NetworkClient.spawnableObjects, Has.Count.EqualTo(2));

            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsValue(obj1));
            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsValue(obj2));
        }

        // test for https://github.com/MirrorNetworking/Mirror/issues/3541
        [Test]
        public void DoesAddActiveAndInactiveObjectsToDictionary()
        {
            NetworkIdentity active = CreateSceneObject(30);
            NetworkIdentity inactive = CreateSceneObject(32);

            active.gameObject.SetActive(true);
            inactive.gameObject.SetActive(false);

            NetworkClient.PrepareToSpawnSceneObjects();

            Assert.That(NetworkClient.spawnableObjects, Has.Count.EqualTo(2));
            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsValue(inactive));
            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsValue(active));
        }

        [Test]
        public void DoesNotAddObjectsWithNoSceneId()
        {
            NetworkIdentity noId = CreateSceneObject(0);
            NetworkIdentity hasId = CreateSceneObject(40);

            noId.gameObject.SetActive(false);
            hasId.gameObject.SetActive(false);

            NetworkClient.PrepareToSpawnSceneObjects();

            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsValue(hasId));
            Assert.IsFalse(NetworkClient.spawnableObjects.ContainsValue(noId));
        }

        [Test]
        public void AddsIdentitiesToDictionaryUsingSceneId()
        {
            NetworkIdentity obj1 = CreateSceneObject(20);
            NetworkIdentity obj2 = CreateSceneObject(21);
            obj1.gameObject.SetActive(false);
            obj2.gameObject.SetActive(false);

            NetworkClient.PrepareToSpawnSceneObjects();

            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsKey(20));
            Assert.That(NetworkClient.spawnableObjects[20], Is.EqualTo(obj1));

            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsKey(21));
            Assert.That(NetworkClient.spawnableObjects[21], Is.EqualTo(obj2));
        }

        [Test]
        public void ClearsExistingItemsFromDictionary()
        {
            // destroyed objects from old scene
            NetworkClient.spawnableObjects.Add(60, null);
            NetworkClient.spawnableObjects.Add(62, null);

            // active object
            NetworkIdentity obj1 = CreateSceneObject(61);
            NetworkClient.spawnableObjects.Add(61, obj1);

            // new disabled object - should be included too since netId == 0.
            NetworkIdentity obj2 = CreateSceneObject(63);
            obj2.gameObject.SetActive(false);

            NetworkClient.PrepareToSpawnSceneObjects();

            Assert.That(NetworkClient.spawnableObjects, Has.Count.EqualTo(2));
            Assert.IsFalse(NetworkClient.spawnableObjects.ContainsValue(null));
            Assert.IsTrue(NetworkClient.spawnableObjects.ContainsValue(obj2));
        }
    }
}
