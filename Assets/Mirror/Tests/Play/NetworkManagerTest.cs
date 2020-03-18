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
            manager.client = gameObject.GetComponent<NetworkClient>();
            manager.server = gameObject.GetComponent<NetworkServer>();
        }

        [TearDown]
        public void TearDownNetworkManager()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void VariableTest()
        {
            Assert.That(manager.dontDestroyOnLoad, Is.True);
            Assert.That(manager.runInBackground, Is.True);
            Assert.That(manager.startOnHeadless, Is.True);
            Assert.That(manager.showDebugMessages, Is.False);
            Assert.That(manager.serverTickRate, Is.EqualTo(30));
            Assert.That(manager.offlineScene, Is.Empty);
            Assert.That(manager.server.MaxConnections, Is.EqualTo(4));
            Assert.That(manager.autoCreatePlayer, Is.True);
            Assert.That(manager.spawnPrefabs, Is.Empty);
            Assert.That(manager.numPlayers, Is.Zero);
            Assert.That(manager.isNetworkActive, Is.False);

            Assert.That(manager.networkSceneName, Is.Empty);
            Assert.That(manager.startPositionIndex, Is.Zero);
            Assert.That(manager.startPositions, Is.Empty);
        }

        [Test]
        public void StartServerTest()
        {
            Assert.That(manager.server.active , Is.False);

            manager.StartServer();

            Assert.That(manager.isNetworkActive , Is.True);
            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.ServerOnly));
            Assert.That(manager.server.active , Is.True);

            manager.StopServer();
        }

        [Test]
        public void StopServerTest()
        {
            manager.StartServer();
            manager.StopServer();

            Assert.That(manager.isNetworkActive , Is.False);
            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.Offline));
        }

        [Test]
        public void StartClientTest()
        {
            manager.StartClient("localhost");

            Assert.That(manager.isNetworkActive , Is.True);
            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.ClientOnly));

            manager.StopClient();
        }

        [Test]
        public void StopClientTest()
        {
            manager.StartClient("localhost");
            manager.StopClient();

            Assert.That(manager.isNetworkActive , Is.False);
            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.Offline));
        }

        [Test]
        public void ShutdownTest()
        {
            manager.StartClient("localhost");
            manager.StopClient();

            Assert.That(manager.startPositions , Is.Empty);
            Assert.That(manager.startPositionIndex , Is.Zero);
        }

        [Test]
        public void RegisterStartPositionTest()
        {
            Assert.That(manager.startPositions , Is.Empty);

            manager.RegisterStartPosition(gameObject.transform);
            Assert.That(manager.startPositions.Count , Is.EqualTo(1));
            Assert.That(manager.startPositions, Has.Member(gameObject.transform));

            manager.UnRegisterStartPosition(gameObject.transform);
        }

        [Test]
        public void UnRegisterStartPositionTest()
        {
            Assert.That(manager.startPositions , Is.Empty);
            
            manager.RegisterStartPosition(gameObject.transform);
            Assert.That(manager.startPositions.Count , Is.EqualTo(1));
            Assert.That(manager.startPositions, Has.Member(gameObject.transform));

            manager.UnRegisterStartPosition(gameObject.transform);
            Assert.That(manager.startPositions , Is.Empty);
        }

        [Test]
        public void GetStartPositionTest()
        {
            Assert.That(manager.startPositions , Is.Empty);
            
            manager.RegisterStartPosition(gameObject.transform);
            Assert.That(manager.startPositions.Count , Is.EqualTo(1));
            Assert.That(manager.startPositions, Has.Member(gameObject.transform));

            Assert.That(manager.GetStartPosition(), Is.SameAs(gameObject.transform));

            manager.UnRegisterStartPosition(gameObject.transform);
        }
    }
}
