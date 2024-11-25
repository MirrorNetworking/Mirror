using System;
using Mirror.Transports.Encryption;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Transports
{

    // This is mostly a copy of MiddlewareTransport, with the stuff requiring actual connections to be setup deleted
    [Description("Test to make sure inner methods are called when using Encryption Transport")]
    public class EncryptionTransportTransportTest
    {
        Transport inner;
        EncryptionTransport encryption;

        [SetUp]
        public void Setup()
        {
            inner = Substitute.For<Transport>();

            GameObject gameObject = new GameObject();

            encryption = gameObject.AddComponent<EncryptionTransport>();
            encryption.Inner = inner;
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(encryption.gameObject);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestAvailable(bool available)
        {
            inner.Available().Returns(available);

            Assert.That(encryption.Available(), Is.EqualTo(available));

            inner.Received(1).Available();
        }

        [Test]
        [TestCase(Channels.Reliable, 4000)]
        [TestCase(Channels.Reliable, 2000)]
        [TestCase(Channels.Unreliable, 4000)]
        public void TestGetMaxPacketSize(int channel, int packageSize)
        {
            inner.GetMaxPacketSize(Arg.Any<int>()).Returns(packageSize);

            Assert.That(encryption.GetMaxPacketSize(channel), Is.EqualTo(packageSize - EncryptedConnection.Overhead));

            inner.Received(1).GetMaxPacketSize(Arg.Is<int>(x => x == channel));
            inner.Received(0).GetMaxPacketSize(Arg.Is<int>(x => x != channel));
        }

        [Test]
        public void TestShutdown()
        {
            encryption.Shutdown();

            inner.Received(1).Shutdown();
        }

        [Test]
        [TestCase("localhost")]
        [TestCase("example.com")]
        public void TestClientConnect(string address)
        {
            encryption.ClientConnect(address);

            inner.Received(1).ClientConnect(address);
            inner.Received(0).ClientConnect(Arg.Is<string>(x => x != address));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestClientConnected(bool connected)
        {
            inner.ClientConnected().Returns(connected);

            Assert.That(encryption.ClientConnected(), Is.EqualTo(false)); // not testing connection handshaking here
        }

        [Test]
        public void TestClientDisconnect()
        {
            encryption.ClientDisconnect();

            inner.Received(1).ClientDisconnect();
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestServerActive(bool active)
        {
            inner.ServerActive().Returns(active);

            Assert.That(encryption.ServerActive(), Is.EqualTo(active));

            inner.Received(1).ServerActive();
        }

        [Test]
        public void TestServerStart()
        {
            encryption.ServerStart();

            inner.Received(1).ServerStart();
        }

        [Test]
        public void TestServerStop()
        {
            encryption.ServerStop();

            inner.Received(1).ServerStop();
        }

        [Test]
        [TestCase(0, "tcp4://localhost:7777")]
        [TestCase(19, "tcp4://example.com:7777")]
        public void TestServerGetClientAddress(int id, string result)
        {
            inner.ServerGetClientAddress(id).Returns(result);

            Assert.That(encryption.ServerGetClientAddress(id), Is.EqualTo(result));

            inner.Received(1).ServerGetClientAddress(id);
            inner.Received(0).ServerGetClientAddress(Arg.Is<int>(x => x != id));

        }

        [Test]
        [TestCase("tcp4://localhost:7777")]
        [TestCase("tcp4://example.com:7777")]
        public void TestServerUri(string address)
        {
            Uri uri = new Uri(address);
            inner.ServerUri().Returns(uri);

            Assert.That(encryption.ServerUri(), Is.EqualTo(uri));

            inner.Received(1).ServerUri();
        }

        [Test]
        public void TestClientDisconnectedCallback()
        {
            int called = 0;
            encryption.OnClientDisconnected = () =>
            {
                called++;
            };
            // connect to give callback to inner
            encryption.ClientConnect("localhost");

            inner.OnClientDisconnected.Invoke();
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientDisconnected.Invoke();
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void TestClientErrorCallback()
        {
            int called = 0;
            encryption.OnClientError = (error, reason) =>
            {
                called++;
                Assert.That(error, Is.EqualTo(TransportError.Unexpected));
            };
            // connect to give callback to inner
            encryption.ClientConnect("localhost");

            inner.OnClientError.Invoke(TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientError.Invoke(TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(19)]
        public void TestServerDisconnectedCallback(int id)
        {
            int called = 0;
            encryption.OnServerDisconnected = (i) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
            };
            // start to give callback to inner
            encryption.ServerStart();

            inner.OnServerDisconnected.Invoke(id);
            Assert.That(called, Is.EqualTo(1));

            inner.OnServerDisconnected.Invoke(id);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(19)]
        public void TestServerErrorCallback(int id)
        {
            int called = 0;
            encryption.OnServerError = (i, error, reason) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
                Assert.That(error, Is.EqualTo(TransportError.Unexpected));
            };
            // start to give callback to inner
            encryption.ServerStart();

            inner.OnServerError.Invoke(id, TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(1));

            inner.OnServerError.Invoke(id, TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(2));
        }
    }
}
