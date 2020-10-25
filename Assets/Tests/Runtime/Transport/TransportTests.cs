using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text;
using System.IO;
using System.Net;
using System;
using Object = UnityEngine.Object;
using System.Linq;
using Cysharp.Threading.Tasks;
using Mirror.KCP;

namespace Mirror.Tests
{
    [TestFixture(typeof(KcpTransport), new[] { "kcp" }, "kcp://localhost", 7777)]
    public class AsyncTransportTests<T> where T : Transport
    {
        #region SetUp

        private T transport;
        private GameObject transportObj;
        private readonly Uri uri;
        private readonly int port;
        private readonly string[] scheme;

        public AsyncTransportTests(string[] scheme, string uri, int port)
        {
            this.scheme = scheme;
            this.uri = new Uri(uri);
            this.port = port;
        }

        IConnection clientConnection;
        IConnection serverConnection;

        [UnitySetUp]
        public IEnumerator Setup() => UniTask.ToCoroutine(async () =>
        {
            transportObj = new GameObject();

            transport = transportObj.AddComponent<T>();

            await transport.ListenAsync();
            UniTask<IConnection> connectTask = transport.ConnectAsync(uri);
            UniTask<IConnection> acceptTask = transport.AcceptAsync();

            clientConnection = await connectTask;
            serverConnection = await acceptTask;
        });


        [TearDown]
        public void TearDown()
        {
            clientConnection.Disconnect();
            serverConnection.Disconnect();
            transport.Disconnect();
            Object.DestroyImmediate(transportObj);
        }

        #endregion

        [UnityTest]
        public IEnumerator ClientToServerTest() => UniTask.ToCoroutine(async () =>
        {
            Encoding utf8 = Encoding.UTF8;
            string message = "Hello from the client";
            byte[] data = utf8.GetBytes(message);
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var stream = new MemoryStream();

            await serverConnection.ReceiveAsync(stream);
            byte[] received = stream.ToArray();
            Assert.That(received, Is.EqualTo(data));
        });

        [Test]
        public void EndpointAddress()
        {
            // should give either IPv4 or IPv6 local address
            var endPoint = (IPEndPoint)serverConnection.GetEndPointAddress();

            IPAddress ipAddress = endPoint.Address;

            if (ipAddress.IsIPv4MappedToIPv6)
            {
                // mono IsLoopback seems buggy,
                // it does not detect loopback with mapped ipv4->ipv6 addresses
                // so map it back down to IPv4
                ipAddress = ipAddress.MapToIPv4();
            }

            Assert.That(IPAddress.IsLoopback(ipAddress), "Expected loopback address but got {0}", ipAddress);
            // random port
        }

        [UnityTest]
        public IEnumerator ClientToServerMultipleTest() => UniTask.ToCoroutine(async () =>
        {
            Encoding utf8 = Encoding.UTF8;
            string message = "Hello from the client 1";
            byte[] data = utf8.GetBytes(message);
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            string message2 = "Hello from the client 2";
            byte[] data2 = utf8.GetBytes(message2);
            await clientConnection.SendAsync(new ArraySegment<byte>(data2));

            var stream = new MemoryStream();

            await serverConnection.ReceiveAsync(stream);
            byte[] received = stream.ToArray();
            Assert.That(received, Is.EqualTo(data));

            stream.SetLength(0);
            await serverConnection.ReceiveAsync(stream);
            byte[] received2 = stream.ToArray();
            Assert.That(received2, Is.EqualTo(data2));
        });

        [UnityTest]
        public IEnumerator ServerToClientTest() => UniTask.ToCoroutine(async () =>
        {
            Encoding utf8 = Encoding.UTF8;
            string message = "Hello from the server";
            byte[] data = utf8.GetBytes(message);
            await serverConnection.SendAsync(new ArraySegment<byte>(data));

            var stream = new MemoryStream();

            await clientConnection.ReceiveAsync(stream);
            byte[] received = stream.ToArray();
            Assert.That(received, Is.EqualTo(data));
        });

        [UnityTest]
        public IEnumerator DisconnectServerTest() => UniTask.ToCoroutine(async () =>
        {
            serverConnection.Disconnect();

            var stream = new MemoryStream();
            try
            {
                await clientConnection.ReceiveAsync(stream);
                Assert.Fail("ReceiveAsync should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [UnityTest]
        public IEnumerator DisconnectClientTest() => UniTask.ToCoroutine(async () =>
        {
            clientConnection.Disconnect();

            var stream = new MemoryStream();
            try
            {
                await serverConnection.ReceiveAsync(stream);
                Assert.Fail("ReceiveAsync should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [UnityTest]
        public IEnumerator DisconnectClientTest2() => UniTask.ToCoroutine(async () =>
        {
            clientConnection.Disconnect();

            var stream = new MemoryStream();
            try
            {
                await clientConnection.ReceiveAsync(stream);
                Assert.Fail("ReceiveAsync should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [Test]
        public void TestServerUri()
        {
            Uri serverUri = transport.ServerUri().First();

            Assert.That(serverUri.Port, Is.EqualTo(port));
            Assert.That(serverUri.Host, Is.EqualTo(Dns.GetHostName()).IgnoreCase);
            Assert.That(serverUri.Scheme, Is.EqualTo(uri.Scheme));
        }

        [Test]
        public void TestScheme()
        {
            Assert.That(transport.Scheme, Is.EquivalentTo(scheme));
        }
    }
}

