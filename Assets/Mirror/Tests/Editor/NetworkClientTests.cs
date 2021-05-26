using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkClientTests : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // we need a server to connect to
            NetworkServer.Listen(10);
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkServer.Shutdown();
            NetworkClient.Shutdown();
            base.TearDown();
        }

        [Test]
        public void ServerIp()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.serverIp, Is.EqualTo("localhost"));
        }

        [Test]
        public void IsConnected()
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
            UpdateTransport();
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

        // TODO flaky
        // TODO running play mode tests, then edit mode tests, makes this fail
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
            UpdateTransport();

            // send it
            AddPlayerMessage message = new AddPlayerMessage();
            NetworkClient.Send(message);

            // update transport so data event is processed
            UpdateTransport();

            // received it on server?
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
