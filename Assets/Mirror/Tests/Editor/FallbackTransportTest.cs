using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class FallbackTransportTest
    {

        Transport transport1;
        Transport transport2;
        FallbackTransport transport;

        [SetUp]
        public void Setup()
        {
            transport1 = Substitute.For<Transport>();
            transport2 = Substitute.For<Transport>();

            GameObject gameObject = new GameObject();

            transport = gameObject.AddComponent<FallbackTransport>();
            transport.transports = new[] { transport1, transport2 };
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(transport.gameObject);
        }

        #region Client tests
        // A Test behaves as an ordinary method
        [Test]
        public void TestAvailable()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(false);
            transport.Awake();

            Assert.That(transport.Available());
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestNotAvailable()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(false);

            Assert.Throws<Exception>(() => transport.Awake());

        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestConnect()
        {
            transport1.Available().Returns(false);
            transport2.Available().Returns(true);
            transport.Awake();

            transport.ClientConnect("some.server.com");

            transport1.DidNotReceive().ClientConnect(Arg.Any<string>());
            transport2.Received().ClientConnect("some.server.com");
        }

        [Test]
        public void TestConnected()
        {
            transport1.Available().Returns(true);
            transport.Awake();
            transport.ClientConnect("some.server.com");

            transport1.ClientConnected().Returns(true);

            Assert.That(transport.ClientConnected());
        }

        [Test]
        public void TestDisconnect()
        {
            transport1.Available().Returns(true);
            transport.Awake();
            transport.ClientConnect("some.server.com");

            transport.ClientDisconnect();

            transport1.Received().ClientDisconnect();
        }

        [Test]
        public void TestClientSend()
        {
            transport1.Available().Returns(true);
            transport.Awake();

            transport.ClientConnect("some.server.com");

            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            transport.ClientSend(3, segment);

            transport1.Received().ClientSend(3, segment);
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

        #endregion

        #region Server tests

        [Test]
        public void TestServerConnected()
        {
            byte[] data = { 1, 2, 3 };
            ArraySegment<byte> segment = new ArraySegment<byte>(data);

            transport1.Available().Returns(true);
            transport2.Available().Returns(true);
            // find available
            transport.Awake();


            // on connect, send a message back
            void SendMessage(int connectionId)
            {
                transport.ServerSend(connectionId, 5, segment);
            }

            // set event and Start to give event to inner
            transport.OnServerConnected = SendMessage;
            transport.ServerStart();

            transport1.OnServerConnected.Invoke(1);

            transport1.Received().ServerSend(1, 5, segment);
        }


        #endregion

    }
}
