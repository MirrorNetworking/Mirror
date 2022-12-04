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
        public void MultiplexedConnectionId()
        {
            // add a few connectionIds from transport #0
            transport.AddToLookup(10,           0); // connId = 0
            transport.AddToLookup(20,           0); // connId = 1
            transport.AddToLookup(int.MaxValue, 0); // connId = max

            // multiplexed ids should count up
            Assert.That(transport.MultiplexedId(1), Is.EqualTo(10));
            Assert.That(transport.MultiplexedId(2), Is.EqualTo(20));
            Assert.That(transport.MultiplexedId(3), Is.EqualTo(int.MaxValue));

            // add a few connectionIds from transport #1
            transport.AddToLookup(10,             1); // connId = 0
            transport.AddToLookup(30,             1); // connId = 1
            transport.AddToLookup(int.MaxValue-1, 1); // connId = max

            // multiplexed ids should count up
            Assert.That(transport.MultiplexedId(4), Is.EqualTo(10));
            Assert.That(transport.MultiplexedId(5), Is.EqualTo(30));
            Assert.That(transport.MultiplexedId(6), Is.EqualTo(int.MaxValue-1));
        }

        [Test]
        public void OriginalConnectionId()
        {
            // TODO
        }

        [Test]
        public void OriginalTransportId()
        {
            // TODO
        }

        // test to reproduce https://github.com/vis2k/Mirror/issues/3280
        [Test]
        public void LargeConnectionId()
        {
            // TODO
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

        [Test]
        public void TestServerSend()
        {
            transport1.Available().Returns(true);
            transport.ClientConnect("some.server.com");

            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            // call ServerSend on multiplex transport.
            // multiplexed connId = 0 corresponds to connId = 0 with transport #1
            transport.ServerSend(0, data, 0);
            transport1.Received().ServerSend(0, segment, 0);

            // call ServerSend on multiplex transport.
            // multiplexed connId = 1 corresponds to connId = 0 with transport #2
            transport.ServerSend(1, data, 0);
            transport2.Received().ServerSend(0, segment, 0);

            // call ServerSend on multiplex transport.
            // multiplexed connId = 2 corresponds to connId = 1 with transport #1
            transport.ServerSend(2, data, 0);
            transport1.Received().ServerSend(1, segment, 0);

            // call ServerSend on multiplex transport.
            // multiplexed connId = 3 corresponds to connId = 1 with transport #2
            transport.ServerSend(3, data, 0);
            transport2.Received().ServerSend(1, segment, 0);
        }
    }
}
