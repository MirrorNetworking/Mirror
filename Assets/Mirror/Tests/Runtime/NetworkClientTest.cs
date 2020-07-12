using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

using static Mirror.Tests.AsyncUtil;
using System.Threading.Tasks;

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
        public IEnumerator GetPrefabTest() => RunAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var prefabObject = new GameObject("prefab", typeof(NetworkIdentity));

            client.RegisterPrefab(prefabObject, guid);

            await Task.Delay(1);

            client.GetPrefab(guid, out GameObject result);

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
        public IEnumerator ObjectHideTest() => RunAsync(async () =>
        {
            client.OnObjectHide(new ObjectHideMessage
            {
                netId = identity.NetId
            });

            await Task.Delay(1);

            Assert.That(identity == null);
        });

        [UnityTest]
        public IEnumerator ObjectDestroyTest() => RunAsync(async () =>
        {
            client.OnObjectDestroy(new ObjectDestroyMessage
            {
                netId = identity.NetId
            });

            await Task.Delay(1);

            Assert.That(identity == null);
        });
    }
}
