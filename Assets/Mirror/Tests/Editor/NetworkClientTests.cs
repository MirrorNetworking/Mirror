using Mirror;
using Mirror.Tests;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace  Mirror.Tests
{
    public class NetworkClientTests
    {
        GameObject transportGO;

        [SetUp]
        public void SetUp()
        {
            // client.connect sets transport.enabled, which only works if
            // Transport is on a GameObject
            transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

            // we need a server to connect to
            NetworkServer.Listen(10);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkServer.Shutdown();
            NetworkClient.Shutdown();
            GameObject.DestroyImmediate(transportGO);
            Transport.activeTransport = null;
        }

        [Test]
        public void serverIp()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.serverIp, Is.EqualTo("localhost"));
        }

        [Test]
        public void isConnected()
        {
            Assert.That(NetworkClient.isConnected, Is.False);
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
        }
    }
}
