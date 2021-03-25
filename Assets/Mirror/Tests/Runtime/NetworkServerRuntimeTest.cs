using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    [TestFixture]
    public class NetworkServerRuntimeTest
    {
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();
            // start server and wait 1 frame
            NetworkServer.Listen(1);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (Transport.activeTransport != null)
            {
                GameObject.Destroy(Transport.activeTransport.gameObject);
            }

            if (NetworkServer.active)
            {
                NetworkServer.Shutdown();
            }
        }

        [UnityTest]
        public IEnumerator DestroyPlayerForConnectionTest()
        {
            GameObject player = new GameObject("testPlayer", typeof(NetworkIdentity));
            NetworkConnectionToClient conn = new NetworkConnectionToClient(1, false, 0);

            NetworkServer.AddPlayerForConnection(conn, player);

            // allow 1 frame to spawn object
            yield return null;

            NetworkServer.DestroyPlayerForConnection(conn);

            // allow 1 frame to unspawn object and for unity to destroy object
            yield return null;

            Assert.That(player == null, "Player should be destroyed with DestroyPlayerForConnection");
        }

        [UnityTest]
        public IEnumerator RemovePlayerForConnectionTest()
        {
            GameObject player = new GameObject("testPlayer", typeof(NetworkIdentity));
            NetworkConnectionToClient conn = new NetworkConnectionToClient(1, false, 0);

            NetworkServer.AddPlayerForConnection(conn, player);

            // allow 1 frame to spawn object
            yield return null;

            NetworkServer.RemovePlayerForConnection(conn, false);

            // allow 1 frame to unspawn object
            yield return null;

            Assert.That(player, Is.Not.Null, "Player should be not be destroyed");
            Assert.That(conn.identity == null, "identity should be null");

            // respawn player
            NetworkServer.AddPlayerForConnection(conn, player);
            Assert.That(conn.identity != null, "identity should not be null");
        }

        [UnityTest]
        public IEnumerator SetClientReadyAndNotReadyCheckObserversOfNetworkIdentityTest()
        {
            // add connection
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
            NetworkServer.AddConnection(connection);
            Assert.That(connection.isReady, Is.False);
            connection.isAuthenticated = true;

            // set client ready
            NetworkServer.SetClientReady(connection);
            Assert.That(connection.isReady, Is.True);

            // spawn network identity on the server
            GameObject gameObject = new GameObject();
            NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
            NetworkServer.Spawn(gameObject);

            // allow 1 frame to spawn object
            yield return null;

            // connection should now automatically be observing the spawned identity
            Assert.That(connection.observing.Contains(networkIdentity), Is.True);
            Assert.That(networkIdentity.observers.ContainsKey(connection.connectionId), Is.True);
            Assert.That(networkIdentity.observers[connection.connectionId], Is.EqualTo(connection));

            // setting client to not ready
            // note: we need runtime test instead of edit mode test
            //       because otherwise gameobject.scene.buildIndex is not valid,
            //       and buildIndex is needed to check if observer should be removed
            NetworkServer.SetClientNotReady(connection);
            Assert.That(connection.isReady, Is.False);

            // client that is not ready should not observe the spawned identity
            Assert.That(connection.observing.Contains(networkIdentity), Is.False);
            Assert.That(networkIdentity.observers.ContainsKey(connection.connectionId), Is.False);

            // clean up
            NetworkServer.Destroy(gameObject);
            NetworkIdentity.spawned.Clear();
            NetworkServer.Shutdown();
        }

        [UnityTest]
        public IEnumerator SetClientReadyAndNotReadyCheckObserversOfDontDestroyOnLoadNetworkIdentityTest()
        {
            // add connection
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
            NetworkServer.AddConnection(connection);
            Assert.That(connection.isReady, Is.False);
            connection.isAuthenticated = true;

            // set client ready
            NetworkServer.SetClientReady(connection);
            Assert.That(connection.isReady, Is.True);

            // spawn network identity on the server
            // note: we need runtime test instead of edit mode test
            //       because otherwise DontDestroyOnLoad won't work
            GameObject gameObject = new GameObject();
            NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
            NetworkServer.Spawn(gameObject);
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            // allow 1 frame to spawn object
            yield return null;

            // connection should now automatically be observing the spawned identity
            Assert.That(connection.observing.Contains(networkIdentity), Is.True);
            Assert.That(networkIdentity.observers.ContainsKey(connection.connectionId), Is.True);
            Assert.That(networkIdentity.observers[connection.connectionId], Is.EqualTo(connection));

            // setting client to not ready
            NetworkServer.SetClientNotReady(connection);
            Assert.That(connection.isReady, Is.False);

            // client that is not ready should still be observing network identity, since it's "DontDestroyOnLoad"
            Assert.That(connection.observing.Contains(networkIdentity), Is.True);
            Assert.That(networkIdentity.observers.ContainsKey(connection.connectionId), Is.True);
            Assert.That(networkIdentity.observers[connection.connectionId], Is.EqualTo(connection));

            // clean up
            NetworkIdentity.spawned.Clear();
            NetworkServer.Shutdown();
            // destroy the test gameobject AFTER server was stopped.
            // otherwise isServer is true in OnDestroy, which means it would try
            // to call Destroy(go). but we need to use DestroyImmediate in
            // Editor
            GameObject.DestroyImmediate(gameObject);
        }

        [UnityTest]
        public IEnumerator Shutdown_DestroysAllSpawnedPrefabs()
        {
            // setup
            NetworkServer.Listen(1);

            const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ValidPrefabAssetGuid));

            NetworkIdentity identity1 = spawnPrefab(prefab);
            NetworkIdentity identity2 = spawnPrefab(prefab);


            // test
            NetworkServer.Shutdown();

            // wait 1 frame for unity to destroy objects
            yield return null;

            // check that objects were destroyed
            // need to use untiy `==` check
            Assert.IsTrue(identity1 == null);
            Assert.IsTrue(identity2 == null);

            Assert.That(NetworkIdentity.spawned, Is.Empty);
        }

        static NetworkIdentity spawnPrefab(GameObject prefab)
        {
            GameObject clone1 = GameObject.Instantiate(prefab);
            NetworkServer.Spawn(clone1);
            NetworkIdentity identity1 = clone1.GetComponent<NetworkIdentity>();
            Assert.IsTrue(NetworkIdentity.spawned.ContainsValue(identity1));
            return identity1;
        }

        [Test]
        public void Shutdown_DisablesAllSpawnedPrefabs()
        {
            // setup
            NetworkServer.Listen(1);

            NetworkIdentity identity1 = spawnSceneObject("test 1");
            NetworkIdentity identity2 = spawnSceneObject("test 2");


            // test
            NetworkServer.Shutdown();

            // check that objects were disabled
            // need to use untiy `==` check
            Assert.IsTrue(identity1 != null);
            Assert.IsTrue(identity2 != null);
            Assert.IsFalse(identity1.gameObject.activeSelf);
            Assert.IsFalse(identity1.gameObject.activeSelf);

            Assert.That(NetworkIdentity.spawned, Is.Empty);

            // cleanup
            GameObject.DestroyImmediate(identity1.gameObject);
            GameObject.DestroyImmediate(identity2.gameObject);
        }

        static NetworkIdentity spawnSceneObject(string Name)
        {
            GameObject obj = new GameObject(Name, typeof(NetworkIdentity));
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            obj.SetActive(false);
            if (identity.sceneId == 0) { identity.sceneId = (ulong)obj.GetHashCode(); }
            NetworkServer.Spawn(obj);

            Assert.IsTrue(NetworkIdentity.spawned.ContainsValue(identity));
            return identity;
        }
    }
}
