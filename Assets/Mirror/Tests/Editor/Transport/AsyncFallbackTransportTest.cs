using System;
using System.Collections;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    public class AsyncFallbackTransportTest
    {
        #region SetUp

        private GameObject transportObj;
        private AsyncFallbackTransport transport;

        private AsyncTransport transport1;
        private AsyncTransport transport2;

        IConnection conn1;
        IConnection conn2;

        [SetUp]
        public void Setup()
        {
            transportObj = new GameObject();

            transport = transportObj.AddComponent<AsyncFallbackTransport>();

            // this gives warnings,  it is ok
            transport1 = Substitute.For<AsyncTransport>();
            transport2 = Substitute.For<AsyncTransport>();

            transport.transports = new[] { transport1, transport2 };
            conn1 = Substitute.For<IConnection>();
            conn2 = Substitute.For<IConnection>();

        }

        [TearDown]
        public void TearDown()
        { 
            Object.DestroyImmediate(transportObj);
        }
        #endregion

        [UnityTest]
        public IEnumerator AcceptTransport1() => RunAsync(async () =>
        {
            transport1.AcceptAsync().Returns(Task.FromResult(conn1));

            Assert.That(await transport.AcceptAsync(), Is.SameAs(conn1));
        });

        [UnityTest]
        public IEnumerator AcceptTransport2() => RunAsync(async () =>
        {
            transport2.AcceptAsync().Returns(Task.FromResult(conn1));
            // transport1 task never ends
            transport1.AcceptAsync().Returns(Task.FromException<IConnection>(new PlatformNotSupportedException()));
            Assert.That(await transport.AcceptAsync(), Is.SameAs(conn1));
        });

        [UnityTest]
        public IEnumerator AcceptMultiple() => RunAsync(async () =>
        {
            transport1.AcceptAsync().Returns(Task.FromResult(conn1), Task.FromResult(conn2));
            // transport2 task never ends
            transport2.AcceptAsync().Returns(Task.FromException<IConnection>(new PlatformNotSupportedException()));
            Assert.That(await transport.AcceptAsync(), Is.SameAs(conn1));
            Assert.That(await transport.AcceptAsync(), Is.SameAs(conn2));
        });

        [UnityTest]
        public IEnumerator AcceptInvalid() => RunAsync(async () =>
        {
            transport1.AcceptAsync().Returns(Task.FromException<IConnection>(new PlatformNotSupportedException()));
            // transport2 task never ends
            transport2.AcceptAsync().Returns(Task.FromException<IConnection>(new PlatformNotSupportedException()));

            try
            {
                IConnection accepted2 = await transport.AcceptAsync();
                Assert.Fail("IF no sub transport is supported transport is not supported");
            }
            catch (PlatformNotSupportedException)
            {
                // expected
            }
        });

        [UnityTest]
        public IEnumerator AcceptUntilAllGone() => RunAsync(async () =>
        {
            transport1.AcceptAsync().Returns(x => Task.FromResult(conn1), x => Task.FromResult<IConnection>(null));
            // transport2 task never ends
            transport2.AcceptAsync().Returns(x => Task.FromResult(conn2), x => Task.FromResult<IConnection>(null));

            Assert.That(await transport.AcceptAsync(), Is.SameAs(conn1));
            Assert.That(await transport.AcceptAsync(), Is.Null);
        });

        [UnityTest]
        public IEnumerator Listen1() => RunAsync(async () =>
        {
            transport1.ListenAsync().Returns(Task.CompletedTask);
            transport2.ListenAsync().Returns(Task.CompletedTask);
            await transport.ListenAsync();

            _ = transport1.Received().ListenAsync();
            _ = transport2.Received(0).ListenAsync();

        });

        [UnityTest]
        public IEnumerator Listen2() => RunAsync(async () =>
        {
            transport1.ListenAsync().Returns(Task.FromException(new PlatformNotSupportedException()));
            transport2.ListenAsync().Returns(Task.CompletedTask);
            await transport.ListenAsync();

            _ = transport1.Received().ListenAsync();
            _ = transport2.Received().ListenAsync();

        });

        [UnityTest]
        public IEnumerator ListenNone() => RunAsync(async () =>
        {
            transport1.ListenAsync().Returns(Task.FromException(new PlatformNotSupportedException()));
            transport2.ListenAsync().Returns(Task.FromException(new PlatformNotSupportedException()));

            try
            {
                await transport.ListenAsync();
                Assert.Fail("IF no sub transport is supported transport is not supported");
            }
            catch (PlatformNotSupportedException)
            {
                // expected
            }

        });

        [Test]
        public void Disconnect1()
        {
            transport.Disconnect();
            transport1.Received().Disconnect();
            transport2.Received(0).Disconnect();
        }

        [Test]
        public void Disconnect2()
        {
            transport1
                .When( x=> x.Disconnect())
                .Do(x => { throw new PlatformNotSupportedException(); });

            transport.Disconnect();
            transport1.Received().Disconnect();
            transport2.Received().Disconnect();
        }

        [Test]
        public void DisconnectNone()
        {
            transport1
                .When(x => x.Disconnect())
                .Do(x => { throw new PlatformNotSupportedException(); });
            transport2
                .When(x => x.Disconnect())
                .Do(x => { throw new PlatformNotSupportedException(); });

            Assert.Throws<PlatformNotSupportedException>(() =>
            {
               transport.Disconnect();
            });
        }

        [Test]
        public void ServerUri1()
        {
            transport1.ServerUri().Returns(new Uri("tcp4://myserver"));

            Assert.That(transport.ServerUri(), Is.EqualTo(new Uri("tcp4://myserver")));
        }

        [Test]
        public void ServerUri2()
        {
            transport1.ServerUri().Returns(x => { throw new PlatformNotSupportedException(); });
            transport2.ServerUri().Returns(new Uri("tcp4://myserver"));

            Assert.That(transport.ServerUri(), Is.EqualTo(new Uri("tcp4://myserver")));
        }


        [Test]
        public void ServerUriNone()
        {
            transport1.ServerUri().Returns(x => { throw new PlatformNotSupportedException(); });
            transport2.ServerUri().Returns(x => { throw new PlatformNotSupportedException(); });

            Assert.Throws<PlatformNotSupportedException>(() =>
            {
                _ = transport.ServerUri();
            });
        }


        [Test]
        public void Scheme1()
        {
            transport1.Scheme.Returns("tcp4");

            Assert.That(transport.Scheme, Is.EqualTo("tcp4"));
        }

        [Test]
        public void Scheme2()
        {
            transport1.Scheme.Returns(x => { throw new PlatformNotSupportedException(); });
            transport2.Scheme.Returns("tcp4");

            Assert.That(transport.Scheme, Is.EqualTo("tcp4"));
        }


        [Test]
        public void SchemeNone()
        {
            transport1.Scheme.Returns(x => { throw new PlatformNotSupportedException(); });
            transport2.Scheme.Returns(x => { throw new PlatformNotSupportedException(); });

            Assert.Throws<PlatformNotSupportedException>(() =>
            {
                _ = transport.Scheme;
            });
        }

        [UnityTest]
        public IEnumerator Connect() => RunAsync(async () =>
        {
            transport1.ConnectAsync(Arg.Any<Uri>())
                .Returns(Task.FromException<IConnection>(new PlatformNotSupportedException("Invalid protocol")));

            // transport2 gives a connection
            transport2.ConnectAsync(Arg.Any<Uri>())
                .Returns(Task.FromResult(conn2));

            IConnection accepted1 = await transport.ConnectAsync(new Uri("tcp4://localhost"));

            Assert.That(accepted1, Is.SameAs(conn2));
        });

        [UnityTest]
        public IEnumerator CannotConnect() => RunAsync(async () =>
        {
            transport1.ConnectAsync(Arg.Any<Uri>())
                .Returns(Task.FromException<IConnection>(new PlatformNotSupportedException("Invalid protocol")));

            // transport2 gives a connection
            transport2.ConnectAsync(Arg.Any<Uri>())
                .Returns(Task.FromException<IConnection>(new PlatformNotSupportedException("Invalid protocol")));

            try
            {
                _ = await transport.ConnectAsync(new Uri("tcp4://localhost"));
                Assert.Fail("Should not be able to connect if none of the transports can connect");
            }
            catch (PlatformNotSupportedException)
            {
                // ok
            }
        });
    }
}
