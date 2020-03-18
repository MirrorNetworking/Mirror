using Mirror.Tcp;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkServerTests 
    {
        NetworkServer testServer;
        GameObject serverGO;

        [SetUp]
        public void SetupNetworkServer()
        {
            serverGO = new GameObject();
            testServer = serverGO.AddComponent<NetworkServer>();
            serverGO.AddComponent<NetworkClient>();
            Transport transport = serverGO.AddComponent<TcpTransport>();

            Transport.activeTransport = transport;
            testServer.Listen();
        }

        [Test]
        public void InitializeTest()
        {
            Assert.That(testServer.connections.Count == 0);
            Assert.That(testServer.active);
            Assert.That(testServer.LocalClientActive, Is.False);
        }

        [Test]
        public void SpawnTest()
        {
            var gameObject = new GameObject();
            gameObject.AddComponent<NetworkIdentity>();
            testServer.Spawn(gameObject);

            Assert.That(gameObject.GetComponent<NetworkIdentity>().server == testServer);
        }

       
        [Test]
        public void ShutdownTest()
        {
            testServer.Shutdown();
            Assert.That(testServer.active == false);
        }

        [TearDown]
        public void ShutdownNetworkServer()
        {
            testServer.Shutdown();
            GameObject.DestroyImmediate(serverGO);
        }
    }
}
