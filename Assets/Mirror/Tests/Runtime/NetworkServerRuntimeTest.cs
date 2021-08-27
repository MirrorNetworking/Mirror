using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    [TestFixture]
    public class NetworkServerRuntimeTest : MirrorPlayModeTest
    {
        [UnitySetUp]
        public override IEnumerator UnitySetUp()
        {
            yield return base.UnitySetUp();

            // start server & client and wait 1 frame
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyPlayerForConnectionTest()
        {
            // create spawned player
            CreateNetworkedAndSpawnPlayer(out GameObject player, out _, NetworkServer.localConnection);

            // destroy player for connection, wait 1 frame to unspawn and destroy
            NetworkServer.DestroyPlayerForConnection(NetworkServer.localConnection);
            yield return null;

            Assert.That(player == null, "Player should be destroyed with DestroyPlayerForConnection");
        }

        [UnityTest]
        public IEnumerator RemovePlayerForConnectionTest()
        {
            // create spawned player
            CreateNetworkedAndSpawnPlayer(out GameObject player, out _, NetworkServer.localConnection);

            // remove player for connection, wait 1 frame to unspawn
            NetworkServer.RemovePlayerForConnection(NetworkServer.localConnection, false);
            yield return null;

            Assert.That(player, Is.Not.Null, "Player should be not be destroyed");
            Assert.That(NetworkServer.localConnection.identity == null, "identity should be null");

            // respawn player
            NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, player);
            Assert.That(NetworkServer.localConnection.identity != null, "identity should not be null");
        }

        [UnityTest]
        public IEnumerator Shutdown_DestroysAllSpawnedPrefabs()
        {
            // setup
            NetworkServer.Listen(1);

            const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ValidPrefabAssetGuid));

            NetworkIdentity identity1 = SpawnPrefab(prefab);
            NetworkIdentity identity2 = SpawnPrefab(prefab);

            // shutdown, wait 1 frame for unity to destroy objects
            NetworkServer.Shutdown();
            yield return null;

            // check that objects were destroyed
            // need to use untiy `==` check
            Assert.IsTrue(identity1 == null);
            Assert.IsTrue(identity2 == null);

            Assert.That(NetworkServer.spawned, Is.Empty);
        }

        NetworkIdentity SpawnPrefab(GameObject prefab)
        {
            GameObject clone1 = GameObject.Instantiate(prefab);
            NetworkServer.Spawn(clone1);
            NetworkIdentity identity1 = clone1.GetComponent<NetworkIdentity>();
            Assert.IsTrue(NetworkServer.spawned.ContainsValue(identity1));
            return identity1;
        }

        [Test]
        public void Shutdown_DisablesAllSpawnedPrefabs()
        {
            // setup
            NetworkServer.Listen(1);

            // spawn two scene objects
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity1);
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity2);
            identity1.sceneId = (ulong)identity1.GetHashCode();
            identity2.sceneId = (ulong)identity2.GetHashCode();

            // test
            NetworkServer.Shutdown();

            // check that objects were disabled
            // need to use untiy `==` check
            Assert.IsTrue(identity1 != null);
            Assert.IsTrue(identity2 != null);
            Assert.IsFalse(identity1.gameObject.activeSelf);
            Assert.IsFalse(identity1.gameObject.activeSelf);

            Assert.That(NetworkServer.spawned, Is.Empty);
        }
    }
}
