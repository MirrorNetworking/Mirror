using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests.NetworkConnections
{
    public class NetworkConnectionToClientTests : MirrorEditModeTest
    {
        List<byte[]> clientReceived = new List<byte[]>();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
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
        public override void TearDown()
        {
            clientReceived.Clear();
            base.TearDown();
        }

        [Test]
        public void Send_BatchesUntilUpdate()
        {
            // create connection and send
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            NetworkTime.PingInterval = float.MaxValue; // disable ping for this test
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
            // batching adds 8 byte timestamp header
            const int BatchHeader = 8;

            // create connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            NetworkTime.PingInterval = float.MaxValue; // disable ping for this test

            // send and update big message
            byte[] message = {0x01, 0x02};
            int sizeHeader = Compression.VarUIntSize((ulong)message.Length);
            connection.Send(new ArraySegment<byte>(message));
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
            Assert.That(clientReceived[0].Length, Is.EqualTo(BatchHeader + sizeHeader + message.Length));
            Assert.That(clientReceived[0][BatchHeader + sizeHeader + 0], Is.EqualTo(0x01));
            Assert.That(clientReceived[0][BatchHeader + sizeHeader + 1], Is.EqualTo(0x02));

            // clear previous
            clientReceived.Clear();

            // send a smaller message
            message = new byte[]{0xFF};
            sizeHeader = Compression.VarUIntSize((ulong)message.Length);
            connection.Send(new ArraySegment<byte>(message));
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
            Assert.That(clientReceived[0].Length, Is.EqualTo(BatchHeader + sizeHeader + message.Length));
            Assert.That(clientReceived[0][BatchHeader + sizeHeader + 0], Is.EqualTo(0xFF));
        }
    }
}
