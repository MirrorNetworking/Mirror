using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkManagerTest : MirrorEditModeTest
    {
        GameObject gameObject => holder;
        NetworkManager manager;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            manager = gameObject.AddComponent<NetworkManager>();
        }

        [Test]
        public void StartServerTest()
        {
            Assert.That(NetworkServer.active, Is.False);

            manager.StartServer();

            Assert.That(manager.mode == NetworkManagerMode.ServerOnly);
            Assert.That(NetworkServer.active, Is.True);
        }

        [Test]
        public void StopServerTest()
        {
            manager.StartServer();
            manager.StopServer();

            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void StartClientTest()
        {
            manager.StartClient();

            Assert.That(manager.mode == NetworkManagerMode.ClientOnly);

            manager.StopClient();
        }

        [Test]
        public void StopClientTest()
        {
            manager.StartClient();
            manager.StopClient();

            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void StartHostTest()
        {
            manager.StartHost();

            Assert.That(manager.mode == NetworkManagerMode.Host);
            Assert.That(NetworkServer.active, Is.True);
            Assert.That(NetworkClient.active, Is.True);
        }

        [Test]
        public void StopHostTest()
        {
            manager.StartHost();
            manager.StopHost();

            Assert.That(manager.mode == NetworkManagerMode.Offline);
            Assert.That(NetworkServer.active, Is.False);
            Assert.That(NetworkClient.active, Is.False);
        }

        [Test]
        public void ShutdownTest()
        {
            manager.StartClient();
            NetworkManager.ResetStatics();

            Assert.That(NetworkManager.startPositions.Count, Is.Zero);
            Assert.That(NetworkManager.startPositionIndex, Is.Zero);
            Assert.That(NetworkManager.clientReadyConnection, Is.Null);
            Assert.That(NetworkManager.loadingSceneAsync, Is.Null);
            Assert.That(NetworkManager.singleton, Is.Null);
            Assert.That(NetworkManager.networkSceneName, Is.Empty);
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
            UriBuilder uriBuilder = new UriBuilder
            {
                Host = "localhost",
                Scheme = "local"
            };
            manager.StartClient(uriBuilder.Uri);

            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.ClientOnly));
            Assert.That(manager.networkAddress, Is.EqualTo(uriBuilder.Uri.Host));
        }
    }
}
