using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mirror
{

    public class PlayerSpawnerTest
    {
        private GameObject go;
        private NetworkClient client;
        private NetworkServer server;
        private PlayerSpawner spawner;
        private NetworkSceneManager sceneManager;
        private ServerObjectManager serverObjectManager;
        private ClientObjectManager clientObjectManager;
        private GameObject playerPrefab;

        private Transform pos1;
        private Transform pos2;

        [SetUp]
        public void Setup()
        {
            go = new GameObject();
            client = go.AddComponent<NetworkClient>();
            server = go.AddComponent<NetworkServer>();
            spawner = go.AddComponent<PlayerSpawner>();
            sceneManager = go.AddComponent<NetworkSceneManager>();
            serverObjectManager = go.AddComponent<ServerObjectManager>();
            clientObjectManager = go.AddComponent<ClientObjectManager>();
            spawner.sceneManager = sceneManager;
            sceneManager.Client = client;
            sceneManager.Server = server;
            serverObjectManager.server = server;
            clientObjectManager.client = client;
            clientObjectManager.networkSceneManager = sceneManager;
            spawner.client = client;
            spawner.server = server;
            spawner.serverObjectManager = serverObjectManager;
            spawner.clientObjectManager = clientObjectManager;

            playerPrefab = new GameObject();
            NetworkIdentity playerId = playerPrefab.AddComponent<NetworkIdentity>();

            spawner.playerPrefab = playerId;

            pos1 = new GameObject().transform;
            pos2 = new GameObject().transform;
            spawner.startPositions.Add(pos1);
            spawner.startPositions.Add(pos2);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(playerPrefab);

            Object.DestroyImmediate(pos1.gameObject);
            Object.DestroyImmediate(pos2.gameObject);
        }

        [Test]
        public void StartExceptionTest()
        {
            spawner.playerPrefab = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                spawner.Start();
            });
        }

        [Test]
        public void StartExceptionMissingServerObjectManagerTest()
        {
            spawner.serverObjectManager = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                spawner.Start();
            });
        }

        [Test]
        public void AutoConfigureClient()
        {
            spawner.Start();
            Assert.That(spawner.client, Is.SameAs(client));
        }

        [Test]
        public void AutoConfigureServer()
        {
            spawner.Start();
            Assert.That(spawner.server, Is.SameAs(server));
        }

        [Test]
        public void GetStartPositionRoundRobinTest()
        {
            spawner.Start();

            spawner.playerSpawnMethod = PlayerSpawner.PlayerSpawnMethod.RoundRobin;
            Assert.That(spawner.GetStartPosition(), Is.SameAs(pos1.transform));
            Assert.That(spawner.GetStartPosition(), Is.SameAs(pos2.transform));
            Assert.That(spawner.GetStartPosition(), Is.SameAs(pos1.transform));
            Assert.That(spawner.GetStartPosition(), Is.SameAs(pos2.transform));
        }

        [Test]
        public void GetStartPositionRandomTest()
        {
            spawner.Start();

            spawner.playerSpawnMethod = PlayerSpawner.PlayerSpawnMethod.Random;
            Assert.That(spawner.GetStartPosition(), Is.SameAs(pos1.transform) | Is.SameAs(pos2.transform));
        }

        [Test]
        public void GetStartPositionNullTest()
        {
            spawner.Start();

            spawner.startPositions.Clear();
            Assert.That(spawner.GetStartPosition(), Is.SameAs(null));
        }

        [Test]
        public void MissingClientObjectSpawnerExceptionTest()
        {
            spawner.clientObjectManager = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                spawner.Start();
            });
        }
    }
}
