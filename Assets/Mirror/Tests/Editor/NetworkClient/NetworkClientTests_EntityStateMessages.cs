using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_EntityStateMessages : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // OnEntityStateMessageUnreliableBaseline is only registered in
            // non-host mode, so tests use ConnectClientBlocking.
            NetworkServer.Listen(1);
        }

        // Connect a remote (non-host) client and authenticate so the WrapHandler
        // auth check passes before reaching the actual message handler body.
        void ConnectAndAuthenticate()
        {
            ConnectClientBlocking(out _);
            NetworkClient.connection.isAuthenticated = true;
        }

        // Serialize an EntityStateMessageUnreliableBaseline body and dispatch it
        // through NetworkClient's registered handler with the given channelId.
        //
        // The payload must contain at least a compressed dirty-bits mask (VarUInt).
        // DeserializeClient reads it unconditionally, even for zero-component
        // identities. We write mask = 0 so no component data follows.
        void InvokeUnreliableBaselineHandler(uint netId, byte baselineTick, int channelId)
        {
            ushort msgType = NetworkMessageId<EntityStateMessageUnreliableBaseline>.Id;
            using NetworkWriterPooled writer = NetworkWriterPool.Get();

            // Build a minimal valid payload: dirty mask = 0, no component data.
            // Use a nested block so the payloadWriter's bytes are copied into
            // 'writer' before payloadWriter is returned to the pool.
            using (NetworkWriterPooled payloadWriter = NetworkWriterPool.Get())
            {
                Compression.CompressVarUInt(payloadWriter, 0UL);
                writer.Write(new EntityStateMessageUnreliableBaseline
                {
                    netId        = netId,
                    baselineTick = baselineTick,
                    payload      = payloadWriter.ToArraySegment()
                });
            }

            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            NetworkClient.handlers[msgType].Invoke(NetworkClient.connection, reader, channelId);
        }

        // Branch: channelId != Channels.Reliable — logs error and returns early.
        [Test]
        public void OnEntityStateMessageUnreliableBaseline_WrongChannel_LogsError()
        {
            ConnectAndAuthenticate();

            LogAssert.Expect(LogType.Error,
                $"Client OnEntityStateMessageUnreliableBaseline arrived on channel {Channels.Unreliable} instead of Reliable. This should never happen!");

            InvokeUnreliableBaselineHandler(1u, 10, Channels.Unreliable);
        }

        // Branch: correct channel, netId not in spawned — no crash, no warning logged.
        [Test]
        public void OnEntityStateMessageUnreliableBaseline_UnknownNetId_DoesNothing()
        {
            ConnectAndAuthenticate();

            const uint unknownNetId = 9999u;
            Assert.That(NetworkClient.spawned.ContainsKey(unknownNetId), Is.False);

            Assert.DoesNotThrow(() => InvokeUnreliableBaselineHandler(unknownNetId, 1, Channels.Reliable));
        }

        // Branch: correct channel, identity found in spawned — sets lastUnreliableBaselineReceived.
        [Test]
        public void OnEntityStateMessageUnreliableBaseline_KnownNetId_SetsBaselineTick()
        {
            ConnectAndAuthenticate();

            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 42u;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            const byte expectedTick = 7;
            InvokeUnreliableBaselineHandler(netId, expectedTick, Channels.Reliable);

            Assert.That(identity.lastUnreliableBaselineReceived, Is.EqualTo(expectedTick));
        }
    }
}