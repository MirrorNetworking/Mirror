using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Mirror.AsyncTcp;
using System.Text;
using System.IO;
using System.Net;

using static Mirror.Tests.AsyncUtil;
using System;
using Object = UnityEngine.Object;
using Mirror.Websocket;
using System.Threading.Tasks;

namespace Mirror.Tests
{
    [TestFixture(typeof(AsyncTcpTransport), "tcp4", "tcp4://localhost", 7777)]
    [TestFixture(typeof(AsyncWsTransport), "ws", "ws://localhost", 7778)]
    public class AsyncTransportTests<T> where T: AsyncTransport
    {
        #region SetUp

        private T transport;
        private GameObject transportObj;
        private Uri uri;
        private int port;
        private string scheme;

        public AsyncTransportTests(string scheme, string uri, int port)
        {
            this.scheme = scheme;
            this.uri = new Uri(uri);
            this.port = port;
        }

        IConnection clientConnection;
        IConnection serverConnection;

        [UnitySetUp]
        public IEnumerator Setup() => RunAsync(async () =>
        {
            transportObj = new GameObject();

            transport = transportObj.AddComponent<T>();

            await transport.ListenAsync();
            Task<IConnection> connectTask = transport.ConnectAsync(uri);
            Task<IConnection> acceptTask = transport.AcceptAsync();

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
        public IEnumerator ClientToServerTest() => RunAsync(async () =>
        {
            Encoding utf8 = Encoding.UTF8;
            string message = "Hello from the client";
            byte[] data = utf8.GetBytes(message);
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var stream = new MemoryStream();

            Assert.That(await serverConnection.ReceiveAsync(stream), Is.True);
            byte[] received = stream.ToArray();
            string receivedData = utf8.GetString(received);
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
        public IEnumerator ClientToServerMultipleTest() => RunAsync(async () =>
        {
            Encoding utf8 = Encoding.UTF8;
            string message = "Hello from the client 1";
            byte[] data = utf8.GetBytes(message);
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            string message2 = "Hello from the client 2";
            byte[] data2 = utf8.GetBytes(message2);
            await clientConnection.SendAsync(new ArraySegment<byte>(data2));

            var stream = new MemoryStream();

            Assert.That(await serverConnection.ReceiveAsync(stream), Is.True);
            byte[] received = stream.ToArray();
            string receivedData = utf8.GetString(received);
            Assert.That(received, Is.EqualTo(data));

            stream.SetLength(0);
            Assert.That(await serverConnection.ReceiveAsync(stream), Is.True);
            byte[] received2 = stream.ToArray();
            string receivedData2 = utf8.GetString(received2);
            Assert.That(received2, Is.EqualTo(data2));
        });

        [UnityTest]
        public IEnumerator ServerToClientTest() => RunAsync(async () =>
        {
            Encoding utf8 = Encoding.UTF8;
            string message = "Hello from the server";
            byte[] data = utf8.GetBytes(message);
            await serverConnection.SendAsync(new ArraySegment<byte>(data));

            var stream = new MemoryStream();

            Assert.That(await clientConnection.ReceiveAsync(stream), Is.True);
            byte[] received = stream.ToArray();
            string receivedData = utf8.GetString(received);
            Assert.That(received, Is.EqualTo(data));
        });

        [UnityTest]
        public IEnumerator DisconnectServerTest() => RunAsync(async () =>
        {
            serverConnection.Disconnect();

            var stream = new MemoryStream();
            Assert.That(await clientConnection.ReceiveAsync(stream), Is.False);
        });

        [UnityTest]
        public IEnumerator DisconnectClientTest() => RunAsync(async () =>
        {
            clientConnection.Disconnect();

            var stream = new MemoryStream();
            Assert.That(await serverConnection.ReceiveAsync(stream), Is.False);
        });

        [UnityTest]
        public IEnumerator DisconnectClientTest2() => RunAsync(async () =>
        {
            clientConnection.Disconnect();

            var stream = new MemoryStream();
            Assert.That(await clientConnection.ReceiveAsync(stream), Is.False);
        });

        [Test]
        public void TestServerUri()
        {
            Uri serverUri = transport.ServerUri();

            Assert.That(serverUri.Port, Is.EqualTo(port));
            Assert.That(serverUri.Host, Is.EqualTo(Dns.GetHostName()).IgnoreCase);
            Assert.That(serverUri.Scheme, Is.EqualTo(uri.Scheme));
        }

        [Test]
        public void TestScheme()
        {
            Assert.That(transport.Scheme, Is.EqualTo(scheme));
        }
    }
}

