using System;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class MultiplexTest : MirrorTest
    {
        Transport transport1;
        Transport transport2;
        new MultiplexTransport transport;

        [SetUp]
        public void Setup()
        {
            base.SetUp();

            transport = holder.AddComponent<MultiplexTransport>();

            transport1 = Substitute.For<Transport>();
            transport2 = Substitute.For<Transport>();
            transport.transports = new[] { transport1, transport2 };

            transport.Awake();
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // A Test behaves as an ordinary method
        [Test]
        public void TestAvailable()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(false);
            Assert.That(transport.Available());
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestNotAvailable()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(false);
            Assert.That(transport.Available(), Is.False);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestConnect()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(true);
            transport.ClientConnect("some.server.com");

            transport1.DidNotReceive().ClientConnect(Arg.Any<string>());
            transport2.Received().ClientConnect("some.server.com");
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestConnectFirstUri()
        {
            Uri uri = new Uri("tcp://some.server.com");

            transport1.Available().Returns(true);
            transport2.Available().Returns(true);

            transport.ClientConnect(uri);
            transport1.Received().ClientConnect(uri);
            transport2.DidNotReceive().ClientConnect(uri);
        }


        // A Test behaves as an ordinary method
        [Test]
        public void TestConnectSecondUri()
        {
            Uri uri = new Uri("ws://some.server.com");

            transport1.Available().Returns(true);

            // first transport does not support websocket
            transport1
                .When(x => x.ClientConnect(uri))
                .Do(x => { throw new ArgumentException("Scheme not supported"); });

            transport2.Available().Returns(true);

            transport.ClientConnect(uri);
            transport2.Received().ClientConnect(uri);
        }

        [Test]
        public void TestConnected()
        {
            transport1.Available().Returns(true);
            transport.ClientConnect("some.server.com");

            transport1.ClientConnected().Returns(true);

            Assert.That(transport.ClientConnected());
        }

        [Test]
        public void TestDisconnect()
        {
            transport1.Available().Returns(true);
            transport.ClientConnect("some.server.com");

            transport.ClientDisconnect();

            transport1.Received().ClientDisconnect();
        }

        [Test]
        public void TestClientSend()
        {
            transport1.Available().Returns(true);
            transport.ClientConnect("some.server.com");

            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            transport.ClientSend(segment, 3);

            transport1.Received().ClientSend(segment, 3);
        }

        [Test]
        public void TestClient1Connected()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(true);

            Action callback = Substitute.For<Action>();
            // find available
            transport.Awake();
            // set event and connect to give event to inner
            transport.OnClientConnected = callback;
            transport.ClientConnect("localhost");
            transport1.OnClientConnected.Invoke();
            callback.Received().Invoke();
        }

        [Test]
        public void TestClient2Connected()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(true);

            Action callback = Substitute.For<Action>();
            // find available
            transport.Awake();
            // set event and connect to give event to inner
            transport.OnClientConnected = callback;
            transport.ClientConnect("localhost");
            transport2.OnClientConnected.Invoke();
            callback.Received().Invoke();
        }

        [Test]
        public void TestServerConnected()
        {
            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            // on connect, send a message back
            void SendMessage(int connectionId)
            {
                transport.ServerSend(connectionId, segment, 5);
            }

            // set event and Start to give event to inner
            transport.OnServerConnected = SendMessage;
            transport.ServerStart();

            transport1.OnServerConnected.Invoke(1);

            transport1.Received().ServerSend(1, segment, 5);
        }
    }
}
