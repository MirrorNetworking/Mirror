using System.Collections;
using NUnit.Framework;
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
            NetworkConnectionToClient conn = new NetworkConnectionToClient(1);

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
            NetworkConnectionToClient conn = new NetworkConnectionToClient(1);

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
    }
}
