using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    [TestFixture]
    public class NetworkServerWithHostRuntimeTest
    {
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();

            // start server and wait 1 frame
            NetworkServer.Listen(1);
            yield return null;

            // connection host and wait 1 frame
            NetworkClient.ConnectHost();
            NetworkClient.ConnectLocalServer();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            NetworkClient.DisconnectLocalServer();
            NetworkClient.Disconnect();
            NetworkClient.Shutdown();

            if (NetworkServer.active)
            {
                NetworkServer.Shutdown();
            }

            if (Transport.activeTransport != null)
            {
                GameObject.Destroy(Transport.activeTransport.gameObject);
            }
        }


        [UnityTest]
        public IEnumerator DisconnectTimeoutTest()
        {
            // Set high ping frequency so no NetworkPingMessage is generated
            NetworkTime.PingFrequency = 5f;

            // Set a short timeout for this test and enable disconnectInactiveConnections
            NetworkServer.disconnectInactiveTimeout = 1f;
            NetworkServer.disconnectInactiveConnections = true;

            GameObject remotePlayer = new GameObject("RemotePlayer", typeof(NetworkIdentity));
            const int remoteConnectionId = 1;
            const int localConnectionId = 0;
            NetworkConnectionToClient remoteConnection = new NetworkConnectionToClient(remoteConnectionId);
            NetworkServer.OnConnected(remoteConnection);
            NetworkServer.AddPlayerForConnection(remoteConnection, remotePlayer);

            // There's a host player from HostSetup + remotePlayer
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.IsTrue(NetworkServer.connections.ContainsKey(localConnectionId));
            Assert.IsTrue(NetworkServer.connections.ContainsKey(remoteConnectionId));

            // wait 2 seconds for remoteConnection to timeout as idle
            float endTime = Time.time + 2f;
            while (Time.time < endTime)
            {
                yield return new WaitForEndOfFrame();
                NetworkServer.Update();
            }

            // host client connection should still be alive
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.localConnection, Is.Not.Null);
            Assert.IsTrue(NetworkServer.connections.ContainsKey(localConnectionId));
            Assert.IsFalse(NetworkServer.connections.ContainsKey(remoteConnectionId));
        }
    }

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

            if (!NetworkServer.active)
                return;

            NetworkServer.Shutdown();
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
