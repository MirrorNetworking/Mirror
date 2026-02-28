using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    // Test coverage for NetworkClient.OnEntityStateMessageUnreliableDelta.
    //
    // Branches:
    //   1. channelId != Channels.Unreliable          → LogError + return
    //   2. netId not in spawned                       → silent no-op
    //   3. remoteTimeStamp < lastUnreliableStateTime  → Debug.Log (out-of-order) + return
    //   4. remoteTimeStamp == lastUnreliableStateTime,
    //      unreliableRedundancy = false               → Debug.Log (duplicate) + return
    //   5. remoteTimeStamp == lastUnreliableStateTime,
    //      unreliableRedundancy = true                → silent return
    //   6. baselineTick != lastUnreliableBaselineReceived → Debug.Log (old baseline) + return
    //   7. all checks pass                            → state time updated, DeserializeClient called
    public class NetworkClientTests_EntityStateMessagesDelta : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // OnEntityStateMessageUnreliableDelta is registered only in
            // non-host mode, so connect a remote client.
            NetworkServer.Listen(1);
        }

        [TearDown]
        public override void TearDown()
        {
            // Reset the public static field that may have been toggled in tests.
            NetworkServer.unreliableRedundancy = false;
            base.TearDown();
        }

        void ConnectAndAuthenticate()
        {
            ConnectClientBlocking(out _);
            NetworkClient.connection.isAuthenticated = true;
        }

        // Serialize an EntityStateMessageUnreliableDelta and dispatch it through
        // NetworkClient's registered handler with the given channelId.
        //
        // Payload contains a compressed dirty-mask = 0 so DeserializeClient
        // succeeds on a zero-component identity without EndOfStreamException.
        void InvokeUnreliableDeltaHandler(uint netId, byte baselineTick, int channelId)
        {
            ushort msgType = NetworkMessageId<EntityStateMessageUnreliableDelta>.Id;
            using NetworkWriterPooled writer = NetworkWriterPool.Get();

            // Nested block ensures payloadWriter's bytes are captured into the
            // outer ArraySegment before payloadWriter is returned to the pool.
            using (NetworkWriterPooled payloadWriter = NetworkWriterPool.Get())
            {
                Compression.CompressVarUInt(payloadWriter, 0UL);
                writer.Write(new EntityStateMessageUnreliableDelta
                {
                    netId        = netId,
                    baselineTick = baselineTick,
                    payload      = payloadWriter.ToArraySegment()
                });
            }

            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            NetworkClient.handlers[msgType].Invoke(NetworkClient.connection, reader, channelId);
        }

        // Branch 1: wrong channel → LogError and return before touching spawned.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_WrongChannel_LogsError()
        {
            ConnectAndAuthenticate();

            LogAssert.Expect(LogType.Error,
                $"Client OnEntityStateMessageUnreliableDelta arrived on channel {Channels.Reliable} instead of Unreliable. This should never happen!");

            InvokeUnreliableDeltaHandler(1u, 1, Channels.Reliable);
        }

        // Branch 2: correct channel, netId not present in spawned → silent no-op.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_UnknownNetId_DoesNothing()
        {
            ConnectAndAuthenticate();

            const uint unknownNetId = 9999u;
            Assert.That(NetworkClient.spawned.ContainsKey(unknownNetId), Is.False);

            Assert.DoesNotThrow(() => InvokeUnreliableDeltaHandler(unknownNetId, 1, Channels.Unreliable));
        }

        // Branch 3: remoteTimeStamp older than lastUnreliableStateTime → out-of-order,
        // logs a Debug.Log and returns without updating state time.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_OutOfOrder_LogsAndIgnores()
        {
            ConnectAndAuthenticate();

            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 42u;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            identity.lastUnreliableStateTime     = 20.0;
            NetworkClient.connection.remoteTimeStamp = 10.0; // older → out of order

            LogAssert.Expect(LogType.Log,
                new Regex("Client caught out of order Unreliable state message"));

            InvokeUnreliableDeltaHandler(netId, 0, Channels.Unreliable);

            Assert.That(identity.lastUnreliableStateTime, Is.EqualTo(20.0));
        }

        // Branch 4: timestamps equal AND unreliableRedundancy = false → duplicate,
        // logs a Debug.Log and returns without updating state time.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_Duplicate_RedundancyDisabled_LogsAndIgnores()
        {
            ConnectAndAuthenticate();

            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 42u;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            NetworkServer.unreliableRedundancy        = false;
            identity.lastUnreliableStateTime          = 10.0;
            NetworkClient.connection.remoteTimeStamp  = 10.0; // equal → duplicate

            LogAssert.Expect(LogType.Log,
                new Regex("Client caught duplicate Unreliable state message"));

            InvokeUnreliableDeltaHandler(netId, 0, Channels.Unreliable);

            Assert.That(identity.lastUnreliableStateTime, Is.EqualTo(10.0));
        }

        // Branch 5: timestamps equal AND unreliableRedundancy = true → silent return,
        // no log emitted and state time is left unchanged.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_Duplicate_RedundancyEnabled_SilentIgnores()
        {
            ConnectAndAuthenticate();

            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 42u;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            NetworkServer.unreliableRedundancy        = true;
            identity.lastUnreliableStateTime          = 10.0;
            NetworkClient.connection.remoteTimeStamp  = 10.0; // equal → duplicate

            Assert.DoesNotThrow(() => InvokeUnreliableDeltaHandler(netId, 0, Channels.Unreliable));

            Assert.That(identity.lastUnreliableStateTime, Is.EqualTo(10.0));
        }

        // Branch 6: timestamp is newer but baselineTick doesn't match
        // lastUnreliableBaselineReceived → stale delta, logs Debug.Log and returns.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_WrongBaselineTick_LogsAndIgnores()
        {
            ConnectAndAuthenticate();

            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 42u;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            identity.lastUnreliableStateTime          = 5.0;
            NetworkClient.connection.remoteTimeStamp  = 20.0; // newer, passes ordering check
            identity.lastUnreliableBaselineReceived   = 3;    // identity expects baseline tick 3

            LogAssert.Expect(LogType.Log,
                new Regex("Client caught Unreliable state message for old baseline"));

            InvokeUnreliableDeltaHandler(netId, 7, Channels.Unreliable); // mismatched tick

            Assert.That(identity.lastUnreliableStateTime, Is.EqualTo(5.0));
        }

        // Branch 7: all checks pass → lastUnreliableStateTime is updated to
        // connection.remoteTimeStamp and DeserializeClient is called without error.
        [Test]
        public void OnEntityStateMessageUnreliableDelta_ValidDelta_UpdatesStateTime()
        {
            ConnectAndAuthenticate();

            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 42u;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            identity.lastUnreliableStateTime         = 5.0;
            NetworkClient.connection.remoteTimeStamp = 20.0; // newer
            identity.lastUnreliableBaselineReceived  = 3;    // baseline tick to match

            InvokeUnreliableDeltaHandler(netId, 3, Channels.Unreliable); // matching tick

            Assert.That(identity.lastUnreliableStateTime, Is.EqualTo(20.0));
        }
    }
}