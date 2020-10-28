using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
{
    [TestFixture]
    public class ClientObjectManagerTest : HostSetup<MockComponent>
    {
        GameObject playerReplacement;

        [Test]
        public void RegisterPrefabExceptionTest()
        {
            var gameObject = new GameObject();
            Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.RegisterPrefab(gameObject);
            });
            Object.Destroy(gameObject);
        }

        [Test]
        public void RegisterPrefabGuidExceptionTest()
        {
            var guid = Guid.NewGuid();
            var gameObject = new GameObject();

            Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.RegisterPrefab(gameObject, guid);
            });
            Object.Destroy(gameObject);
        }

        [Test]
        public void OnSpawnAssetSceneIDFailureExceptionTest()
        {
            var msg = new SpawnMessage();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.OnSpawn(msg);
            });

            Assert.That(ex.Message, Is.EqualTo("OnObjSpawn netId: " + msg.netId + " has invalid asset Id"));
        }

        [Test]
        public void UnregisterPrefabExceptionTest()
        {
            var gameObject = new GameObject();
            Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.UnregisterPrefab(gameObject);
            });
            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator GetPrefabTest() => UniTask.ToCoroutine(async () =>
        {
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));

            clientObjectManager.RegisterPrefab(prefabObject, guid);

            await UniTask.Delay(1);

            GameObject result = clientObjectManager.GetPrefab(guid);

            Assert.That(result, Is.SameAs(prefabObject));

            Object.Destroy(prefabObject);
        });

        [Test]
        public void RegisterPrefabDelegateNoIdentityExceptionTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.RegisterPrefab(new GameObject(), TestSpawnDelegate, TestUnspawnDelegate);
            });
        }

        [Test]
        public void RegisterPrefabDelegateEmptyIdentityExceptionTest()
        {
            GameObject prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            NetworkIdentity identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.Empty;

            Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.RegisterPrefab(prefabObject, TestSpawnDelegate, TestUnspawnDelegate);
            });

            Object.Destroy(prefabObject);
        }

        [Test]
        public void RegisterPrefabDelegateTest()
        {
            GameObject prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            NetworkIdentity identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.NewGuid();

            clientObjectManager.RegisterPrefab(prefabObject, TestSpawnDelegate, TestUnspawnDelegate);

            Assert.That(clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            Object.Destroy(prefabObject);
        }

        [Test]
        public void UnregisterPrefabTest()
        {
            GameObject prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            NetworkIdentity identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.NewGuid();

            clientObjectManager.RegisterPrefab(prefabObject, TestSpawnDelegate, TestUnspawnDelegate);

            Assert.That(clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            clientObjectManager.UnregisterPrefab(prefabObject);

            Assert.That(!clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(!clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            Object.Destroy(prefabObject);
        }

        [Test]
        public void UnregisterSpawnHandlerTest()
        {
            GameObject prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            NetworkIdentity identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.NewGuid();

            clientObjectManager.RegisterPrefab(prefabObject, TestSpawnDelegate, TestUnspawnDelegate);

            Assert.That(clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            clientObjectManager.UnregisterSpawnHandler(identity.AssetId);

            Assert.That(!clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(!clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            Object.Destroy(prefabObject);
        }

        GameObject TestSpawnDelegate(Vector3 position, Guid assetId)
        {
            return new GameObject();
        }

        void TestUnspawnDelegate(GameObject gameObject)
        {
            Debug.Log("Just testing. Nothing to see here");
        }

        [Test]
        public void GetPrefabEmptyNullTest()
        {
            GameObject result = clientObjectManager.GetPrefab(Guid.Empty);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ReplacePlayerHostTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            clientObjectManager.RegisterPrefab(playerReplacement);

            server.ReplacePlayerForConnection(server.LocalConnection, client, playerReplacement, true);

            Assert.That(server.LocalClient.Connection.Identity, Is.EqualTo(replacementIdentity));
        }

        [UnityTest]
        public IEnumerator ObjectHideTest() => UniTask.ToCoroutine(async () =>
        {
            clientObjectManager.OnObjectHide(new ObjectHideMessage
            {
                netId = identity.NetId
            });

            await AsyncUtil.WaitUntilWithTimeout(() => identity == null);

            Assert.That(identity == null);
        });

        [UnityTest]
        public IEnumerator ObjectDestroyTest() => UniTask.ToCoroutine(async () =>
        {
            clientObjectManager.OnObjectDestroy(new ObjectDestroyMessage
            {
                netId = identity.NetId
            });

            await AsyncUtil.WaitUntilWithTimeout(() => identity == null);

            Assert.That(identity == null);
        });

        [Test]
        public void SpawnSceneObjectTest()
        {
            //Setup new scene object for test
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            var identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = guid;
            clientObjectManager.spawnableObjects.Add(0, identity);

            NetworkIdentity result = clientObjectManager.SpawnSceneObject(new SpawnMessage { sceneId = 0, assetId = guid });

            Assert.That(result, Is.SameAs(identity));

            Object.Destroy(prefabObject);
        }
    }
}
