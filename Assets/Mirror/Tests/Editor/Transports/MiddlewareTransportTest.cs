using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Transports
{
    public class MyMiddleware : MiddlewareTransport {}

    [Description("Test to make sure inner methods are called when using Middleware Transport")]
    public class MiddlewareTransportTest
    {
        Transport inner;
        MyMiddleware middleware;

        [SetUp]
        public void Setup()
        {
            inner = Substitute.For<Transport>();

            GameObject gameObject = new GameObject();

            middleware = gameObject.AddComponent<MyMiddleware>();
            middleware.inner = inner;
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(middleware.gameObject);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestAvailable(bool available)
        {
            inner.Available().Returns(available);

            Assert.That(middleware.Available(), Is.EqualTo(available));

            inner.Received(1).Available();
        }

        [Test]
        [TestCase(Channels.Reliable, 4000)]
        [TestCase(Channels.Reliable, 2000)]
        [TestCase(Channels.Unreliable, 4000)]
        public void TestGetMaxPacketSize(int channel, int packageSize)
        {
            inner.GetMaxPacketSize(Arg.Any<int>()).Returns(packageSize);

            Assert.That(middleware.GetMaxPacketSize(channel), Is.EqualTo(packageSize));

            inner.Received(1).GetMaxPacketSize(Arg.Is<int>(x => x == channel));
            inner.Received(0).GetMaxPacketSize(Arg.Is<int>(x => x != channel));
        }

        [Test]
        public void TestShutdown()
        {
            middleware.Shutdown();

            inner.Received(1).Shutdown();
        }

        [Test]
        [TestCase("localhost")]
        [TestCase("example.com")]
        public void TestClientConnect(string address)
        {
            middleware.ClientConnect(address);

            inner.Received(1).ClientConnect(address);
            inner.Received(0).ClientConnect(Arg.Is<string>(x => x != address));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestClientConnected(bool connected)
        {
            inner.ClientConnected().Returns(connected);

            Assert.That(middleware.ClientConnected(), Is.EqualTo(connected));

            inner.Received(1).ClientConnected();
        }

        [Test]
        public void TestClientDisconnect()
        {
            middleware.ClientDisconnect();

            inner.Received(1).ClientDisconnect();
        }

        [Test]
        [TestCase(Channels.Reliable)]
        [TestCase(Channels.Unreliable)]
        public void TestClientSend(int channel)
        {
            byte[] array = new byte[10];
            const int offset = 2;
            const int count = 5;
            ArraySegment<byte> segment = new ArraySegment<byte>(array, offset, count);

            middleware.ClientSend(segment, channel);

            inner.Received(1).ClientSend(Arg.Is<ArraySegment<byte>>(x => x.Array == array && x.Offset == offset && x.Count == count), channel);
            inner.Received(0).ClientSend(Arg.Any<ArraySegment<byte>>(), Arg.Is<int>(x => x != channel));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestServerActive(bool active)
        {
            inner.ServerActive().Returns(active);

            Assert.That(middleware.ServerActive(), Is.EqualTo(active));

            inner.Received(1).ServerActive();
        }

        [Test]
        public void TestServerStart()
        {
            middleware.ServerStart();

            inner.Received(1).ServerStart();
        }

        [Test]
        public void TestServerStop()
        {
            middleware.ServerStop();

            inner.Received(1).ServerStop();
        }

        [Test]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(19, 1)]
        public void TestServerSend(int id, int channel)
        {
            byte[] array = new byte[10];
            const int offset = 2;
            const int count = 5;
            ArraySegment<byte> segment = new ArraySegment<byte>(array, offset, count);

            middleware.ServerSend(id, segment, channel);

            inner.Received(1).ServerSend(id, Arg.Is<ArraySegment<byte>>(x => x.Array == array && x.Offset == offset && x.Count == count), channel);
            // only need to check first arg,
            inner.Received(0).ServerSend(Arg.Is<int>(x => x != id), Arg.Any<ArraySegment<byte>>(), Arg.Any<int>());
        }

        [Test]
        [TestCase(0, "tcp4://localhost:7777")]
        [TestCase(19, "tcp4://example.com:7777")]
        public void TestServerGetClientAddress(int id, string result)
        {
            inner.ServerGetClientAddress(id).Returns(result);

            Assert.That(middleware.ServerGetClientAddress(id), Is.EqualTo(result));

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

            Assert.That(middleware.ServerUri(), Is.EqualTo(uri));

            inner.Received(1).ServerUri();
        }

        [Test]
        public void TestClientConnectedCallback()
        {
            int called = 0;
            middleware.OnClientConnected = () =>
            {
                called++;
            };
            // connect to give callback to inner
            middleware.ClientConnect("localhost");

            inner.OnClientConnected.Invoke();
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientConnected.Invoke();
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        public void TestClientDataReceivedCallback(int channel)
        {
            byte[] data = new byte[4];
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 2);

            int called = 0;
            middleware.OnClientDataReceived = (d, c) =>
            {
                called++;
                Assert.That(c, Is.EqualTo(channel));
                Assert.That(d.Array, Is.EqualTo(segment.Array));
                Assert.That(d.Offset, Is.EqualTo(segment.Offset));
                Assert.That(d.Count, Is.EqualTo(segment.Count));
            };
            // connect to give callback to inner
            middleware.ClientConnect("localhost");

            inner.OnClientDataReceived.Invoke(segment, channel);
            Assert.That(called, Is.EqualTo(1));


            data = new byte[4];
            segment = new ArraySegment<byte>(data, 0, 3);

            inner.OnClientDataReceived.Invoke(segment, channel);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void TestClientDisconnectedCallback()
        {
            int called = 0;
            middleware.OnClientDisconnected = () =>
            {
                called++;
            };
            // connect to give callback to inner
            middleware.ClientConnect("localhost");

            inner.OnClientDisconnected.Invoke();
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientDisconnected.Invoke();
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void TestClientErrorCallback()
        {
            int called = 0;
            middleware.OnClientError = (error, reason) =>
            {
                called++;
                Assert.That(error, Is.EqualTo(TransportError.Unexpected));
            };
            // connect to give callback to inner
            middleware.ClientConnect("localhost");

            inner.OnClientError.Invoke(TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientError.Invoke(TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void TestClientExceptionCallback()
        {
            int called = 0;
            middleware.OnClientTransportException = (exception) =>
            {
                called++;
                // Assert that exception is System.Exception
                Assert.That(exception, Is.TypeOf<Exception>());
            };
            // connect to give callback to inner
            middleware.ClientConnect("localhost");

            inner.OnClientTransportException.Invoke(new Exception());
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientTransportException.Invoke(new Exception());
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0, "")]
        [TestCase(1, "")]
        [TestCase(19, "")]
        public void TestServerConnectedCallback(int id, string remoteClientAddress)
        {
            int called = 0;
            middleware.OnServerConnectedWithAddress = (i, clientAddress) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
            };
            // start to give callback to inner
            middleware.ServerStart();

            inner.OnServerConnectedWithAddress.Invoke(id, remoteClientAddress);
            Assert.That(called, Is.EqualTo(1));

            inner.OnServerConnectedWithAddress.Invoke(id, remoteClientAddress);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(19, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(19, 1)]
        public void TestServerDataReceivedCallback(int id, int channel)
        {
            byte[] data = new byte[4];
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 2);

            int called = 0;
            middleware.OnServerDataReceived = (i, d, c) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
                Assert.That(c, Is.EqualTo(channel));
                Assert.That(d.Array, Is.EqualTo(segment.Array));
                Assert.That(d.Offset, Is.EqualTo(segment.Offset));
                Assert.That(d.Count, Is.EqualTo(segment.Count));
            };
            // start to give callback to inner
            middleware.ServerStart();

            inner.OnServerDataReceived.Invoke(id, segment, channel);
            Assert.That(called, Is.EqualTo(1));


            data = new byte[4];
            segment = new ArraySegment<byte>(data, 0, 3);

            inner.OnServerDataReceived.Invoke(id, segment, channel);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(19)]
        public void TestServerDisconnectedCallback(int id)
        {
            int called = 0;
            middleware.OnServerDisconnected = (i) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
            };
            // start to give callback to inner
            middleware.ServerStart();

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
            middleware.OnServerError = (i, error, reason) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
                Assert.That(error, Is.EqualTo(TransportError.Unexpected));
            };
            // start to give callback to inner
            middleware.ServerStart();

            inner.OnServerError.Invoke(id, TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(1));

            inner.OnServerError.Invoke(id, TransportError.Unexpected, "");
            Assert.That(called, Is.EqualTo(2));
        }
    }
}
