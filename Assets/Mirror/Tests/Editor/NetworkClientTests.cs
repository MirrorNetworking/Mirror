using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
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
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);
            NetworkServer.Listen(10);

            // setup client handlers too
            NetworkClient.RegisterHandler<ConnectMessage>(msg => { }, false);
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

        [Test]
        public void DisconnectInHostMode()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
            Assert.That(NetworkServer.localConnection, !Is.Null);

            NetworkClient.Disconnect();
            Assert.That(NetworkClient.isConnected, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        [Test]
        public void Send()
        {
            // register server handler
            int called = 0;
            NetworkServer.RegisterHandler<AddPlayerMessage>((conn, msg) => { ++called; }, false);

            // connect a regular connection. not host, because host would use
            // connId=0 but memorytransport uses connId=1
            NetworkClient.Connect("localhost");
            // update transport so connect event is processed
            ((MemoryTransport)Transport.activeTransport).LateUpdate();

            // send it
            AddPlayerMessage message = new AddPlayerMessage();
            NetworkClient.Send(message);

            // update transport so data event is processed
            ((MemoryTransport)Transport.activeTransport).LateUpdate();

            // received it on server?
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
