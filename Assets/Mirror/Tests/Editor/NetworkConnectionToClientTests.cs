using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkConnectionToClientTests
    {
        GameObject transportGO;
        MemoryTransport transport;
        List<byte[]> clientReceived = new List<byte[]>();

        [SetUp]
        public void SetUp()
        {
            // transport is needed by server and client.
            // it needs to be on a gameobject because client.connect enables it,
            // which throws a NRE if not on a gameobject
            transportGO = new GameObject();
            Transport.activeTransport = transport = transportGO.AddComponent<MemoryTransport>();
            transport.OnClientDataReceived = (message, channelId) => {
                byte[] array = new byte[message.Count];
                Buffer.BlockCopy(message.Array, message.Offset, array, 0, message.Count);
                clientReceived.Add(array);
            };
            transport.ServerStart();
            transport.ClientConnect("localhost");
            Assert.That(transport.ServerActive, Is.True);
            Assert.That(transport.ClientConnected, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            clientReceived.Clear();
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void Send_BatchesUntilUpdate()
        {
            // create connection and send
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, 0);
            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Send() should only add to batch, not send anything yet
            transport.LateUpdate();
            Assert.That(clientReceived.Count, Is.EqualTo(0));

            // updating the connection should now send
            connection.Update();
            transport.LateUpdate();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }

        [Test]
        public void Send_BatchesUntilInterval()
        {
            // create connection and send
            int intervalMilliseconds = 10;
            float intervalSeconds = intervalMilliseconds / 1000f;
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, intervalSeconds);
            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Send() and update shouldn't send yet until interval elapsed
            connection.Update();
            transport.LateUpdate();
            Assert.That(clientReceived.Count, Is.EqualTo(0));

            // wait 'interval'
            Thread.Sleep(intervalMilliseconds);

            // updating again should flush out the batch
            connection.Update();
            transport.LateUpdate();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }
    }
}
