using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class TestListenerBehaviour : MonoBehaviour
    {
        /*
            **Note**

            If object is destroyed then both OnDisable and OnDestroy will be called

         */

        public event System.Action onDestroyCalled;
        public event System.Action onDisableCalled;

        public void OnDisable()
        {
            onDisableCalled?.Invoke();
        }
        private void OnDestroy()
        {
            onDestroyCalled?.Invoke();
        }
    }
    public class ClientSceneTests_DestroyAllClientObjects
    {
        readonly List<GameObject> _createdObjects = new List<GameObject>();
        protected Dictionary<uint, NetworkIdentity> spawned => NetworkIdentity.spawned;
        protected Dictionary<Guid, UnSpawnDelegate> unspawnHandlers => ClientScene.unspawnHandlers;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in _createdObjects)
            {
                GameObject.DestroyImmediate(item.gameObject);
            }
            _createdObjects.Clear();

            spawned.Clear();
            unspawnHandlers.Clear();
        }

        TestListenerBehaviour CreateAndAddObject(uint netId, ulong sceneId)
        {
            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.netId = netId;
            identity.sceneId = sceneId;
            TestListenerBehaviour listener = go.AddComponent<TestListenerBehaviour>();
            spawned.Add(netId, identity);
            return listener;
        }

        [UnityTest]
        public IEnumerator DestroysAllNetworkPrefabsInScene()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(10001, 0);
            TestListenerBehaviour listener2 = CreateAndAddObject(10002, 0);
            TestListenerBehaviour listener3 = CreateAndAddObject(10003, 0);

            int destroyCalled1 = 0;
            int destroyCalled2 = 0;
            int destroyCalled3 = 0;

            listener1.onDestroyCalled += () => destroyCalled1++;
            listener2.onDestroyCalled += () => destroyCalled2++;
            listener3.onDestroyCalled += () => destroyCalled3++;

            ClientScene.DestroyAllClientObjects();

            // wait for frame to make sure unity events are called
            yield return null;

            Assert.That(destroyCalled1, Is.EqualTo(1));
            Assert.That(destroyCalled2, Is.EqualTo(1));
            Assert.That(destroyCalled3, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisablesAllNetworkSceneObjectsInScene()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(20001, 101);
            TestListenerBehaviour listener2 = CreateAndAddObject(20002, 102);
            TestListenerBehaviour listener3 = CreateAndAddObject(20003, 103);

            int disableCalled1 = 0;
            int disableCalled2 = 0;
            int disableCalled3 = 0;

            listener1.onDisableCalled += () => disableCalled1++;
            listener2.onDisableCalled += () => disableCalled2++;
            listener3.onDisableCalled += () => disableCalled3++;

            int destroyCalled1 = 0;
            int destroyCalled2 = 0;
            int destroyCalled3 = 0;

            listener1.onDestroyCalled += () => destroyCalled1++;
            listener2.onDestroyCalled += () => destroyCalled2++;
            listener3.onDestroyCalled += () => destroyCalled3++;

            ClientScene.DestroyAllClientObjects();

            // wait for frame to make sure unity events are called
            yield return null;

            Assert.That(disableCalled1, Is.EqualTo(1));
            Assert.That(disableCalled2, Is.EqualTo(1));
            Assert.That(disableCalled3, Is.EqualTo(1));

            Assert.That(destroyCalled1, Is.EqualTo(0), "Scene objects should not be destroyed");
            Assert.That(destroyCalled2, Is.EqualTo(0), "Scene objects should not be destroyed");
            Assert.That(destroyCalled3, Is.EqualTo(0), "Scene objects should not be destroyed");
        }

        [Test]
        public void CallsUnspawnHandlerInsteadOfDestroy()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(30001, 0);
            TestListenerBehaviour listener2 = CreateAndAddObject(30002, 0);
            TestListenerBehaviour listener3 = CreateAndAddObject(30003, 0);

            Guid guid1 = Guid.NewGuid();
            Guid guid2 = Guid.NewGuid();
            Guid guid3 = Guid.NewGuid();

            int unspawnCalled1 = 0;
            int unspawnCalled2 = 0;
            int unspawnCalled3 = 0;

            unspawnHandlers.Add(guid1, x => unspawnCalled1++);
            unspawnHandlers.Add(guid2, x => unspawnCalled2++);
            unspawnHandlers.Add(guid3, x => unspawnCalled3++);
            listener1.GetComponent<NetworkIdentity>().assetId = guid1;
            listener2.GetComponent<NetworkIdentity>().assetId = guid2;
            listener3.GetComponent<NetworkIdentity>().assetId = guid3;

            int disableCalled1 = 0;
            int disableCalled2 = 0;
            int disableCalled3 = 0;

            listener1.onDisableCalled += () => disableCalled1++;
            listener2.onDisableCalled += () => disableCalled2++;
            listener3.onDisableCalled += () => disableCalled3++;

            ClientScene.DestroyAllClientObjects();

            Assert.That(unspawnCalled1, Is.EqualTo(1));
            Assert.That(unspawnCalled2, Is.EqualTo(1));
            Assert.That(unspawnCalled3, Is.EqualTo(1));

            Assert.That(disableCalled1, Is.EqualTo(0), "Object with UnspawnHandler should not be destroyed");
            Assert.That(disableCalled2, Is.EqualTo(0), "Object with UnspawnHandler should not be destroyed");
            Assert.That(disableCalled3, Is.EqualTo(0), "Object with UnspawnHandler should not be destroyed");
        }

        [Test]
        public void ClearsSpawnedList()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(30001, 0);
            TestListenerBehaviour listener2 = CreateAndAddObject(30002, 0);
            TestListenerBehaviour listener3 = CreateAndAddObject(30003, 0);

            ClientScene.DestroyAllClientObjects();

            Assert.That(spawned, Is.Empty);
        }
    }
}
