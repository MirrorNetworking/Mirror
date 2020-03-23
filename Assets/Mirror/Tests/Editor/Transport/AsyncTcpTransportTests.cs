using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Mirror.AsyncTcp;
using System.Text;
using System.IO;

using static Mirror.Tests.AsyncUtil;
using System;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    public class AsyncTcpTransportTests
    {
        #region SetUp

        private AsyncTransport transport;
        private GameObject transportObj;

        [SetUp]
        public void Setup()
        {
            transportObj = new GameObject();

            AsyncTcpTransport tcpTransport = transportObj.AddComponent<AsyncTcpTransport>();
            tcpTransport.Port = 8798;

            transport = tcpTransport;
        }

        [TearDown]
        public void TearDown()
        {
            transport.Disconnect();
            Object.DestroyImmediate(transportObj);
        }
        #endregion

        [UnityTest]
        public IEnumerator ClientToServerTest()
        {
            return RunAsync(async () =>
            {
                await transport.ListenAsync();
                IConnection clientConnection = await transport.ConnectAsync(new Uri("tcp4://localhost:8798"));
                IConnection serverConnection = await transport.AcceptAsync();

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
        }

        [UnityTest]
        public IEnumerator ClientToServerMultipleTest()
        {
            return RunAsync(async () =>
            {
                await transport.ListenAsync();
                IConnection clientConnection = await transport.ConnectAsync(new Uri("tcp4://localhost:8798"));
                IConnection serverConnection = await transport.AcceptAsync();

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
        }

        [UnityTest]
        public IEnumerator ServerToClientTest()
        {
            return RunAsync(async () =>
            {
                await transport.ListenAsync();
                IConnection clientConnection = await transport.ConnectAsync(new Uri("tcp4://localhost:8798"));
                IConnection serverConnection = await transport.AcceptAsync();

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
        }

        [UnityTest]
        public IEnumerator DisconnectServerTest()
        {
            return RunAsync(async () =>
            {
                await transport.ListenAsync();
                IConnection clientConnection = await transport.ConnectAsync(new Uri("tcp4://localhost:8798"));
                IConnection serverConnection = await transport.AcceptAsync();

                serverConnection.Disconnect();

                var stream = new MemoryStream();
                Assert.That(await clientConnection.ReceiveAsync(stream), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator DisconnectClientTest()
        {
            return RunAsync(async () =>
            {
                await transport.ListenAsync();
                IConnection clientConnection = await transport.ConnectAsync(new Uri("tcp4://localhost:8798"));
                IConnection serverConnection = await transport.AcceptAsync();

                clientConnection.Disconnect();

                var stream = new MemoryStream();
                Assert.That(await serverConnection.ReceiveAsync(stream), Is.False);
            });
        }

    }
}
