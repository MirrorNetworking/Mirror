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
        public void OnSerializeAndDeserializeAllSafely_SERVER_TO_CLIENT()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeTest1NetworkBehaviour comp1,
                out SerializeTest2NetworkBehaviour comp2);

            // set to SERVER with some unique values
            identity.isServer = true;
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

            // set to CLIENT and reset component values
            identity.isServer = false;
            identity.isClient = true;
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

        // serialize -> deserialize. multiple components to be sure.
        // one for Owner, one for Observer
        // one SERVER_TO_CLIENT, one CLIENT_TO_SERVER
        [Test]
        public void OnSerializeAndDeserializeAllSafely_CLIENT_TO_SERVER()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeTest1NetworkBehaviour comp1,
                out SerializeTest2NetworkBehaviour comp2);

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            identity.isClient = true;
            identity.connectionToServer = new FakeNetworkConnection();
            comp1.syncDirection = SyncDirection.SERVER_TO_CLIENT;
            comp1.value = 12345;
            comp2.syncDirection = SyncDirection.CLIENT_TO_SERVER;
            comp2.value = "67890";

            // serialize all
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // should have written something into 'owner' writer
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.EqualTo(0));
            Debug.Log("ownerWriter: " + BitConverter.ToString(ownerWriter.ToArray()));

            // set to SERVER and reset component values
            identity.isServer = true;
            identity.isClient = false;
            comp1.value = 0;
            comp2.value = null;

            // deserialize all. should only get the CLIENT_TO_SERVER value.
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            identity.OnDeserializeAllSafely(reader, true);
            Assert.That(comp1.value, Is.EqualTo(0));
            Assert.That(comp2.value, Is.EqualTo("67890"));

            // reset component values
            comp1.value = 0;
            comp2.value = null;
        }

        // serialization should work even if a component throws an exception.
        // so if first component throws, second should still be serialized fine.
        [Test]
        public void SerializationException()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeExceptionNetworkBehaviour compExc,
                out SerializeTest2NetworkBehaviour comp2);

            // set to SERVER with some unique values
            identity.isServer = true;
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

            // set to CLIENT and reset component values
            identity.isServer = false;
            identity.isClient = true;
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

            // set to SERVER
            identity.isServer = true;

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

            // set to SERVER with some unique values
            identity.isServer = true;
            comp.value = "67890";

            // serialize
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // set to CLIENT and reset component values
            identity.isServer = false;
            identity.isClient = true;
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
