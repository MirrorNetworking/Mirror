using System;
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
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);
            NetworkServer.Listen(10);

            // setup client handlers too
            NetworkClient.RegisterHandler<ConnectMessage>(msg => {}, false);
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

        [Test]
        public void ConnectUri()
        {
            NetworkClient.Connect(new Uri("memory://localhost"));
            // update transport so connect event is processed
            ((MemoryTransport)Transport.activeTransport).LateUpdate();
            Assert.That(NetworkClient.isConnected, Is.True);
        }
    }
}
