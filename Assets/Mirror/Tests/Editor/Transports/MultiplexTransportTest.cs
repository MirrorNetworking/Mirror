using System;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests.Transports
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
        public void ConnectionIdMapping()
        {
            // add a few connectionIds from transport #0
            // one large connId to prevent https://github.com/vis2k/Mirror/issues/3280
            int t0_c10  = transport.AddToLookup(10,           0); // should get multiplexId = 1
            int t0_c20  = transport.AddToLookup(20,           0); // should get multiplexId = 2
            int t0_cmax = transport.AddToLookup(int.MaxValue, 0); // should get multiplexId = 3

            // add a few connectionIds from transport #1
            // one large connId to prevent https://github.com/vis2k/Mirror/issues/3280
            int t1_c10  = transport.AddToLookup(10,           1); // should get multiplexId = 4
            int t1_c50  = transport.AddToLookup(50,           1); // should get multiplexId = 5
            int t1_cmax = transport.AddToLookup(int.MaxValue, 1); // should get multiplexId = 6

            // MultiplexId -> (OriginalId, TransportIndex) for transport #0
            if (transport.OriginalId(t0_c10, out int originalId, out int transportIndex))
            {
                Assert.That(transportIndex, Is.EqualTo(0));
                Assert.That(originalId, Is.EqualTo(10));
            }

            if (transport.OriginalId(t0_c20, out originalId, out transportIndex))
            {
                Assert.That(transportIndex, Is.EqualTo(0));
                Assert.That(originalId, Is.EqualTo(20));
            }

            if (transport.OriginalId(t0_cmax, out originalId, out transportIndex))
            {
                Assert.That(transportIndex, Is.EqualTo(0));
                Assert.That(originalId, Is.EqualTo(int.MaxValue));
            }

            // MultiplexId -> (OriginalId, TransportIndex) for transport #1
            if (transport.OriginalId(t1_c10, out originalId, out transportIndex))
            {
                Assert.That(transportIndex, Is.EqualTo(1));
                Assert.That(originalId, Is.EqualTo(10));
            }

            if (transport.OriginalId(t1_c50, out originalId, out transportIndex))
            {
                Assert.That(transportIndex, Is.EqualTo(1));
                Assert.That(originalId, Is.EqualTo(50));
            }

            if (transport.OriginalId(t1_cmax, out originalId, out transportIndex))
            {
                Assert.That(transportIndex, Is.EqualTo(1));
                Assert.That(originalId, Is.EqualTo(int.MaxValue));
            }

            // (OriginalId, TransportIndex) -> MultiplexId for transport #1
            Assert.That(transport.MultiplexId(10, 0), Is.EqualTo(t0_c10));
            Assert.That(transport.MultiplexId(20, 0), Is.EqualTo(t0_c20));
            Assert.That(transport.MultiplexId(int.MaxValue, 0), Is.EqualTo(t0_cmax));

            // (OriginalId, TransportIndex) -> MultiplexId for transport #2
            Assert.That(transport.MultiplexId(10, 1), Is.EqualTo(t1_c10));
            Assert.That(transport.MultiplexId(50, 1), Is.EqualTo(t1_c50));
            Assert.That(transport.MultiplexId(int.MaxValue, 1), Is.EqualTo(t1_cmax));
        }

        [Test]
        public void TestAvailable()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(false);
            Assert.That(transport.Available());
        }

        [Test]
        public void TestNotAvailable()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(false);
            Assert.That(transport.Available(), Is.False);
        }

        [Test]
        public void TestConnect()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(true);

            // connect on multiplex transport
            transport.ClientConnect("some.server.com");

            // should be forwarded to available transport #2
            transport1.DidNotReceive().ClientConnect(Arg.Any<string>());
            transport2.Received().ClientConnect("some.server.com");
        }

        [Test]
        public void TestConnectFirstUri()
        {
            Uri uri = new Uri("tcp://some.server.com");

            transport1.Available().Returns(true);
            transport2.Available().Returns(true);

            // connect on multiplext ransport
            transport.ClientConnect(uri);

            // should be forwarded to available transport #1
            transport1.Received().ClientConnect(uri);
            transport2.DidNotReceive().ClientConnect(uri);
        }


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

            // connect on multiplex transport
            transport.ClientConnect(uri);

            // should be forwarded to available transport #2
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

            // disconnect on multiplex transport
            transport.ClientDisconnect();

            // should be forwarded to transport #1
            transport1.Received().ClientDisconnect();
        }

        [Test]
        public void TestClientSend()
        {
            transport1.Available().Returns(true);
            transport.ClientConnect("some.server.com");

            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            // send on multiplex transport
            transport.ClientSend(segment, 3);

            // should be forwarded to to transport #1
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
            void SendMessage(int connectionId, string remoteClientAddress)
            {
                transport.ServerSend(connectionId, segment, 5);
            }

            // set event and Start to give event to inner
            transport.OnServerConnectedWithAddress = SendMessage;
            transport.ServerStart();

            transport1.OnServerConnectedWithAddress.Invoke(1, "");

            transport1.Received().ServerSend(1, segment, 5);
        }

        [Test]
        public void TestServerSend()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(true);
            transport.ServerStart();
            transport.ClientConnect("some.server.com");

            transport.OnServerConnectedWithAddress = (connectionId, remoteClientAddress) => {};
            transport.OnServerDisconnected = _ => {};

            // connect two connectionIds.
            // one of them very large to prevent
            // https://github.com/vis2k/Mirror/issues/3280
            transport1.OnServerConnectedWithAddress(10, "");
            transport2.OnServerConnectedWithAddress(int.MaxValue, "");

            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            // call ServerSend on multiplex transport.
            // multiplexed connId = 1 represents transport #1 connId = 10
            transport.ServerSend(1, segment, 0);
            transport1.Received().ServerSend(10, segment, 0);

            // call ServerSend on multiplex transport.
            // multiplexed connId = 2 represents transport #2 connId = int.max
            transport.ServerSend(2, segment, 0);
            transport2.Received().ServerSend(int.MaxValue, segment, 0);
        }
    }
}
