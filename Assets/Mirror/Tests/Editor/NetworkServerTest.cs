using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkServerTest
    {
        [SetUp]
        public void SetUp()
        {
            Transport.activeTransport = Substitute.For<Transport>();
        }

        [TearDown]
        public void TearDown()
        {
            Transport.activeTransport = null;
        }

        [Test]
        public void IsActiveTest()
        {
            Assert.That(NetworkServer.active, Is.False);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);
            NetworkServer.Shutdown();
            Assert.That(NetworkServer.active, Is.False);
        }

        [Test]
        public void MaxConnectionsTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen with maxconnections=1
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first: should work
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // connect second: should fail
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void ConnectMessageHandlerTest()
        {
            // message handlers
            bool connectCalled = false;
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { connectCalled = true; }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(connectCalled, Is.False);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(connectCalled, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void DisconnectMessageHandlerTest()
        {
            // message handlers
            bool disconnectCalled = false;
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { disconnectCalled = true; }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(disconnectCalled, Is.False);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(disconnectCalled, Is.False);

            // disconnect
            Transport.activeTransport.OnServerDisconnected.Invoke(42);
            Assert.That(disconnectCalled, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void ConnectionsDictTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);

            // connect second
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);

            // disconnect second
            Transport.activeTransport.OnServerDisconnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);

            // disconnect first
            Transport.activeTransport.OnServerDisconnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void SetLocalConnectionTest()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            ULocalConnectionToClient localConnection = new ULocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void LocalClientActiveTest()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.localClientActive, Is.False);

            // set local connection
            NetworkServer.SetLocalConnection(new ULocalConnectionToClient());
            Assert.That(NetworkServer.localClientActive, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void ShutdownCleanupTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // set local connection
            NetworkServer.SetLocalConnection(new ULocalConnectionToClient());
            Assert.That(NetworkServer.localClientActive, Is.True);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // shutdown
            NetworkServer.Shutdown();

            // state cleared?
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.active, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
            Assert.That(NetworkServer.localClientActive, Is.False);
        }
    }
}
