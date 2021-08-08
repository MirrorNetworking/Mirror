// OnDe/SerializeSafely tests.
using System;
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
        }

        // serialize -> deserialize. multiple components to be sure.
        // one for Owner, one for Observer
        [Test]
        public void OnSerializeAndDeserializeAllSafely()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeTest1NetworkBehaviour comp1,
                out SerializeTest2NetworkBehaviour comp2);

            // set some unique values to serialize
            comp1.value = 12345;
            comp1.syncMode = SyncMode.Observers;
            comp2.value = "67890";
            comp2.syncMode = SyncMode.Owner;

            // serialize all
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // owner & observers should have written something
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));
            Debug.Log("ownerWriter: " + BitConverter.ToString(ownerWriter.ToArray()));
            Debug.Log("observersWriter: " + BitConverter.ToString(observersWriter.ToArray()));

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for owner
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            identity.OnDeserializeAllSafely(reader, true);
            Assert.That(comp1.value, Is.EqualTo(12345));
            Assert.That(comp2.value, Is.EqualTo("67890"));

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for observers
            reader = new NetworkReader(observersWriter.ToArray());
            identity.OnDeserializeAllSafely(reader, true);
            // observers mode, should be in data
            Assert.That(comp1.value, Is.EqualTo(12345));
            // owner mode, should not be in data
            Assert.That(comp2.value, Is.EqualTo(null));
        }

        // serialization should work even if a component throws an exception.
        // so if first component throws, second should still be serialized fine.
        [Test]
        public void SerializationException()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeExceptionNetworkBehaviour compExc,
                out SerializeTest2NetworkBehaviour comp2);

            // set some unique values to serialize
            compExc.syncMode = SyncMode.Observers;
            comp2.value = "67890";
            comp2.syncMode = SyncMode.Owner;

            // serialize all - should work even if compExc throws an exception
            // error log because of the exception is expected
            LogAssert.ignoreFailingMessages = true;
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);
            LogAssert.ignoreFailingMessages = false;

            // owner & observers should have written something
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));

            // reset component values
            comp2.value = null;

            // deserialize all for owner - should work even if compExc throws an exception
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            // error log because of the exception is expected
            LogAssert.ignoreFailingMessages = true;
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp2.value, Is.EqualTo("67890"));

            // reset component values
            comp2.value = null;

            // deserialize all for observers - should work even if compExc throws an exception
            reader = new NetworkReader(observersWriter.ToArray());
            // error log because of the exception is expected
            LogAssert.ignoreFailingMessages = true;
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;
            // owner mode, should not be in data
            Assert.That(comp2.value, Is.EqualTo(null));
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void TooManyComponents()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity);

            // add 65 components
            for (int i = 0; i < 65; ++i)
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();

            // CreateNetworked already initializes the components.
            // let's reset and initialize again with the added ones.
            identity.Reset();
            identity.Awake();

            // ignore error from creating cache (has its own test)
            LogAssert.ignoreFailingMessages = true;
            _ = identity.NetworkBehaviours;
            LogAssert.ignoreFailingMessages = false;

            // try to serialize
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeMismatchNetworkBehaviour compMiss,
                out SerializeTest2NetworkBehaviour comp);

            // set some unique values to serialize
            comp.value = "67890";

            // serialize
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // reset component values
            comp.value = null;

            // deserialize all
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            // warning log because of serialization mismatch
            LogAssert.ignoreFailingMessages = true;
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;

            // the mismatch component will fail, but the one before and after
            // should still work fine. that's the whole point.
            Assert.That(comp.value, Is.EqualTo("67890"));
        }
    }
}
