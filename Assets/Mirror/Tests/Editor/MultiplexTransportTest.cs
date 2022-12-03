using System;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class MultiplexTransportTest : MirrorTest
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
        [Test]
        public void FromBaseId()
        {
            // if we have 3 transports, then
            // transport 0 will produce connection ids [0, 3, 6, 9, ...]
            const int transportAmount = 3;

            Assert.That(MultiplexTransport.FromBaseId(0, 0, transportAmount), Is.EqualTo(0));
            Assert.That(MultiplexTransport.FromBaseId(0, 1, transportAmount), Is.EqualTo(3));
            Assert.That(MultiplexTransport.FromBaseId(0, 2, transportAmount), Is.EqualTo(6));
            Assert.That(MultiplexTransport.FromBaseId(0, 3, transportAmount), Is.EqualTo(9));

            // transport 1 will produce connection ids [1, 4, 7, 10, ...]
            Assert.That(MultiplexTransport.FromBaseId(1, 0, transportAmount), Is.EqualTo(1));
            Assert.That(MultiplexTransport.FromBaseId(1, 1, transportAmount), Is.EqualTo(4));
            Assert.That(MultiplexTransport.FromBaseId(1, 2, transportAmount), Is.EqualTo(7));
            Assert.That(MultiplexTransport.FromBaseId(1, 3, transportAmount), Is.EqualTo(10));

            // transport 2 will produce connection ids [2, 5, 8, 11, ...]
            Assert.That(MultiplexTransport.FromBaseId(2, 0, transportAmount), Is.EqualTo(2));
            Assert.That(MultiplexTransport.FromBaseId(2, 1, transportAmount), Is.EqualTo(5));
            Assert.That(MultiplexTransport.FromBaseId(2, 2, transportAmount), Is.EqualTo(8));
            Assert.That(MultiplexTransport.FromBaseId(2, 3, transportAmount), Is.EqualTo(11));
        }

        [Test]
        public void ToBaseId()
        {
            const int transportAmount = 3;

            Assert.That(MultiplexTransport.ToBaseId(0, transportAmount), Is.EqualTo(0));
            Assert.That(MultiplexTransport.ToBaseId(1, transportAmount), Is.EqualTo(0));
            Assert.That(MultiplexTransport.ToBaseId(2, transportAmount), Is.EqualTo(0));

            Assert.That(MultiplexTransport.ToBaseId(3, transportAmount), Is.EqualTo(1));
            Assert.That(MultiplexTransport.ToBaseId(4, transportAmount), Is.EqualTo(1));
            Assert.That(MultiplexTransport.ToBaseId(5, transportAmount), Is.EqualTo(1));

            Assert.That(MultiplexTransport.ToBaseId(6, transportAmount), Is.EqualTo(2));
            Assert.That(MultiplexTransport.ToBaseId(7, transportAmount), Is.EqualTo(2));
            Assert.That(MultiplexTransport.ToBaseId(8, transportAmount), Is.EqualTo(2));

            Assert.That(MultiplexTransport.ToBaseId(9, transportAmount), Is.EqualTo(3));
        }

        [Test]
        public void ToTransportId()
        {
            const int transportAmount = 3;

            Assert.That(MultiplexTransport.ToTransportId(0, transportAmount), Is.EqualTo(0));
            Assert.That(MultiplexTransport.ToTransportId(1, transportAmount), Is.EqualTo(1));
            Assert.That(MultiplexTransport.ToTransportId(2, transportAmount), Is.EqualTo(2));

            Assert.That(MultiplexTransport.ToTransportId(3, transportAmount), Is.EqualTo(0));
            Assert.That(MultiplexTransport.ToTransportId(4, transportAmount), Is.EqualTo(1));
            Assert.That(MultiplexTransport.ToTransportId(5, transportAmount), Is.EqualTo(2));

            Assert.That(MultiplexTransport.ToTransportId(6, transportAmount), Is.EqualTo(0));
            Assert.That(MultiplexTransport.ToTransportId(7, transportAmount), Is.EqualTo(1));
            Assert.That(MultiplexTransport.ToTransportId(8, transportAmount), Is.EqualTo(2));

            Assert.That(MultiplexTransport.ToTransportId(9, transportAmount), Is.EqualTo(0));
        }

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
