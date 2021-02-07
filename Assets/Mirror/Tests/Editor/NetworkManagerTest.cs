using System;
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
            gameObject.AddComponent<MemoryTransport>();
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
            Assert.That(manager.dontDestroyOnLoad, Is.True);
            Assert.That(manager.runInBackground, Is.True);
            Assert.That(manager.autoStartServerBuild, Is.True);
            Assert.That(manager.showDebugMessages, Is.False);
            Assert.That(manager.serverTickRate, Is.EqualTo(30));
            Assert.That(manager.offlineScene, Is.Empty);
            Assert.That(manager.networkAddress, Is.EqualTo("localhost"));
            Assert.That(manager.maxConnections, Is.EqualTo(100));
            Assert.That(manager.autoCreatePlayer, Is.True);
            Assert.That(manager.spawnPrefabs, Is.Empty);
            Assert.That(manager.numPlayers, Is.Zero);
            Assert.That(manager.isNetworkActive, Is.False);

            Assert.That(NetworkManager.networkSceneName, Is.Empty);
            Assert.That(NetworkManager.startPositionIndex, Is.Zero);
            Assert.That(NetworkManager.startPositions, Is.Empty);
        }

        [Test]
        public void StartServerTest()
        {
            Assert.That(NetworkServer.active, Is.False);

            manager.StartServer();

            Assert.That(manager.isNetworkActive, Is.True);
            Assert.That(manager.mode == NetworkManagerMode.ServerOnly);
            Assert.That(NetworkServer.active, Is.True);
        }

        [Test]
        public void StopServerTest()
        {
            manager.StartServer();
            manager.StopServer();

            Assert.That(manager.isNetworkActive, Is.False);
            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void StartClientTest()
        {
            manager.StartClient();

            Assert.That(manager.isNetworkActive, Is.True);
            Assert.That(manager.mode == NetworkManagerMode.ClientOnly);

            manager.StopClient();
        }

        [Test]
        public void StopClientTest()
        {
            manager.StartClient();
            manager.StopClient();

            Assert.That(manager.isNetworkActive, Is.False);
            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void ShutdownTest()
        {
            manager.StartClient();
            NetworkManager.Shutdown();

            Assert.That(NetworkManager.startPositions.Count, Is.Zero);
            Assert.That(NetworkManager.startPositionIndex, Is.Zero);
            Assert.That(NetworkManager.startPositionIndex, Is.Zero);
            Assert.That(NetworkManager.singleton, Is.Null);
        }

        [Test]
        public void RegisterStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
        }

        [Test]
        public void UnRegisterStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);
        }

        [Test]
        public void GetStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(gameObject.transform));

            Assert.That(manager.GetStartPosition(), Is.SameAs(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
        }

        [Test]
        public void StartClientUriTest()
        {
            UriBuilder uriBuilder = new UriBuilder()
            {
                Host = "localhost",
                Scheme = "local"
            };
            manager.StartClient(uriBuilder.Uri);

            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.ClientOnly));
            Assert.That(manager.isNetworkActive, Is.EqualTo(true));
            Assert.That(manager.networkAddress, Is.EqualTo(uriBuilder.Uri.Host));
        }
    }
}
