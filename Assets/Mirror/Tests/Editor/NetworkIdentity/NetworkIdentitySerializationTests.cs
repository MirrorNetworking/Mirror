// OnDe/SerializeSafely tests.
using System.Text.RegularExpressions;
using Mirror.Tests.EditorBehaviours.NetworkIdentities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkIdentities
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
        public void SerializeAndDeserializeAll()
        {
            // need two of both versions so we can serialize -> deserialize
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest2NetworkBehaviour serverOwnerComp, out SerializeTest1NetworkBehaviour serverObserversComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest2NetworkBehaviour clientOwnerComp, out SerializeTest1NetworkBehaviour clientObserversComp
            );

            // set sync modes
            serverOwnerComp.syncMode     = clientOwnerComp.syncMode     = SyncMode.Owner;
            serverObserversComp.syncMode = clientObserversComp.syncMode = SyncMode.Observers;

            // set unique values on server components
            serverOwnerComp.value = "42";
            serverObserversComp.value = 42;

            // serialize server object
            serverIdentity.SerializeServer(true, ownerWriter, observersWriter);

            // deserialize client object with OWNER payload
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientOwnerComp.value, Is.EqualTo("42"));
            Assert.That(clientObserversComp.value, Is.EqualTo(42));

            // reset component values
            clientOwnerComp.value = null;
            clientObserversComp.value = 0;

            // deserialize client object with OBSERVERS payload
            reader = new NetworkReader(observersWriter.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientOwnerComp.value, Is.EqualTo(null)); // owner mode shouldn't be in data
            Assert.That(clientObserversComp.value, Is.EqualTo(42)); // observers mode should be in data
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
            serverIdentity.SerializeServer(true, ownerWriter, observersWriter);

            // deserialize client object with OWNER payload
            // should work even if compExc throws an exception
            // error log because of the exception is expected
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientComp2.value, Is.EqualTo("42"));

            // reset component values
            clientComp2.value = null;

            // deserialize client object with OBSERVER payload
            // should work even if compExc throws an exception
            // error log because of the exception is expected
            reader = new NetworkReader(observersWriter.ToArray());
            clientIdentity.DeserializeClient(reader, true);
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
                // clientGO.AddComponent<SerializeTest1NetworkBehaviour>();
            }

            // CreateNetworked already initializes the components.
            // let's reset and initialize again with the added ones.
            // this should show the 'too many components' error
            LogAssert.Expect(LogType.Error, new Regex(".*too many NetworkBehaviour.*"));
            serverIdentity.ResetState();
            // clientIdentity.Reset();
            serverIdentity.Awake();
            // clientIdentity.Awake();
        }

        [Test]
        public void ErrorCorrection()
        {
            int original = 0x12345678;
            byte safety  =       0x78; // last byte

            // correct size shouldn't be corrected
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 0, safety),  Is.EqualTo(original));

            // read a little too much
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 1, safety),   Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 2, safety),   Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 42, safety),  Is.EqualTo(original));

            // read a little too less
            Assert.That(NetworkBehaviour.ErrorCorrection(original - 1, safety),   Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original - 2, safety),   Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original - 42, safety),  Is.EqualTo(original));

            // reading way too much / less is expected to fail.
            // we can only correct the last byte, not more.
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 250, safety), !Is.EqualTo(original));
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
            serverIdentity.SerializeServer(true, ownerWriter, observersWriter);

            // deserialize on client
            // ignore warning log because of serialization mismatch
            LogAssert.ignoreFailingMessages = true;
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            LogAssert.ignoreFailingMessages = false;

            // the mismatch component will fail, but the one before and after
            // should still work fine. that's the whole point.
            Assert.That(clientComp.value, Is.EqualTo("42"));
        }

        // ensure Serialize writes nothing if not dirty.
        // previously after the dirty mask improvement, it would write a 1 byte
        // 0-dirty-mask. instead, we need to ensure it writes nothing.
        // too easy to miss, with too significant bandwidth implications.
        [Test]
        public void SerializeServer_NotInitial_NotDirty_WritesNothing()
        {
            // create spawned so that isServer/isClient is set properly
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp1, out SerializeTest2NetworkBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp1, out SerializeTest2NetworkBehaviour clientComp2);

            // change nothing
            // serverComp.value = "42";

            // serialize server object.
            // 'initial' would write everything.
            // instead, try 'not initial' with 0 dirty bits
            serverIdentity.SerializeServer(false, ownerWriter, observersWriter);
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
            Assert.That(observersWriter.Position, Is.EqualTo(0));
        }

        [Test]
        public void SerializeClient_NotInitial_NotDirty_WritesNothing()
        {
            // create spawned so that isServer/isClient is set properly
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp1, out SerializeTest2NetworkBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp1, out SerializeTest2NetworkBehaviour clientComp2);

            // client only serializes owned ClientToServer components
            clientIdentity.isOwned = true;
            serverComp1.syncDirection = SyncDirection.ClientToServer;
            serverComp2.syncDirection = SyncDirection.ClientToServer;
            clientComp1.syncDirection = SyncDirection.ClientToServer;
            clientComp2.syncDirection = SyncDirection.ClientToServer;

            // change nothing
            // clientComp.value = "42";

            // serialize client object
            clientIdentity.SerializeClient(ownerWriter);
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
        }

        // serialize -> deserialize. multiple components to be sure.
        // one for Owner, one for Observer
        // one ServerToClient, one ClientToServer
        [Test]
        public void SerializeAndDeserialize_ClientToServer_NOT_OWNED()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SerializeTest1NetworkBehaviour comp1,
                out SerializeTest2NetworkBehaviour comp2);

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            identity.isOwned = false;
            identity.connectionToServer = null; // NOT OWNED
            comp1.syncDirection = SyncDirection.ServerToClient;
            comp1.value = 12345;
            comp2.syncDirection = SyncDirection.ClientToServer;
            comp2.value = "67890";

            // serialize all
            identity.SerializeClient(ownerWriter);

            // shouldn't sync anything. because even though it's ClientToServer,
            // we don't own this one so we shouldn't serialize & sync it.
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
        }

        // server should still send initial even if Owner + ClientToServer
        [Test]
        public void SerializeServer_OwnerMode_ClientToServer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp);

            // pretend to be owned
            identity.isOwned = true;
            comp.syncMode = SyncMode.Owner;
            comp.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp.syncDirection = SyncDirection.ClientToServer;
            comp.SetValue(11); // modify with helper function to avoid #3525

            // initial: should still write for owner
            identity.SerializeServer(true, ownerWriter, observersWriter);
            Debug.Log("initial ownerWriter: " + ownerWriter);
            Debug.Log("initial observerWriter: " + observersWriter);
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.EqualTo(0));

            // delta: ClientToServer comes from the client
            comp.SetValue(22); // modify with helper function to avoid #3525
            ownerWriter.Position = 0;
            observersWriter.Position = 0;
            identity.SerializeServer(false, ownerWriter, observersWriter);
            Debug.Log("delta ownerWriter: " + ownerWriter);
            Debug.Log("delta observersWriter: " + observersWriter);
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
            Assert.That(observersWriter.Position, Is.EqualTo(0));
        }

        // TODO this started failing after we moved SyncVarTest1NetworkBehaviour
        // into it's own asmdef.
        // server should still broadcast ClientToServer components to everyone
        // except the owner.
        [Test]
        public void SerializeServer_ObserversMode_ClientToServer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp);

            // pretend to be owned
            identity.isOwned = true;
            comp.syncMode = SyncMode.Observers;
            comp.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp.syncDirection = SyncDirection.ClientToServer;
            comp.SetValue(11); // modify with helper function to avoid #3525

            // initial: should write something for owner and observers
            identity.SerializeServer(true, ownerWriter, observersWriter);
            Debug.Log("initial ownerWriter: " + ownerWriter);
            Debug.Log("initial observerWriter: " + observersWriter);
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));

            // delta: should only write for observers
            comp.SetValue(22); // modify with helper function to avoid #3525
            ownerWriter.Position = 0;
            observersWriter.Position = 0;
            identity.SerializeServer(false, ownerWriter, observersWriter);
            Debug.Log("delta ownerWriter: " + ownerWriter);
            Debug.Log("delta observersWriter: " + observersWriter);
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));
        }
    }
}
