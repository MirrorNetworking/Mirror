using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_PrepareToSpawnSceneObjects : ClientSceneTestsBase
    {
        NetworkIdentity CreateSceneObject(ulong sceneId)
        {
            GameObject runtimeObject = new GameObject("Runtime GameObject");
            NetworkIdentity networkIdentity = runtimeObject.AddComponent<NetworkIdentity>();
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            networkIdentity.sceneId = sceneId;

            _createdObjects.Add(runtimeObject);

            return networkIdentity;
        }


        [Test]
        public void AddsAllInactiveIdentitiesInSceneWithSceneIdToDictionary()
        {
            NetworkIdentity obj1 = CreateSceneObject(10);
            NetworkIdentity obj2 = CreateSceneObject(11);
            NetworkIdentity obj3 = CreateSceneObject(12);
            NetworkIdentity obj4 = CreateSceneObject(13);

            obj1.gameObject.SetActive(false);
            obj2.gameObject.SetActive(false);
            obj3.gameObject.SetActive(false);
            obj4.gameObject.SetActive(false);

            ClientScene.PrepareToSpawnSceneObjects();

            Assert.That(spawnableObjects, Has.Count.EqualTo(4));

            Assert.IsTrue(spawnableObjects.ContainsValue(obj1));
            Assert.IsTrue(spawnableObjects.ContainsValue(obj2));
            Assert.IsTrue(spawnableObjects.ContainsValue(obj3));
            Assert.IsTrue(spawnableObjects.ContainsValue(obj4));
        }

        [Test]
        public void DoesNotAddActiveObjectsToDictionary()
        {
            NetworkIdentity active1 = CreateSceneObject(30);
            NetworkIdentity active2 = CreateSceneObject(31);
            NetworkIdentity inactive1 = CreateSceneObject(32);
            NetworkIdentity inactive2 = CreateSceneObject(33);

            active1.gameObject.SetActive(true);
            active2.gameObject.SetActive(true);
            inactive1.gameObject.SetActive(false);
            inactive2.gameObject.SetActive(false);

            ClientScene.PrepareToSpawnSceneObjects();

            Assert.That(spawnableObjects, Has.Count.EqualTo(2));

            Assert.IsTrue(spawnableObjects.ContainsValue(inactive1));
            Assert.IsTrue(spawnableObjects.ContainsValue(inactive2));

            Assert.IsFalse(spawnableObjects.ContainsValue(active1));
            Assert.IsFalse(spawnableObjects.ContainsValue(active2));
        }

        [Test]
        public void DoesNotAddObjectsWithNoSceneId()
        {
            NetworkIdentity noId1 = CreateSceneObject(0);
            NetworkIdentity noId2 = CreateSceneObject(0);
            NetworkIdentity hasId1 = CreateSceneObject(40);
            NetworkIdentity hasId2 = CreateSceneObject(41);

            noId1.gameObject.SetActive(false);
            noId2.gameObject.SetActive(false);
            hasId1.gameObject.SetActive(false);
            hasId2.gameObject.SetActive(false);

            ClientScene.PrepareToSpawnSceneObjects();

            Assert.IsTrue(spawnableObjects.ContainsValue(hasId1));
            Assert.IsTrue(spawnableObjects.ContainsValue(hasId2));

            Assert.IsFalse(spawnableObjects.ContainsValue(noId1));
            Assert.IsFalse(spawnableObjects.ContainsValue(noId2));
        }

        [Test]
        public void AddsIdentitiesToDictionaryUsingSceneId()
        {
            NetworkIdentity obj1 = CreateSceneObject(20);
            NetworkIdentity obj2 = CreateSceneObject(21);
            obj1.gameObject.SetActive(false);
            obj2.gameObject.SetActive(false);

            ClientScene.PrepareToSpawnSceneObjects();

            Assert.IsTrue(spawnableObjects.ContainsKey(20));
            Assert.That(spawnableObjects[20], Is.EqualTo(obj1));

            Assert.IsTrue(spawnableObjects.ContainsKey(21));
            Assert.That(spawnableObjects[21], Is.EqualTo(obj2));
        }

        [Test]
        public void ClearsExistingItemsFromDictionary()
        {
            // destroyed objects from old scene
            spawnableObjects.Add(60, null);
            spawnableObjects.Add(62, null);

            // active object
            NetworkIdentity obj1 = CreateSceneObject(61);
            spawnableObjects.Add(61, obj1);

            // new disabled object
            NetworkIdentity obj2 = CreateSceneObject(63);
            obj2.gameObject.SetActive(false);

            ClientScene.PrepareToSpawnSceneObjects();

            Assert.That(spawnableObjects, Has.Count.EqualTo(1));
            Assert.IsFalse(spawnableObjects.ContainsValue(null));
            Assert.IsTrue(spawnableObjects.ContainsValue(obj2));
        }
    }
}
