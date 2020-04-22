using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkServerRuntimeTest : HostSetup
    {
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
