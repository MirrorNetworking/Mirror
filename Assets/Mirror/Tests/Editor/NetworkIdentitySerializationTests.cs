// OnDe/SerializeSafely tests.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkIdentitySerializationTests : MirrorEditModeTest
    {
        // writers are always needed. create in setup for convenience.
        NetworkWriter ownerWriter;
        NetworkWriter observersWriter;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            ownerWriter = new NetworkWriter();
            observersWriter = new NetworkWriter();

            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        // serialize -> deserialize. multiple components to be sure.
        // one for Owner, one for Observer
        [Test]
        public void OnSerializeAndDeserializeAllSafely()
        {
            // need two of both versions so we can serialize -> deserialize
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp1, out SerializeTest2NetworkBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp1, out SerializeTest2NetworkBehaviour clientComp2
            );

            // set sync modes
            serverComp1.syncMode = clientComp1.syncMode = SyncMode.Observers;
            serverComp2.syncMode = clientComp2.syncMode = SyncMode.Owner;

            // set unique values on server components
            serverComp1.value = 42;
            serverComp2.value = "42";

            // serialize server object
            serverIdentity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // deserialize client object with OWNER payload
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.OnDeserializeAllSafely(reader, true);
            Assert.That(clientComp1.value, Is.EqualTo(42));
            Assert.That(clientComp2.value, Is.EqualTo("42"));

            // reset component values
            clientComp1.value = 0;
            clientComp2.value = null;

            // deserialize client object with OBSERVERS payload
            reader = new NetworkReader(observersWriter.ToArray());
            clientIdentity.OnDeserializeAllSafely(reader, true);
            Assert.That(clientComp1.value, Is.EqualTo(42)); // observers mode should be in data
            Assert.That(clientComp2.value, Is.EqualTo(null)); // owner mode shouldn't be in data
        }

        // serialization should work even if a component throws an exception.
        // so if first component throws, second should still be serialized fine.
        [Test]
        public void SerializationException()
        {
            // the exception component will log exception errors all the way
            // through this function, starting from spawning where it's
            // serialized for the first time.
            LogAssert.ignoreFailingMessages = true;

            // need two of both versions so we can serialize -> deserialize
            // spawning the exception component will already show an exception.
            // ignore it.
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeExceptionNetworkBehaviour serverCompExc, out SerializeTest2NetworkBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity, out SerializeExceptionNetworkBehaviour clientCompExc, out SerializeTest2NetworkBehaviour clientComp2);

            // set sync modes
            serverCompExc.syncMode = clientCompExc.syncMode = SyncMode.Observers;
            serverComp2.syncMode = clientComp2.syncMode = SyncMode.Owner;

            // set unique values on server components
            serverComp2.value = "42";

            // serialize server object
            // should work even if compExc throws an exception.
            // error log because of the exception is expected.
            serverIdentity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // deserialize client object with OWNER payload
            // should work even if compExc throws an exception
            // error log because of the exception is expected
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.OnDeserializeAllSafely(reader, true);
            Assert.That(clientComp2.value, Is.EqualTo("42"));

            // reset component values
            clientComp2.value = null;

            // deserialize client object with OBSERVER payload
            // should work even if compExc throws an exception
            // error log because of the exception is expected
            reader = new NetworkReader(observersWriter.ToArray());
            clientIdentity.OnDeserializeAllSafely(reader, true);
            Assert.That(clientComp2.value, Is.EqualTo(null)); // owner mode should be in data

            // restore error checks
            LogAssert.ignoreFailingMessages = false;
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void TooManyComponents()
        {
            // create spawned so that isServer/isClient is set properly
            CreateNetworkedAndSpawn(
                out GameObject serverGO, out NetworkIdentity serverIdentity,
                out GameObject clientGO, out NetworkIdentity clientIdentity);

            // add 65 components
            for (int i = 0; i < 65; ++i)
            {
                serverGO.AddComponent<SerializeTest1NetworkBehaviour>();
                clientGO.AddComponent<SerializeTest1NetworkBehaviour>();
            }

            // CreateNetworked already initializes the components.
            // let's reset and initialize again with the added ones.
            serverIdentity.Reset();
            clientIdentity.Reset();
            serverIdentity.Awake();
            clientIdentity.Awake();

            // ignore error from creating cache (has its own test)
            LogAssert.ignoreFailingMessages = true;
            _ = serverIdentity.NetworkBehaviours;
            _ = clientIdentity.NetworkBehaviours;
            LogAssert.ignoreFailingMessages = false;

            // try to serialize
            serverIdentity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // Should still write with too many Components because NetworkBehavioursCache should handle the error
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));
        }

        // OnDeserializeSafely should be able to detect and handle serialization
        // mismatches (= if compA writes 10 bytes but only reads 8 or 12, it
        // shouldn't break compB's serialization. otherwise we end up with
        // insane runtime errors like monsters that look like npcs. that's what
        // happened back in the day with UNET).
        [Test]
        public void SerializationMismatch()
        {
            // create spawned so that isServer/isClient is set properly
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeMismatchNetworkBehaviour serverCompMiss, out SerializeTest2NetworkBehaviour serverComp,
                out _, out NetworkIdentity clientIdentity, out SerializeMismatchNetworkBehaviour clientCompMiss, out SerializeTest2NetworkBehaviour clientComp);

            // set some unique values on server component to serialize
            serverComp.value = "42";

            // serialize server object
            serverIdentity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // deserialize on client
            // ignore warning log because of serialization mismatch
            LogAssert.ignoreFailingMessages = true;
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;

            // the mismatch component will fail, but the one before and after
            // should still work fine. that's the whole point.
            Assert.That(clientComp.value, Is.EqualTo("42"));
        }
    }
}
