using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using NSubstitute;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkClientTest : HostSetup<MockComponent>
    {
        GameObject playerReplacement;

        [Test]
        public void IsConnectedTest()
        {
            Assert.That(client.IsConnected);
        }

        [Test]
        public void ConnectionTest()
        {
            Assert.That(client.Connection != null);
        }

        [Test]
        public void CurrentTest()
        {
            Assert.That(NetworkClient.Current == null);
        }

        [Test]
        public void RegisterPrefabExceptionTest()
        {
            var gameObject = new GameObject();
            Assert.Throws<InvalidOperationException>(() =>
            {
                client.RegisterPrefab(gameObject);
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
                client.RegisterPrefab(gameObject, guid);
            });
            Object.Destroy(gameObject);
        }

        [Test]
        public void OnSpawnAssetSceneIDFailureExceptionTest()
        {
            var msg = new SpawnMessage();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                client.OnSpawn(msg);
            });

            Assert.That(ex.Message, Is.EqualTo("OnObjSpawn netId: " + msg.netId + " has invalid asset Id"));
        }

        [Test]
        public void UnregisterPrefabExceptionTest()
        {
            var gameObject = new GameObject();
            Assert.Throws<InvalidOperationException>(() =>
            {
                client.UnregisterPrefab(gameObject);
            });
            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator GetPrefabTest() => UniTask.ToCoroutine(async () =>
        {
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));

            client.RegisterPrefab(prefabObject, guid);

            await UniTask.Delay(1);

            GameObject result = client.GetPrefab(guid);

            Assert.That(result, Is.SameAs(prefabObject));

            Object.Destroy(prefabObject);
        });

        [Test]
        public void ReplacePlayerHostTest()
        {
            playerReplacement = new GameObject("replacement", typeof(NetworkIdentity));
            NetworkIdentity replacementIdentity = playerReplacement.GetComponent<NetworkIdentity>();
            replacementIdentity.AssetId = Guid.NewGuid();
            client.RegisterPrefab(playerReplacement);

            server.ReplacePlayerForConnection(server.LocalConnection, client, playerReplacement, true);

            Assert.That(server.LocalClient.Connection.Identity, Is.EqualTo(replacementIdentity));
        }

        [UnityTest]
        public IEnumerator ObjectHideTest() => UniTask.ToCoroutine(async () =>
        {
            client.OnObjectHide(new ObjectHideMessage
            {
                netId = identity.NetId
            });

            await AsyncUtil.WaitUntilWithTimeout(() => identity == null);

            Assert.That(identity == null);
        });

        [UnityTest]
        public IEnumerator ObjectDestroyTest() => UniTask.ToCoroutine(async () =>
        {
            client.OnObjectDestroy(new ObjectDestroyMessage
            {
                netId = identity.NetId
            });

            await AsyncUtil.WaitUntilWithTimeout(() => identity == null);

            Assert.That(identity == null);
        });

        [Test]
        public void GetNewConnectionTest()
        {
            Assert.That(client.GetNewConnection(Substitute.For<IConnection>()), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ClientDisconnectTest() => UniTask.ToCoroutine(async () =>
        {
            client.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => client.connectState == ConnectState.Disconnected);
            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);
        });

        [Test]
        public void RegisterPrefabDelegateNoIdentityExceptionTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                client.RegisterPrefab(new GameObject(), TestSpawnDelegate, TestUnspawnDelegate);
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
                client.RegisterPrefab(prefabObject, TestSpawnDelegate, TestUnspawnDelegate);
            });

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
            GameObject result = client.GetPrefab(Guid.Empty);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void SpawnSceneObjectTest()
        {
            //Setup new scene object for test
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));
            var identity = prefabObject.GetComponent<NetworkIdentity>();
            identity.AssetId = guid;
            client.spawnableObjects.Add(0, identity);

            NetworkIdentity result = client.SpawnSceneObject(new SpawnMessage { sceneId = 0, assetId = guid });

            Assert.That(result, Is.SameAs(identity));

            Object.Destroy(prefabObject);
        }
    }
}
