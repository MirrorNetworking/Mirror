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

        void UpdateTransport()
        {
            transport.ServerEarlyUpdate();
            transport.ClientEarlyUpdate();
        }

        [Test]
        public void Send_WithoutBatching_SendsImmediately()
        {
            // create connection and send
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, false, 0);
            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Send() should send immediately, not only in server.update flushing
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }

        [Test]
        public void Send_BatchesUntilUpdate()
        {
            // create connection and send
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, true, 0);
            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Send() should only add to batch, not send anything yet
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(0));

            // updating the connection should now send
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }

        [Test]
        public void Send_BatchesUntilInterval()
        {
            // create connection and send
            int intervalMilliseconds = 10;
            float intervalSeconds = intervalMilliseconds / 1000f;
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, true, intervalSeconds);
            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Send() and update shouldn't send yet until interval elapsed
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(0));

            // wait 'interval'
            Thread.Sleep(intervalMilliseconds);

            // updating again should flush out the batch
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }

        // IMPORTANT
        //
        // there was a bug where batching resets .Position instead of .Length,
        // resulting in extremely high bandwidth where if the last message's
        // Length was 2, and the current message's Length was 1, then we would
        // still send a writer with Length = 2 because we did not reset .Length!
        // -> let's try to send a big message, update, then send a small message
        [Test]
        public void SendBatchingResetsPreviousWriter()
        {
            // create connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, true, 0);

            // send and update big message
            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
            Assert.That(clientReceived[0].Length, Is.EqualTo(2));
            Assert.That(clientReceived[0][0], Is.EqualTo(0x01));
            Assert.That(clientReceived[0][1], Is.EqualTo(0x02));

            // clear previous
            clientReceived.Clear();

            // send a smaller message
            message = new byte[]{0xFF};
            connection.Send(new ArraySegment<byte>(message));
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
            Assert.That(clientReceived[0].Length, Is.EqualTo(1));
            Assert.That(clientReceived[0][0], Is.EqualTo(0xFF));
        }
    }
}
