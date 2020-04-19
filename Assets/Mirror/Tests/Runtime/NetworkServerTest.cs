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

        [UnityTest]
        public IEnumerator DisconnectTimeoutTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

            // Set high ping frequency so no NetworkPingMessage is generated
            NetworkTime.PingFrequency = 20f;

            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            NetworkClient.ConnectHost();
            ULocalConnectionToClient localConnection = NetworkServer.localConnection as ULocalConnectionToClient;
            Assert.That(NetworkServer.localConnection, Is.Not.Null);
            localConnection.serverIdleTimeout = 5f;

            GameObject RemotePlayer = new GameObject("RemotePlayer", typeof(NetworkIdentity));
            NetworkConnectionToClient remoteConnection = new NetworkConnectionToClient(1);
            remoteConnection.serverIdleTimeout = 5f;
            NetworkServer.OnConnected(remoteConnection);
            NetworkServer.AddPlayerForConnection(remoteConnection, RemotePlayer);

            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // wait 10 seconds for conn43 to timeout as idle
            float endTime = Time.time + 10;
            while (endTime > Time.time)
            {
                NetworkServer.Update();
                yield return null;
            }

            // host client connection should still be alive
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));
        }
    }
}
