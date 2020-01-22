using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror.Tests
{
    public class FallbackTransportTest
    {

        Transport transport1;
        Transport transport2;
        FallbackTransport transport;

        [SetUp]
        public void SetupMultipex()
        {
            transport1 = Substitute.For<Transport>();
            transport2 = Substitute.For<Transport>();

            var gameObject = new GameObject();

            transport = gameObject.AddComponent<FallbackTransport>();
            transport.transports = new[] { transport1, transport2 };
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
            var segment = new ArraySegment<byte>(data);

            transport.ClientSend(3, segment);

            transport1.Received().ClientSend(3, segment);
        }

        [Test]
        public void TestClient1Connected()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(true);

            UnityAction callback = Substitute.For<UnityAction>();
            transport.Awake();
            transport.OnClientConnected.AddListener(callback);
            transport1.OnClientConnected.Invoke();
            callback.Received().Invoke();
        }

        [Test]
        public void TestClient2Connected()
        {
            transport1.Available().Returns(true);
            transport2.Available().Returns(true);

            UnityAction callback = Substitute.For<UnityAction>();
            transport.Awake();
            transport.OnClientConnected.AddListener(callback);
            transport2.OnClientConnected.Invoke();
            callback.Received().Invoke();
        }

        #endregion

        #region Server tests

        [Test]
        public void TestServerConnected()
        {
            byte[] data = { 1, 2, 3 };
            var segment = new ArraySegment<byte>(data);

            transport1.Available().Returns(true);
            transport2.Available().Returns(true);
            transport.Awake();

            // on connect, send a message back
            void SendMessage(int connectionId)
            {
                var connectionIds = new List<int>(new[] { connectionId });
                transport.ServerSend(connectionIds, 5, segment);
            }

            transport.OnServerConnected.AddListener(SendMessage);

            transport1.OnServerConnected.Invoke(1);

            int[] expectedIds = { 1 };

            transport1.Received().ServerSend(
                Arg.Is<List<int>>(x => x.SequenceEqual(expectedIds)),
                5,
                segment);
        }


        #endregion

    }
}
