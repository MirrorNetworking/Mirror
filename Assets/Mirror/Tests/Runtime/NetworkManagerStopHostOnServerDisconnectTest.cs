using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    class NetworkManagerOnServerDisconnect : NetworkManager
    {
        public int called;
        public override void OnServerDisconnect(NetworkConnection conn) { ++called; }
    }

    [TestFixture]
    public class NetworkManagerStopHostOnServerDisconnectTest
    {
        GameObject gameObject;
        GameObject playerPrefab;
        NetworkManagerOnServerDisconnect manager;
        MemoryTransport transport;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            transport= gameObject.AddComponent<MemoryTransport>();
            manager = gameObject.AddComponent<NetworkManagerOnServerDisconnect>();
            playerPrefab = new GameObject("player");
            NetworkIdentity id = playerPrefab.AddComponent<NetworkIdentity>();
            id.assetId = Guid.NewGuid();
            id.sceneId = 0;
            manager.playerPrefab = playerPrefab;
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(gameObject);
            GameObject.DestroyImmediate(playerPrefab);
            NetworkServer.Shutdown();
            NetworkClient.Shutdown();
            Transport.activeTransport = null;
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/1515
        [UnityTest]
        public IEnumerator StopHostCallsOnServerDisconnectForHostClient()
        {
            // wait for awake to be called before setting activeTransport
            yield return null;
            Transport.activeTransport = transport;

            // OnServerDisconnect is always called when a client disconnects.
            // it should also be called for the host client when we stop the host
            Assert.That(manager.called, Is.EqualTo(0));
            manager.StartHost();
            manager.StopHost();
            Assert.That(manager.called, Is.EqualTo(1));
        }
    }
}
