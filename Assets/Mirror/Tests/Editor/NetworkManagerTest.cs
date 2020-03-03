using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkManagerTest
    {
        GameObject gameObject;
        NetworkManager manager;

        [SetUp]
        public void SetupNetworkManager()
        {
            gameObject = new GameObject();
            manager = gameObject.AddComponent<NetworkManager>();
        }

        [TearDown]
        public void TearDownNetworkManager()
        {
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void VariableTest()
        {
            Assert.That(manager.dontDestroyOnLoad == true);
            Assert.That(manager.runInBackground == true);
            Assert.That(manager.startOnHeadless == true);
            Assert.That(manager.showDebugMessages == false);
            Assert.That(manager.serverTickRate == 30);
            Assert.That(manager.offlineScene == "");
            Assert.That(manager.networkAddress == "localhost");
            Assert.That(manager.maxConnections == 4);
            Assert.That(manager.autoCreatePlayer == true);
            Assert.That(manager.spawnPrefabs.Count == 0);
            Assert.That(manager.numPlayers == 0);
            Assert.That(manager.isNetworkActive == false);

            Assert.That(NetworkManager.networkSceneName == "");
            Assert.That(NetworkManager.startPositionIndex == 0);
            Assert.That(NetworkManager.startPositions.Count == 0);
            Assert.That(NetworkManager.isHeadless == false);
        }

        [Test]
        public void StartServerTest()
        {
            Assert.That(NetworkServer.active == false);

            manager.StartServer();

            Assert.That(manager.isNetworkActive == true);
            Assert.That(manager.mode == NetworkManagerMode.ServerOnly);
            Assert.That(NetworkServer.active == true);
        }

        [Test]
        public void StopServerTest()
        {
            manager.StartServer();
            manager.StopServer();

            Assert.That(manager.isNetworkActive == false);
            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void StartClientTest()
        {
            manager.StartClient();

            Assert.That(manager.isNetworkActive == true);
            Assert.That(manager.mode == NetworkManagerMode.ClientOnly);

            manager.StopClient();
        }

        [Test]
        public void StopClientTest()
        {
            manager.StartClient();
            manager.StopClient();

            Assert.That(manager.isNetworkActive == false);
            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void ShutdownTest()
        {
            manager.StartClient();
            NetworkManager.Shutdown();

            Assert.That(NetworkManager.startPositions.Count == 0);
            Assert.That(NetworkManager.startPositionIndex == 0);
            Assert.That(NetworkManager.startPositionIndex == 0);
            Assert.That(NetworkManager.singleton == null);
        }

        [Test]
        public void RegisterStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count == 0);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count == 1);
            Assert.That(NetworkManager.startPositions.Contains(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
        }

        [Test]
        public void UnRegisterStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count == 0);
            
            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count == 1);
            Assert.That(NetworkManager.startPositions.Contains(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count == 0);
        }

        [Test]
        public void GetStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count == 0);
            
            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count == 1);
            Assert.That(NetworkManager.startPositions.Contains(gameObject.transform));

            Assert.That(manager.GetStartPosition() == gameObject.transform);

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
        }
    }
}
