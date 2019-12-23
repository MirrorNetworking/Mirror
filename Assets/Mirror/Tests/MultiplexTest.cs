using System;
using System.Collections;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class MultiplexTest
    {

        Transport transport1;
        Transport transport2;
        MultiplexTransport transport;

        [SetUp]
        public void SetupMultipex()
        {
            transport1 = Substitute.For<Transport>();
            transport2 = Substitute.For<Transport>();

            GameObject gameObject = new GameObject();

            transport = gameObject.AddComponent<MultiplexTransport>();
            transport.transports = new []{ transport1, transport2 };

            transport.Awake();
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
    }
}
