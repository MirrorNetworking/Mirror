using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkConnections
{
    public class NetworkConnectionToClientTests : MirrorEditModeTest
    {
        List<byte[]> clientReceived = new List<byte[]>();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            transport.OnClientDataReceived = (message, channelId) =>
            {
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

        // ---- constructor & properties -----------------------------------------------

        [Test]
        public void Constructor_SetsConnectionId()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(99);
            Assert.That(connection.connectionId, Is.EqualTo(99));
        }

        [Test]
        public void Constructor_DefaultAddress_IsLocalhost()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.address, Is.EqualTo("localhost"));
        }

        [Test]
        public void Constructor_CustomAddress()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1, "10.0.0.1");
            Assert.That(connection.address, Is.EqualTo("10.0.0.1"));
        }

        [Test]
        public void ToString_ReturnsExpectedFormat()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            Assert.That(connection.ToString(), Is.EqualTo("connection(42)"));
        }

        // ---- defaults ---------------------------------------------------------------

        [Test]
        public void Defaults_IsAuthenticated_IsFalse()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.isAuthenticated, Is.False);
        }

        [Test]
        public void Defaults_IsReady_IsFalse()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.isReady, Is.False);
        }

        [Test]
        public void Defaults_Owned_IsEmpty()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.owned, Is.Empty);
        }

        [Test]
        public void Defaults_Observing_IsEmpty()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.observing, Is.Empty);
        }

        // ---- IsAlive ----------------------------------------------------------------

        [Test]
        public void IsAlive_ReturnsTrueWithinTimeout()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            // lastMessageTime is assigned Time.time in the constructor
            Assert.That(connection.IsAlive(60f), Is.True);
        }

        [Test]
        public void IsAlive_ReturnsFalseWhenTimedOut()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            connection.lastMessageTime = Time.time - 100f; // simulate 100s of inactivity
            Assert.That(connection.IsAlive(10f), Is.False);
        }

        // ---- owned set --------------------------------------------------------------

        [Test]
        public void AddOwnedObject_AddsToOwnedSet()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            CreateNetworked(out _, out NetworkIdentity identity);
            connection.AddOwnedObject(identity);
            Assert.That(connection.owned, Contains.Item(identity));
        }

        [Test]
        public void RemoveOwnedObject_RemovesFromOwnedSet()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            CreateNetworked(out _, out NetworkIdentity identity);
            connection.AddOwnedObject(identity);
            connection.RemoveOwnedObject(identity);
            Assert.That(connection.owned, Is.Empty);
        }

        // ---- Disconnect -------------------------------------------------------------

        [Test]
        public void Disconnect_SetsIsReadyFalse()
        {
            // NOTE: calls Transport.active.ServerDisconnect which stops the
            //       MemoryTransport server, but TearDown handles full cleanup.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            connection.isReady = true;
            connection.Disconnect();
            Assert.That(connection.isReady, Is.False);
        }

        // ---- Cleanup ----------------------------------------------------------------

        [Test]
        public void Cleanup_PreventsQueuedMessageFromBeingSent()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            NetworkTime.PingInterval = float.MaxValue; // disable ping for this test

            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Cleanup() clears the batcher before Update() can flush it
            connection.Cleanup();
            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(0));
        }

        // ---- batching / send --------------------------------------------------------

        // NOTE: These tests create NetworkConnectionToClient(42) while the
        //       MemoryTransport's real connection ID is 1.  This works because
        //       MemoryTransport.ServerSend ignores the connection ID and routes
        //       all server sends to the single connected client.
        [Test]
        public void Send_BatchesUntilUpdate()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            NetworkTime.PingInterval = float.MaxValue; // disable ping for this test

            byte[] message = {0x01, 0x02};
            connection.Send(new ArraySegment<byte>(message));

            // Send() should only add to batch, not send anything yet
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(0));

            // updating the connection should now flush and send
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

        [Test]
        public void Send_UnreliableChannel_BatchesUntilUpdate()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            NetworkTime.PingInterval = float.MaxValue; // disable ping for this test

            byte[] message = {0xAA};
            connection.Send(new ArraySegment<byte>(message), Channels.Unreliable);

            // not sent before Update()
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(0));

            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }

        [Test]
        public void Send_MultipleMessages_AreBatchedIntoSingleTransportCall()
        {
            // all three tiny messages share a timestamp and fit within the
            // 1400-byte MemoryTransport threshold, so Batcher packs them into
            // one batch and Update() makes exactly one SendToTransport call.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            NetworkTime.PingInterval = float.MaxValue; // disable ping for this test

            connection.Send(new ArraySegment<byte>(new byte[]{0x01}));
            connection.Send(new ArraySegment<byte>(new byte[]{0x02}));
            connection.Send(new ArraySegment<byte>(new byte[]{0x03}));

            connection.Update();
            UpdateTransport();
            Assert.That(clientReceived.Count, Is.EqualTo(1));
        }
    }
}
