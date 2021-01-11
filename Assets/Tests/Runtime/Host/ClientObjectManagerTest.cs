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
        public void OnSpawnAssetSceneIDFailureExceptionTest()
        {
            var msg = new SpawnMessage();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.OnSpawn(msg);
            });

            Assert.That(ex.Message, Is.EqualTo("OnObjSpawn netId: " + msg.netId + " has invalid asset Id"));
        }

        [UnityTest]
        public IEnumerator GetPrefabTest() => UniTask.ToCoroutine(async () =>
        {
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            var identity = prefabObject.GetComponent<NetworkIdentity>();

            clientObjectManager.RegisterPrefab(identity, guid);

            await UniTask.Delay(1);

            NetworkIdentity result = clientObjectManager.GetPrefab(guid);

            Assert.That(result, Is.SameAs(identity));

            Object.Destroy(prefabObject);
        });

        [Test]
        public void RegisterPrefabDelegateEmptyIdentityExceptionTest()
        {
            GameObject prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            NetworkIdentity identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.Empty;

            Assert.Throws<InvalidOperationException>(() =>
            {
                clientObjectManager.RegisterPrefab(identity, TestSpawnDelegate, TestUnspawnDelegate);
            });

            Object.Destroy(prefabObject);
        }

        [Test]
        public void RegisterPrefabDelegateTest()
        {
            GameObject prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            NetworkIdentity identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.NewGuid();

            clientObjectManager.RegisterPrefab(identity, TestSpawnDelegate, TestUnspawnDelegate);

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

            clientObjectManager.RegisterPrefab(identity, TestSpawnDelegate, TestUnspawnDelegate);

            Assert.That(clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            clientObjectManager.UnregisterPrefab(identity);

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

            clientObjectManager.RegisterPrefab(identity, TestSpawnDelegate, TestUnspawnDelegate);

            Assert.That(clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            clientObjectManager.UnregisterSpawnHandler(identity.AssetId);

            Assert.That(!clientObjectManager.spawnHandlers.ContainsKey(identity.AssetId));
            Assert.That(!clientObjectManager.unspawnHandlers.ContainsKey(identity.AssetId));

            Object.Destroy(prefabObject);
        }

        NetworkIdentity TestSpawnDelegate(SpawnMessage msg)
        {
            return new GameObject("spawned", typeof(NetworkIdentity)).GetComponent<NetworkIdentity>();
        }

        void TestUnspawnDelegate(NetworkIdentity identity)
        {
            Object.Destroy(identity.gameObject);
        }

        [Test]
        public void GetPrefabEmptyNullTest()
        {
            NetworkIdentity result = clientObjectManager.GetPrefab(Guid.Empty);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetPrefabNotFoundNullTest()
        {
            NetworkIdentity result = clientObjectManager.GetPrefab(GenerateUniqueGuid());

            Assert.That(result, Is.Null);
        }

        //Used to ensure the test has a unique non empty guid
        Guid GenerateUniqueGuid()
        {
            Guid testGuid = Guid.NewGuid();

            if (clientObjectManager.prefabs.ContainsKey(testGuid))
            {
                testGuid = GenerateUniqueGuid();
            }
            return testGuid;    
        }

        [Test]
        public void ReplacePlayerHostTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            clientObjectManager.RegisterPrefab(replacementIdentity);

            serverObjectManager.ReplacePlayerForConnection(server.LocalConnection, client, playerReplacement, true);

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
