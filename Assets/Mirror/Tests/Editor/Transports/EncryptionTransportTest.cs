using System;
using System.Collections.Generic;
using Mirror.Transports.Encryption;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Transports
{
    public class EncryptionTransportTest
    {
        Transport inner;
        EncryptionTransport encryption;

        [SetUp]
        public void Setup()
        {
            GameObject gameObject = new GameObject();
            inner = gameObject.AddComponent<MemoryTransport>();
            encryption = gameObject.AddComponent<EncryptionTransport>();
            encryption.inner = inner;
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(encryption.gameObject);
        }

        [Test]
        public void TestBasic()
        {
            var message = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

            var serverCreds = EncryptionCredentials.Generate();
            Queue<byte[]> clientRecv = new Queue<byte[]>();
            Queue<byte[]> serverRecv = new Queue<byte[]>();
            EncryptedConnection server = null;
            bool serverDone = false;
            bool clientDone = false;
            server = new EncryptedConnection(serverCreds, false,
                (bytes, i) => { clientRecv.Enqueue(bytes.ToArray()); },
                (bytes, i) =>
                {
                    Assert.That(message, Is.EqualTo(message));
                    serverDone = true;
                },
                () => { server.Send(message, Channels.Unreliable); },
                (error, s) => throw new Exception($"{error}: {s}"));
            var clientCreds = EncryptionCredentials.Generate();
            EncryptedConnection client = null;;
            client =  new EncryptedConnection(clientCreds, true,
                (bytes, i) =>
                {
                    serverRecv.Enqueue(bytes.ToArray());
                },
                (bytes, i) => {
                    Assert.That(message, Is.EqualTo(message));
                    clientDone = true;

                },
                () => { client.Send(message, Channels.Unreliable); },
                (error, s) => throw new Exception($"{error}: {s}"));
            double time = 1;
            while (!serverDone || !clientDone)
            {
                if (time > 20)
                {
                    throw new Exception("Timeout.");
                }
                time += 0.05;
                while (clientRecv.TryDequeue(out var data))
                {
                    client.OnReceiveRaw(data, Channels.Unreliable);
                }
                client.Tick(time);

                while (serverRecv.TryDequeue(out var data))
                {
                    server.OnReceiveRaw(data, Channels.Unreliable);
                }
                server.Tick(time);
            }
        }
    }
}
