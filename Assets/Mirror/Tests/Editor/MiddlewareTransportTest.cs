using System;
using System.IO;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class MyMiddleware : MiddlewareTransport { }

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
            //manually call awake in editmode
            middleware.Awake();
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
        [TestCase(Channels.DefaultReliable, 4000)]
        [TestCase(Channels.DefaultReliable, 2000)]
        [TestCase(Channels.DefaultUnreliable, 4000)]
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
        [TestCase(Channels.DefaultReliable)]
        [TestCase(Channels.DefaultUnreliable)]
        public void TestClientSend(int channel)
        {
            byte[] array = new byte[10];
            const int offset = 2;
            const int count = 5;
            ArraySegment<byte> segment = new ArraySegment<byte>(array, offset, count);

            middleware.ClientSend(channel, segment);

            inner.Received(1).ClientSend(channel, Arg.Is<ArraySegment<byte>>(x => x.Array == array && x.Offset == offset && x.Count == count));
            inner.Received(0).ClientSend(Arg.Is<int>(x => x != channel), Arg.Any<ArraySegment<byte>>());
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

            middleware.ServerSend(id, channel, segment);

            inner.Received(1).ServerSend(id, channel, Arg.Is<ArraySegment<byte>>(x => x.Array == array && x.Offset == offset && x.Count == count));
            // only need to check first arg, 
            inner.Received(0).ServerSend(Arg.Is<int>(x => x != id), Arg.Any<int>(), Arg.Any<ArraySegment<byte>>());
        }

        [Test]
        [TestCase(0, true)]
        [TestCase(19, false)]
        public void TestServerDisconnect(int id, bool result)
        {
            inner.ServerDisconnect(id).Returns(result);

            Assert.That(middleware.ServerDisconnect(id), Is.EqualTo(result));

            inner.Received(1).ServerDisconnect(id);
            inner.Received(0).ServerDisconnect(Arg.Is<int>(x => x != id));
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
        public void TestOnClientConnected()
        {
            int called = 0;
            middleware.OnClientConnected.AddListener(() =>
            {
                called++;
            });

            inner.OnClientConnected.Invoke();
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientConnected.Invoke();
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        public void TestOnClientDataReceived(int channel)
        {
            byte[] data = new byte[4];
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 2);

            int called = 0;
            middleware.OnClientDataReceived.AddListener((d, c) =>
            {
                called++;
                Assert.That(c, Is.EqualTo(channel));
                Assert.That(d.Array, Is.EqualTo(segment.Array));
                Assert.That(d.Offset, Is.EqualTo(segment.Offset));
                Assert.That(d.Count, Is.EqualTo(segment.Count));
            });


            inner.OnClientDataReceived.Invoke(segment, channel);
            Assert.That(called, Is.EqualTo(1));


            data = new byte[4];
            segment = new ArraySegment<byte>(data, 0, 3);

            inner.OnClientDataReceived.Invoke(segment, channel);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void TestOnClientDisconnected()
        {
            int called = 0;
            middleware.OnClientDisconnected.AddListener(() =>
            {
                called++;
            });

            inner.OnClientDisconnected.Invoke();
            Assert.That(called, Is.EqualTo(1));

            inner.OnClientDisconnected.Invoke();
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void TestOnClientError()
        {
            Exception exception = new InvalidDataException();

            int called = 0;
            middleware.OnClientError.AddListener((e) =>
            {
                called++;
                Assert.That(e, Is.EqualTo(exception));
            });

            inner.OnClientError.Invoke(exception);
            Assert.That(called, Is.EqualTo(1));

            exception = new NullReferenceException();

            inner.OnClientError.Invoke(exception);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(19)]
        public void TestOnServerConnected(int id)
        {
            int called = 0;
            middleware.OnServerConnected.AddListener((i) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
            });

            inner.OnServerConnected.Invoke(id);
            Assert.That(called, Is.EqualTo(1));

            inner.OnServerConnected.Invoke(id);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(19, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(19, 1)]
        public void TestOnServerDataReceived(int id, int channel)
        {
            byte[] data = new byte[4];
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 2);

            int called = 0;
            middleware.OnServerDataReceived.AddListener((i, d, c) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
                Assert.That(c, Is.EqualTo(channel));
                Assert.That(d.Array, Is.EqualTo(segment.Array));
                Assert.That(d.Offset, Is.EqualTo(segment.Offset));
                Assert.That(d.Count, Is.EqualTo(segment.Count));
            });


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
        public void TestOnServerDisconnected(int id)
        {
            int called = 0;
            middleware.OnServerDisconnected.AddListener((i) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
            });

            inner.OnServerDisconnected.Invoke(id);
            Assert.That(called, Is.EqualTo(1));

            inner.OnServerDisconnected.Invoke(id);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(19)]
        public void TestOnServerError(int id)
        {
            Exception exception = new InvalidDataException();

            int called = 0;
            middleware.OnServerError.AddListener((i, e) =>
            {
                called++;
                Assert.That(i, Is.EqualTo(id));
                Assert.That(e, Is.EqualTo(exception));
            });

            inner.OnServerError.Invoke(id, exception);
            Assert.That(called, Is.EqualTo(1));

            exception = new NullReferenceException();

            inner.OnServerError.Invoke(id, exception);
            Assert.That(called, Is.EqualTo(2));
        }
    }
}
