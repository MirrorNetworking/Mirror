using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
using System;
using System.IO;

using UnityEngine;
using Random = UnityEngine.Random;
using System.Linq;
using Mirror.KCP;

namespace Mirror.Tests
{
    public class KcpTransportTest
    {
        public ushort port = 7896;

        KcpTransport transport;
        KcpConnection clientConnection;
        KcpConnection serverConnection;

        Uri testUri;

        UniTask listenTask;

        byte[] data;

        [UnitySetUp]
        public IEnumerator Setup() => UniTask.ToCoroutine(async () =>
        {
            // each test goes in a different port
            // that way the transports can take some time to cleanup
            // without interfering with each other.
            port++;

            var transportGo = new GameObject("kcpTransport", typeof(KcpTransport));

            transport = transportGo.GetComponent<KcpTransport>();

            transport.Port = port;
            // speed this up
            transport.HashCashBits = 3;
       
            transport.Connected.AddListener(connection => serverConnection = (KcpConnection)connection);

            listenTask = transport.ListenAsync();

            var uriBuilder = new UriBuilder
            {
                Host = "localhost",
                Scheme = "kcp",
                Port = port
            };

            testUri = uriBuilder.Uri;

            clientConnection = (KcpConnection)await transport.ConnectAsync(uriBuilder.Uri);

            await UniTask.WaitUntil(() => serverConnection != null);

            // for our tests,  lower the timeout to just 0.1s
            // so that the tests run quickly.
            serverConnection.Timeout = 500;
            clientConnection.Timeout = 500;

            data = new byte[Random.Range(10, 255)];
            for (int i=0; i< data.Length; i++)
                data[i] = (byte)Random.Range(1, 255);
        });

        [UnityTearDown]
        public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
        {
            clientConnection?.Disconnect();
            serverConnection?.Disconnect();
            transport.Disconnect();

            await listenTask;
            UnityEngine.Object.Destroy(transport.gameObject);
            // wait a frame so object will be destroyed
        });

        // A Test behaves as an ordinary method
        [Test]
        public void Connect()
        {
            Assert.That(clientConnection, Is.Not.Null);
            Assert.That(serverConnection, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SendDataFromClient() => UniTask.ToCoroutine(async () =>
        {
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await serverConnection.ReceiveAsync(buffer);

            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
        });

        [UnityTest]
        public IEnumerator SendDataFromServer() => UniTask.ToCoroutine(async () =>
        {
            await serverConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await clientConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
        });

        [UnityTest]
        public IEnumerator ReceivedBytes() => UniTask.ToCoroutine(async () =>
        {
            long received = transport.ReceivedBytes;
            Assert.That(received, Is.GreaterThan(0), "Must have received some bytes to establish the connection");

            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await serverConnection.ReceiveAsync(buffer);

            Assert.That(transport.ReceivedBytes, Is.GreaterThan(received + data.Length), "Client sent data,  we should have received");

        });

        [UnityTest]
        public IEnumerator SentBytes() => UniTask.ToCoroutine(async () =>
        {
            long sent = transport.SentBytes;
            Assert.That(sent, Is.GreaterThan(0), "Must have received some bytes to establish the connection");

            await serverConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await clientConnection.ReceiveAsync(buffer);

            Assert.That(transport.SentBytes, Is.GreaterThan(sent + data.Length), "Client sent data,  we should have received");

        });

        [UnityTest]
        public IEnumerator SendUnreliableDataFromServer() => UniTask.ToCoroutine(async () =>
        {
            await serverConnection.SendAsync(new ArraySegment<byte>(data), Channel.Unreliable);

            var buffer = new MemoryStream();
            int channel = await clientConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
            Assert.That(channel, Is.EqualTo(Channel.Unreliable));
        });

        [UnityTest]
        public IEnumerator SendUnreliableDataFromClient() => UniTask.ToCoroutine(async () =>
        {
            await clientConnection.SendAsync(new ArraySegment<byte>(data), Channel.Unreliable);

            var buffer = new MemoryStream();
            int channel = await serverConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
            Assert.That(channel, Is.EqualTo(Channel.Unreliable));
        });


        [UnityTest]
        public IEnumerator DisconnectFromServer() => UniTask.ToCoroutine(async () =>
        {
            serverConnection.Disconnect();

            var buffer = new MemoryStream();
            try
            {
                await clientConnection.ReceiveAsync(buffer);
                Assert.Fail("ReceiveAsync should throw EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [UnityTest]
        public IEnumerator DisconnectFromClient() => UniTask.ToCoroutine(async () =>
        {
            clientConnection.Disconnect();

            var buffer = new MemoryStream();
            try
            {
                await serverConnection.ReceiveAsync(buffer);
                Assert.Fail("ReceiveAsync should throw EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [UnityTest]
        public IEnumerator DisconnectServerFromIdle() => UniTask.ToCoroutine(async () =>
        {
            var buffer = new MemoryStream();
            try
            {
                await serverConnection.ReceiveAsync(buffer);
                Assert.Fail("ReceiveAsync should throw EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [UnityTest]
        public IEnumerator DisconnectClientFromIdle() => UniTask.ToCoroutine(async () =>
        {
            // after certain amount of time with no messages, it should disconnect
            var buffer = new MemoryStream();
            try
            {
                await clientConnection.ReceiveAsync(buffer);
                Assert.Fail("ReceiveAsync should throw EndOfStreamException");
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
            Assert.That(serverUri.Scheme, Is.EqualTo(testUri.Scheme));
        }

        [Test]
        public void IsSupportedTest()
        {
            Assert.That(transport.Supported, Is.True);
        }

        [UnityTest]
        public IEnumerator ConnectionsDontLeak() => UniTask.ToCoroutine(async () =>
        {
            serverConnection.Disconnect();

            var buffer = new MemoryStream();

            try
            {
                while (true)
                {
                    await serverConnection.ReceiveAsync(buffer);
                }
            }
            catch (EndOfStreamException)
            {
                // connection is now successfully closed
            }

            Assert.That(transport.connectedClients, Is.Empty);
        });
    }
}
