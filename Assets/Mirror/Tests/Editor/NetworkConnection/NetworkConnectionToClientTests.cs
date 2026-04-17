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
        public void InternalConstructor_CreatesInstance()
        {
            // internal NetworkConnectionToClient() : base() { } is used by Mirror
            // internally.  connectionId and address are left at their type defaults
            // because only the public constructor sets them.
            NetworkConnectionToClient connection = new NetworkConnectionToClient();
            Assert.That(connection.connectionId, Is.EqualTo(0));
            Assert.That(connection.address, Is.Null);
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

        // ---- rtt --------------------------------------------------------------------

        [Test]
        public void Rtt_DefaultsToZero()
        {
            // rtt reads _rtt.Value; no pong messages received yet, so it must be 0
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.rtt, Is.EqualTo(0.0));
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

        // ---- UpdatePing -------------------------------------------------------------

        [Test]
        public void UpdatePing_SendsPingWhenIntervalElapsed()
        {
            // PingInterval = -1f ensures localTime >= lastPingTime + (-1) is always true
            float savedPingInterval = NetworkTime.PingInterval;
            try
            {
                NetworkTime.PingInterval = -1f;
                NetworkConnectionToClient connection = new NetworkConnectionToClient(1);

                // Update() calls UpdatePing (fires ping) then flushes the unreliable batcher
                connection.Update();
                UpdateTransport();

                // one batch containing the NetworkPingMessage should have arrived
                Assert.That(clientReceived.Count, Is.EqualTo(1));
            }
            finally
            {
                NetworkTime.PingInterval = savedPingInterval;
            }
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

        // ---- DestroyOwnedObjects ----------------------------------------------------

        [Test]
        public void DestroyOwnedObjects_DestroysSpawnedGameObjects()
        {
            NetworkServer.Listen(1);

            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            CreateNetworked(out _, out NetworkIdentity identity);

            // In edit-mode tests Application.isPlaying is false, so OnValidate
            // fires on AddComponent and AssignSceneID() assigns a random non-zero
            // sceneId.  Force it to 0 so Mirror treats this as a dynamically
            // instantiated object — exercising the NetworkServer.Destroy path
            // (NetworkConnectionToClient line 222) instead of the scene-object
            // KeepActive path.
            identity.sceneId = 0;

            // Spawn registers the identity in NetworkServer.spawned.
            NetworkServer.Spawn(identity.gameObject);
            connection.AddOwnedObject(identity);

            // DestroyOwnedObjects iterates owned; for each identity with
            // sceneId == 0 it calls NetworkServer.Destroy (line 222), which
            // synchronously calls UnSpawnInternal -> spawned.Remove, then
            // DestroyImmediate (edit mode).  owned is cleared afterwards.
            connection.DestroyOwnedObjects();

            Assert.That(connection.owned, Is.Empty);
            Assert.That(NetworkServer.spawned, Is.Empty);
        }

        // ---- OnTimeSnapshot ---------------------------------------------------------

        [Test]
        public void OnTimeSnapshot_UpdatesRemoteTimescale()
        {
            // dynamicAdjustment = true by default, so this exercises that branch too.
            // InsertAndAdjust sets remoteTimescale via Timescale(); it will be != 0.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            Assert.That(connection.remoteTimescale, Is.EqualTo(0.0)); // default before any snapshot

            connection.OnTimeSnapshot(new TimeSnapshot(NetworkTime.localTime, NetworkTime.localTime));

            Assert.That(connection.remoteTimescale, Is.Not.EqualTo(0.0));
        }

        [Test]
        public void OnTimeSnapshot_WithDynamicAdjustmentDisabled_UpdatesRemoteTimescale()
        {
            // Explicitly disable dynamic adjustment to cover the branch that skips
            // bufferTimeMultiplier recalculation and calls InsertAndAdjust directly.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            bool savedDynamicAdjustment = NetworkClient.snapshotSettings.dynamicAdjustment;
            try
            {
                NetworkClient.snapshotSettings.dynamicAdjustment = false;
                connection.OnTimeSnapshot(new TimeSnapshot(NetworkTime.localTime, NetworkTime.localTime));
                Assert.That(connection.remoteTimescale, Is.Not.EqualTo(0.0));
            }
            finally
            {
                NetworkClient.snapshotSettings.dynamicAdjustment = savedDynamicAdjustment;
            }
        }

        [Test]
        public void OnTimeSnapshot_IgnoresSnapshotsWhenBufferFull()
        {
            // snapshotBufferSizeLimit = 0 means snapshots.Count (0) >= limit (0) → early return.
            // remoteTimescale is never touched by InsertAndAdjust, so it stays at default 0.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            connection.snapshotBufferSizeLimit = 0;

            connection.OnTimeSnapshot(new TimeSnapshot(NetworkTime.localTime, NetworkTime.localTime));

            Assert.That(connection.remoteTimescale, Is.EqualTo(0.0));
        }

        // ---- UpdateTimeInterpolation ------------------------------------------------

        [Test]
        public void UpdateTimeInterpolation_DoesNothingWithNoSnapshots()
        {
            // With no snapshots, the if (snapshots.Count > 0) guard short-circuits.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            double timelineBefore = connection.remoteTimeline;

            connection.UpdateTimeInterpolation();

            Assert.That(connection.remoteTimeline, Is.EqualTo(timelineBefore));
        }

        [Test]
        public void UpdateTimeInterpolation_StepsWhenSnapshotsExist()
        {
            // Add a snapshot so snapshots.Count > 0, which exercises StepTime +
            // StepInterpolation. In edit mode unscaledDeltaTime is typically 0,
            // so remoteTimeline stays constant, but the branch is fully covered.
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            NetworkTime.PingInterval = float.MaxValue;

            connection.OnTimeSnapshot(new TimeSnapshot(NetworkTime.localTime, NetworkTime.localTime));
            double timescaleAfterSnapshot = connection.remoteTimescale;
            Assert.That(timescaleAfterSnapshot, Is.Not.EqualTo(0.0)); // confirm snapshot was accepted

            // UpdateTimeInterpolation must not throw and must not alter remoteTimescale
            // (timescale is only ever written by OnTimeSnapshot / InsertAndAdjust)
            connection.UpdateTimeInterpolation();
            Assert.That(connection.remoteTimescale, Is.EqualTo(timescaleAfterSnapshot));
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
