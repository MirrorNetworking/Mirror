using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    [Timeout(2000)]
    public class MultiplexTransportTest
    {
        #region SetUp

        private GameObject transportObj;
        private MultiplexTransport transport;

        private Transport transport1;
        private Transport transport2;

        IConnection conn1;
        IConnection conn2;

        [SetUp]
        public void Setup()
        {
            transportObj = new GameObject();

            transport = transportObj.AddComponent<MultiplexTransport>();

            // this gives warnings,  it is ok
            transport1 = Substitute.For<Transport>();
            transport2 = Substitute.For<Transport>();

            transport1.Supported.Returns(true);
            transport2.Supported.Returns(true);

            transport.transports = new[] { transport1, transport2 };
            conn1 = Substitute.For<IConnection>();
            conn2 = Substitute.For<IConnection>();

            transport.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(transportObj);
        }
        #endregion

        [Test]
        public void AcceptTransport1()
        {
            var connectedDelegate = Substitute.For<UnityAction<IConnection>>();

            transport.Connected.AddListener(connectedDelegate);
            transport1.Connected.Invoke(conn1);

            connectedDelegate.Received().Invoke(conn1);
        }

        [Test]
        public void AcceptTransport2()
        {
            var connectedDelegate = Substitute.For<UnityAction<IConnection>>();
            transport.Connected.AddListener(connectedDelegate);

            transport2.Connected.Invoke(conn1);

            connectedDelegate.Received().Invoke(conn1);
        }

        [Test]
        public void AcceptMultiple()
        {
            var connectedDelegate = Substitute.For<UnityAction<IConnection>>();
            transport.Connected.AddListener(connectedDelegate);

            transport1.Connected.Invoke(conn1);
            transport2.Connected.Invoke(conn2);

            connectedDelegate.Received().Invoke(conn1);
            connectedDelegate.Received().Invoke(conn2);
        }

        [Test]
        public void AcceptUntilAllGone()
        {
            var connectedDelegate = Substitute.For<UnityAction<IConnection>>();
            transport.Connected.AddListener(connectedDelegate);

            transport1.Connected.Invoke(conn1);
            transport1.Disconnect();
            transport2.Connected.Invoke(conn2);

            connectedDelegate.Received().Invoke(conn1);
            connectedDelegate.Received().Invoke(conn2);
        }

        [UnityTest]
        public IEnumerator Listen() => UniTask.ToCoroutine(async () =>
        {
            transport1.ListenAsync().Returns(UniTask.CompletedTask);
            transport2.ListenAsync().Returns(UniTask.CompletedTask);
            await transport.ListenAsync();

            transport1.Received().ListenAsync().Forget();
            transport2.Received().ListenAsync().Forget();

        });

        [Test]
        public void Disconnect()
        {
            transport.Disconnect();

            transport1.Received().Disconnect();
            transport2.Received().Disconnect();
        }

        [Test]
        public void ServerUri()
        {
            transport1.ServerUri().Returns(new[] { new Uri("kcp://myserver") });

            Assert.That(transport.ServerUri(), Is.EquivalentTo(new[] { new Uri("kcp://myserver") }));
        }

        [Test]
        public void Scheme1()
        {
            transport1.Scheme.Returns(new[] { "kcp" });

            Assert.That(transport.Scheme, Is.EquivalentTo(new[] { "kcp" }));
        }

        [Test]
        public void SchemeNone()
        {
            transport1.Scheme.Returns(new[] { "yomama" });
            transport2.Scheme.Returns(new[] { "pepe" });
            transport1.Supported.Returns(false);
            transport2.Supported.Returns(false);

            
            Assert.That(transport.Scheme, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Connect() => UniTask.ToCoroutine(async () =>
        {
            transport1.Scheme.Returns(new[] { "yomama" });
            transport2.Scheme.Returns(new[] { "kcp" });

            transport1.ConnectAsync(Arg.Any<Uri>())
                .Returns(UniTask.FromException<IConnection>(new ArgumentException("Invalid protocol")));

            // transport2 gives a connection
            transport2.ConnectAsync(Arg.Any<Uri>())
                .Returns(UniTask.FromResult(conn2));

            IConnection accepted1 = await transport.ConnectAsync(new Uri("kcp://localhost"));

            Assert.That(accepted1, Is.SameAs(conn2));
        });

        [UnityTest]
        public IEnumerator CannotConnect() => UniTask.ToCoroutine(async () =>
        {
            transport1.ConnectAsync(Arg.Any<Uri>())
                .Returns(UniTask.FromException<IConnection>(new ArgumentException("Invalid protocol")));

            // transport2 gives a connection
            transport2.ConnectAsync(Arg.Any<Uri>())
                .Returns(UniTask.FromException<IConnection>(new ArgumentException("Invalid protocol")));

            try
            {
                _ = await transport.ConnectAsync(new Uri("kcp://localhost"));
                Assert.Fail("Should not be able to connect if none of the transports can connect");
            }
            catch (ArgumentException)
            {
                // ok
            }
        });

        [Test]
        public void GetTransportTest()
        {
            Assert.That(transport.GetTransport() == transport1 || transport.GetTransport() == transport2);
        }

        [Test]
        public void GetTransportExceptionTest()
        {
            transport.transports = new Transport[0];

            Assert.Throws<PlatformNotSupportedException>(() =>
            {
                transport.GetTransport();
            });
        }
    }
}
