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
            
            await transport.ListenAsync();

            UniTask<IConnection> acceptTask = transport.AcceptAsync();
            var uriBuilder = new UriBuilder
            {
                Host = "localhost",
                Scheme = "kcp",
                Port = port
            };

            testUri = uriBuilder.Uri;

            UniTask<IConnection> connectTask = transport.ConnectAsync(uriBuilder.Uri);

            serverConnection = (KcpConnection)await acceptTask;
            clientConnection = (KcpConnection)await connectTask;

            // for our tests,  lower the timeout to just 0.1s
            // so that the tests run quickly.
            serverConnection.Timeout = 500;
            clientConnection.Timeout = 500;
        });

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            clientConnection?.Disconnect();
            serverConnection?.Disconnect();
            transport.Disconnect();

            UnityEngine.Object.Destroy(transport.gameObject);
            // wait a frame so object will be destroyed

            yield return null;
        }

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
            byte[] data = { (byte)Random.Range(1, 255) };
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await serverConnection.ReceiveAsync(buffer);

            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
        });

        [UnityTest]
        public IEnumerator SendDataFromServer() => UniTask.ToCoroutine(async () =>
        {
            byte[] data = { (byte)Random.Range(1, 255) };
            await serverConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await clientConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
        });

        [UnityTest]
        public IEnumerator SendUnreliableDataFromServer() => UniTask.ToCoroutine(async () =>
        {
            byte[] data = { (byte)Random.Range(1, 255) };
            await serverConnection.SendAsync(new ArraySegment<byte>(data), Channel.Unreliable);

            var buffer = new MemoryStream();
            (bool next, int channel) = await clientConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
            Assert.That(channel, Is.EqualTo(Channel.Unreliable));
        });

        [UnityTest]
        public IEnumerator SendUnreliableDataFromClient() => UniTask.ToCoroutine(async () =>
        {
            byte[] data = { (byte)Random.Range(1, 255) };
            await clientConnection.SendAsync(new ArraySegment<byte>(data), Channel.Unreliable);

            var buffer = new MemoryStream();
            (bool next, int channel) = await serverConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
            Assert.That(channel, Is.EqualTo(Channel.Unreliable));
        });


        [UnityTest]
        public IEnumerator DisconnectFromServer() => UniTask.ToCoroutine(async () =>
        {
            serverConnection.Disconnect();

            var buffer = new MemoryStream();
            bool more = (await clientConnection.ReceiveAsync(buffer)).next;

            Assert.That(more, Is.False, "Receive should return false when the connection is disconnected");
        });

        [UnityTest]
        public IEnumerator DisconnectFromClient() => UniTask.ToCoroutine(async () =>
        {
            clientConnection.Disconnect();

            var buffer = new MemoryStream();
            bool more = (await serverConnection.ReceiveAsync(buffer)).next;

            Assert.That(more, Is.False, "Receive should return false when the connection is disconnected");
        });

        [UnityTest]
        public IEnumerator DisconnectServerFromIdle() => UniTask.ToCoroutine(async () =>
        {
            var buffer = new MemoryStream();
            bool more = (await serverConnection.ReceiveAsync(buffer)).next;

            Assert.That(more, Is.False, "After some time of no activity, the server should disconnect");
        });

        [UnityTest]
        public IEnumerator DisconnectClientFromIdle() => UniTask.ToCoroutine(async () =>
        {
            // after certain amount of time with no messages, it should disconnect
            var buffer = new MemoryStream();
            bool more = (await clientConnection.ReceiveAsync(buffer)).next;

            Assert.That(more, Is.False, "After some time of no activity, the client should disconnect");
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
            while ((await serverConnection.ReceiveAsync(buffer)).next)
            {
                // just keep waiting until no more messages are received
            }

            Assert.That(transport.connectedClients, Is.Empty);
        });
    }
}
