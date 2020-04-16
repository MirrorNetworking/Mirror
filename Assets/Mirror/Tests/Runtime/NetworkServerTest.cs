using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkServerRuntimeTest
    {
        private GameObject transportGameObject;

        [SetUp]
        public void SetUp()
        {
            transportGameObject = new GameObject("Transport");
            Transport.activeTransport = transportGameObject.AddComponent<MemoryTransport>();
        }

        [TearDown]
        public void TearDown()
        {
            // reset all state
            NetworkServer.Shutdown();

            Transport.activeTransport = null;
            Object.Destroy(transportGameObject);
        }


        [UnityTest]
        public IEnumerator DestroyPlayerForConnectionTest()
        {
            NetworkServer.Listen(1);

            GameObject player = new GameObject("testPlayer", typeof(NetworkIdentity));
            NetworkConnectionToClient conn = new NetworkConnectionToClient(1);

            NetworkServer.AddPlayerForConnection(conn, player);

            NetworkServer.DestroyPlayerForConnection(conn);

            // takes 1 frame for unity to destroy object
            yield return null;

            Assert.That(player == null, "Player should be destroyed with DestroyPlayerForConnection");
        }
    }
}
