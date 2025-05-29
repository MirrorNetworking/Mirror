// OnDe/SerializeSafely tests.
using System.Collections.Generic;
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
        NetworkWriter ownerWriterReliable;
        NetworkWriter observersWriterReliable;
        NetworkWriter ownerWriterUnreliableBaseline;
        NetworkWriter observersWriterUnreliableBaseline;
        NetworkWriter ownerWriterUnreliableDelta;
        NetworkWriter observersWriterUnreliableDelta;
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            ownerWriterReliable = new NetworkWriter();
            observersWriterReliable = new NetworkWriter();

            ownerWriterUnreliableBaseline = new NetworkWriter();
            observersWriterUnreliableBaseline = new NetworkWriter();

            ownerWriterUnreliableDelta = new NetworkWriter();
            observersWriterUnreliableDelta = new NetworkWriter();

            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        void ResetWriters()
        {
            ownerWriterReliable.Reset();
            observersWriterReliable.Reset();
            ownerWriterUnreliableBaseline.Reset();
            observersWriterUnreliableBaseline.Reset();
            ownerWriterUnreliableDelta.Reset();
            observersWriterUnreliableDelta.Reset();
        }

        // serialize -> deserialize. multiple components to be sure.
        // one for Owner, one for Observer
        [Test]
        public void SerializeServer_Spawn_OwnerAndObserver()
        {
            // need two of both versions so we can serialize -> deserialize
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest2NetworkBehaviour serverOwnerComp, out SerializeTest1NetworkBehaviour serverObserversComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest2NetworkBehaviour clientOwnerComp, out SerializeTest1NetworkBehaviour clientObserversComp
            );

            // set sync modes
            serverOwnerComp.syncMode = clientOwnerComp.syncMode = SyncMode.Owner;
            serverObserversComp.syncMode = clientObserversComp.syncMode = SyncMode.Observers;

            // set unique values on server components
            serverOwnerComp.value = "42";
            serverObserversComp.value = 42;

            // serialize server object
            serverIdentity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);

            // deserialize client object with OWNER payload
            NetworkReader reader = new NetworkReader(ownerWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientOwnerComp.value, Is.EqualTo("42"));
            Assert.That(clientObserversComp.value, Is.EqualTo(42));

            // reset component values
            clientOwnerComp.value = null;
            clientObserversComp.value = 0;

            // deserialize client object with OBSERVERS payload
            reader = new NetworkReader(observersWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientOwnerComp.value, Is.EqualTo(null));   // owner mode shouldn't be in data
            Assert.That(clientObserversComp.value, Is.EqualTo(42)); // observers mode should be in data
        }


        // verify owner changes serialize data properly
        [Test]
        public void SerializeServer_AssignOwner()
        {
            // need two of both versions so we can serialize -> deserialize
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest2NetworkBehaviour serverOwnerComp, out SerializeTest1NetworkBehaviour serverObserversComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest2NetworkBehaviour clientOwnerComp, out SerializeTest1NetworkBehaviour clientObserversComp
            );

            // set sync modes
            serverOwnerComp.syncMode = clientOwnerComp.syncMode = SyncMode.Owner;
            serverObserversComp.syncMode = clientObserversComp.syncMode = SyncMode.Observers;

            // set unique values on server components
            serverOwnerComp.value = "42";
            serverObserversComp.value = 42;

            // serialize server object
            serverIdentity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);

            // deserialize client object with OWNER payload
            NetworkReader reader = new NetworkReader(ownerWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientOwnerComp.value, Is.EqualTo("42"));
            Assert.That(clientObserversComp.value, Is.EqualTo(42));

            // reset component values
            clientOwnerComp.value = null;
            clientObserversComp.value = 0;

            // deserialize client object with OBSERVERS payload
            reader = new NetworkReader(observersWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientOwnerComp.value, Is.EqualTo(null));   // owner mode shouldn't be in data
            Assert.That(clientObserversComp.value, Is.EqualTo(42)); // observers mode should be in data

            // incremental update:
            ResetWriters();
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable,
                observersWriterReliable,
                ownerWriterUnreliableBaseline,
                observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta,
                observersWriterUnreliableDelta,
                false);
            // no change!
            Assert.AreEqual(0, ownerWriterReliable.Position);
            Assert.AreEqual(0, observersWriterReliable.Position);
            Assert.AreEqual(0, ownerWriterUnreliableBaseline.Position);
            Assert.AreEqual(0, observersWriterUnreliableBaseline.Position);
            Assert.AreEqual(0, ownerWriterUnreliableDelta.Position);
            Assert.AreEqual(0, observersWriterUnreliableDelta.Position);

            // Now assign authority
            Assert.AreNotEqual(serverIdentity.connectionToClient, connectionToClient);
            serverIdentity.AssignClientAuthority(connectionToClient);

            // this should lead to owner data being marked as dirty so it can be sent
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable,
                observersWriterReliable,
                ownerWriterUnreliableBaseline,
                observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta,
                observersWriterUnreliableDelta,
                false);

            Assert.AreNotEqual(0, ownerWriterReliable.Position);
            Assert.AreEqual(0, observersWriterReliable.Position);
            Assert.AreEqual(0, ownerWriterUnreliableBaseline.Position);
            Assert.AreEqual(0, observersWriterUnreliableBaseline.Position);
            Assert.AreEqual(0, ownerWriterUnreliableDelta.Position);
            Assert.AreEqual(0, observersWriterUnreliableDelta.Position);

            // client component values still contain the data from observer deserialize, so just the observer value and no owner data set
            Assert.AreEqual(null, clientOwnerComp.value);
            Assert.AreEqual(42, clientObserversComp.value);
            // deserialize client object with owner payload
            reader = new NetworkReader(ownerWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            // Rider doesn't realize the DeserializeClient changes component values and gives false warnings
            // Disable that inspection for the next two statements:
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.AreEqual("42", clientOwnerComp.value);
            // ReSharper disable once HeuristicUnreachableCode
            Assert.That(clientObserversComp.value, Is.EqualTo(42));

        }

        // test serialize -> deserialize of any supported number of components
        [Test]
        public void SerializeAndDeserializeN([NUnit.Framework.Range(1, 64)] int numberOfNBs)
        {
            List<SerializeTest1NetworkBehaviour> serverNBs = new List<SerializeTest1NetworkBehaviour>();
            List<SerializeTest1NetworkBehaviour> clientNBs = new List<SerializeTest1NetworkBehaviour>();
            // need two of both versions so we can serialize -> deserialize
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, ni =>
                {
                    for (int i = 0; i < numberOfNBs; i++)
                    {
                        SerializeTest1NetworkBehaviour nb = ni.gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
                        nb.syncInterval = 0;
                        nb.syncMode = SyncMode.Observers;
                        serverNBs.Add(nb);
                    }
                },
                out _, out NetworkIdentity clientIdentity, ni =>
                {
                    for (int i = 0; i < numberOfNBs; i++)
                    {
                        SerializeTest1NetworkBehaviour nb = ni.gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
                        nb.syncInterval = 0;
                        nb.syncMode = SyncMode.Observers;
                        clientNBs.Add(nb);
                    }
                }
            );

            // INITIAL SYNC
            // set unique values on server components
            for (int i = 0; i < serverNBs.Count; i++)
            {
                serverNBs[i].value = (i + 1) * 3;
                serverNBs[i].SetDirty();
            }

            // serialize server object
            serverIdentity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);

            // deserialize client object with OBSERVERS payload
            NetworkReader reader = new NetworkReader(observersWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            for (int i = 0; i < clientNBs.Count; i++)
            {
                int expected = (i + 1) * 3;
                Assert.That(clientNBs[i].value, Is.EqualTo(expected), $"Expected the clientNBs[{i}] to have a value of {expected}");
            }

            // clear dirty bits for incremental sync
            foreach (SerializeTest1NetworkBehaviour serverNB in serverNBs)
                serverNB.ClearAllDirtyBits();

            // INCREMENTAL SYNC ALL
            // set unique values on server components
            for (int i = 0; i < serverNBs.Count; i++)
            {
                serverNBs[i].value = (i + 1) * 11;
                serverNBs[i].SetDirty();
            }

            ownerWriterReliable.Reset();
            observersWriterReliable.Reset();
            // serialize server object
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false
            );

            // deserialize client object with OBSERVERS payload
            reader = new NetworkReader(observersWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, false);
            for (int i = 0; i < clientNBs.Count; i++)
            {
                int expected = (i + 1) * 11;
                Assert.That(clientNBs[i].value, Is.EqualTo(expected), $"Expected the clientNBs[{i}] to have a value of {expected}");
            }

            // clear dirty bits for incremental sync
            foreach (SerializeTest1NetworkBehaviour serverNB in serverNBs)
                serverNB.ClearAllDirtyBits();

            // INCREMENTAL SYNC INDIVIDUAL
            for (int i = 0; i < numberOfNBs; i++)
            {
                // reset all client nbs
                foreach (SerializeTest1NetworkBehaviour clientNB in clientNBs)
                    clientNB.value = 0;

                int expected = (i + 1) * 7;

                // set unique value on server components
                serverNBs[i].value = expected;
                serverNBs[i].SetDirty();

                ownerWriterReliable.Reset();
                observersWriterReliable.Reset();
                // serialize server object
                serverIdentity.SerializeServer_Broadcast(
                    ownerWriterReliable, observersWriterReliable,
                    ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                    ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                    false
                );

                // deserialize client object with OBSERVERS payload
                reader = new NetworkReader(observersWriterReliable.ToArray());
                clientIdentity.DeserializeClient(reader, false);
                for (int index = 0; index < clientNBs.Count; index++)
                {
                    SerializeTest1NetworkBehaviour clientNB = clientNBs[index];
                    if (index == i)
                    {
                        Assert.That(clientNB.value, Is.EqualTo(expected), $"Expected the clientNBs[{index}] to have a value of {expected}");
                    }
                    else
                    {
                        Assert.That(clientNB.value, Is.EqualTo(0), $"Expected the clientNBs[{index}] to have a value of 0 since we're not syncing that index (on sync of #{i})");
                    }
                }
            }
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
            serverIdentity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);

            // deserialize client object with OWNER payload
            // should work even if compExc throws an exception
            // error log because of the exception is expected
            NetworkReader reader = new NetworkReader(ownerWriterReliable.ToArray());
            clientIdentity.DeserializeClient(reader, true);
            Assert.That(clientComp2.value, Is.EqualTo("42"));

            // reset component values
            clientComp2.value = null;

            // deserialize client object with OBSERVER payload
            // should work even if compExc throws an exception
            // error log because of the exception is expected
            reader = new NetworkReader(observersWriterReliable.ToArray());
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
            byte safety = 0x78; // last byte

            // correct size shouldn't be corrected
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 0, safety), Is.EqualTo(original));

            // read a little too much
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 1, safety), Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 2, safety), Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original + 42, safety), Is.EqualTo(original));

            // read a little too less
            Assert.That(NetworkBehaviour.ErrorCorrection(original - 1, safety), Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original - 2, safety), Is.EqualTo(original));
            Assert.That(NetworkBehaviour.ErrorCorrection(original - 42, safety), Is.EqualTo(original));

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
            serverIdentity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);

            // deserialize on client
            // ignore warning log because of serialization mismatch
            LogAssert.ignoreFailingMessages = true;
            NetworkReader reader = new NetworkReader(ownerWriterReliable.ToArray());
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
        public void SerializeServer_Broadcast_NotDirty_WritesNothing()
        {
            // create spawned so that isServer/isClient is set properly
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp1, out SerializeTest2NetworkBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp1, out SerializeTest2NetworkBehaviour clientComp2);

            // some reliable, some unreliable components
            serverComp1.syncMethod = clientComp1.syncMethod = SyncMethod.Reliable;
            serverComp2.syncMethod = clientComp2.syncMethod = SyncMethod.Hybrid;

            // change nothing
            // serverComp.value = "42";

            // serialize server object.
            // 'initial' would write everything.
            // instead, try 'not initial' with 0 dirty bits
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);

            Assert.That(ownerWriterReliable.Position, Is.EqualTo(0));
            Assert.That(observersWriterReliable.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.EqualTo(0));
        }

        [Test]
        public void SerializeClient_NotInitial_NotDirty_WritesNothing()
        {
            // create spawned so that isServer/isClient is set properly
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp1, out SerializeTest2NetworkBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp1, out SerializeTest2NetworkBehaviour clientComp2);

            // some reliable, some unreliable components
            serverComp1.syncMethod = clientComp1.syncMethod = SyncMethod.Reliable;
            serverComp2.syncMethod = clientComp2.syncMethod = SyncMethod.Hybrid;

            // client only serializes owned ClientToServer components
            clientIdentity.isOwned = true;
            serverComp1.syncDirection = SyncDirection.ClientToServer;
            serverComp2.syncDirection = SyncDirection.ClientToServer;
            clientComp1.syncDirection = SyncDirection.ClientToServer;
            clientComp2.syncDirection = SyncDirection.ClientToServer;

            // change nothing
            // clientComp.value = "42";

            // serialize client object
            clientIdentity.SerializeClient(ownerWriterReliable, ownerWriterUnreliableBaseline, ownerWriterUnreliableDelta, false);
            Assert.That(ownerWriterReliable.Position, Is.EqualTo(0));
            Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0));
            Assert.That(ownerWriterUnreliableDelta.Position, Is.EqualTo(0));
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
            identity.SerializeClient(ownerWriterReliable, new NetworkWriter(), new NetworkWriter(), false);

            // shouldn't sync anything. because even though it's ClientToServer,
            // we don't own this one so we shouldn't serialize & sync it.
            Assert.That(ownerWriterReliable.Position, Is.EqualTo(0));
        }

        // server should still send initial even if Owner + ClientToServer
        [Test]
        public void SerializeServer_OwnerMode_ClientToServer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp1,
                out SyncVarTest2NetworkBehaviour comp2);

            // one Reliable, one Unreliable component
            comp1.syncMethod = SyncMethod.Reliable;
            comp2.syncMethod = SyncMethod.Hybrid;

            // pretend to be owned
            identity.isOwned = true;
            comp1.syncMode = comp2.syncMode = SyncMode.Owner;
            comp1.syncInterval = comp2.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp1.syncDirection = comp2.syncDirection = SyncDirection.ClientToServer;
            comp1.SetValue(11);   // modify with helper function to avoid #3525
            comp2.SetValue("22"); // modify with helper function to avoid #3525

            // initial: should still write for owner
            identity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);
            Debug.Log("initial ownerWriter: " + ownerWriterReliable);
            Debug.Log("initial observerWriter: " + observersWriterReliable);
            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.EqualTo(0));

            // delta: ClientToServer comes from the client
            comp1.SetValue(33);   // modify with helper function to avoid #3525
            comp2.SetValue("44"); // modify with helper function to avoid #3525
            ownerWriterReliable.Position = 0;
            observersWriterReliable.Position = 0;
            identity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);
            Debug.Log("delta ownerWriter: " + ownerWriterReliable);
            Debug.Log("delta observersWriter: " + observersWriterReliable);

            Assert.That(ownerWriterReliable.Position, Is.EqualTo(0));
            Assert.That(observersWriterReliable.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.EqualTo(0));
        }

        // server should still broadcast ClientToServer components to everyone
        // except the owner.
        [Test]
        public void SerializeServer_ObserversMode_ClientToServer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp1,
                out SyncVarTest2NetworkBehaviour comp2);

            // one Reliable, one Unreliable component
            comp1.syncMethod = SyncMethod.Reliable;
            comp2.syncMethod = SyncMethod.Hybrid;

            // pretend to be owned
            identity.isOwned = true;
            comp1.syncMode = comp2.syncMode = SyncMode.Observers;
            comp1.syncInterval = comp2.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp1.syncDirection = comp2.syncDirection = SyncDirection.ClientToServer;
            comp1.SetValue(11);   // modify with helper function to avoid #3525
            comp2.SetValue("22"); // modify with helper function to avoid #3525

            // initial: should still write for owner AND observers
            identity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);
            Debug.Log("initial ownerWriter: " + ownerWriterReliable);
            Debug.Log("initial observerWriter: " + observersWriterReliable);
            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            // delta: should only write for observers
            comp1.SetValue(33);   // modify with helper function to avoid #3525
            comp2.SetValue("44"); // modify with helper function to avoid #3525
            ownerWriterReliable.Position = 0;
            observersWriterReliable.Position = 0;
            identity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);
            Debug.Log("delta ownerWriter: " + ownerWriterReliable);
            Debug.Log("delta observersWriter: " + observersWriterReliable);

            Assert.That(ownerWriterReliable.Position, Is.EqualTo(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.GreaterThan(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.GreaterThan(0));
        }

        [Test]
        public void SerializeServer_ObserversMode_ServerToClient_ReliableAndUnreliable()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp1,
                out SyncVarTest2NetworkBehaviour comp2);

            // one Reliable, one Unreliable component
            comp1.syncMethod = SyncMethod.Reliable;
            comp2.syncMethod = SyncMethod.Hybrid;

            // pretend to be owned
            identity.isOwned = true;
            comp1.syncMode = comp2.syncMode = SyncMode.Observers;
            comp1.syncInterval = comp2.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp1.syncDirection = comp2.syncDirection = SyncDirection.ServerToClient;
            comp1.SetValue(11);   // modify with helper function to avoid #3525
            comp2.SetValue("22"); // modify with helper function to avoid #3525

            // initial: should still write for owner AND observers
            identity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);
            Debug.Log("initial ownerWriter: " + ownerWriterReliable);
            Debug.Log("initial observerWriter: " + observersWriterReliable);
            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            // delta: should write something for all
            comp1.SetValue(33);   // modify with helper function to avoid #3525
            comp2.SetValue("44"); // modify with helper function to avoid #3525
            ownerWriterReliable.Position = 0;
            observersWriterReliable.Position = 0;
            identity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);
            Debug.Log("delta ownerWriter: " + ownerWriterReliable);
            Debug.Log("delta observersWriter: " + observersWriterReliable);

            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            Assert.That(ownerWriterUnreliableBaseline.Position, Is.GreaterThan(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.GreaterThan(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.GreaterThan(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.GreaterThan(0));
        }

        [Test]
        public void SerializeServer_ObserversMode_ServerToClient_ReliableOnly()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp1,
                out SyncVarTest2NetworkBehaviour comp2);

            // one Reliable, one Unreliable component
            comp1.syncMethod = SyncMethod.Reliable;
            comp2.syncMethod = SyncMethod.Hybrid;

            // pretend to be owned
            identity.isOwned = true;
            comp1.syncMode = comp2.syncMode = SyncMode.Observers;
            comp1.syncInterval = comp2.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp1.syncDirection = comp2.syncDirection = SyncDirection.ServerToClient;
            comp1.SetValue(11); // modify with helper function to avoid #3525
            // comp2.SetValue("22"); // Unreliable component doesn't change this time

            // initial: should still write for owner AND observers
            identity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);
            Debug.Log("initial ownerWriter: " + ownerWriterReliable);
            Debug.Log("initial observerWriter: " + observersWriterReliable);
            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            // delta: should write something for all
            comp1.SetValue(33); // modify with helper function to avoid #3525
            // comp2.SetValue("44"); // Unreliable component doesn't change this time
            ownerWriterReliable.Position = 0;
            observersWriterReliable.Position = 0;
            identity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);
            Debug.Log("delta ownerWriter: " + ownerWriterReliable);
            Debug.Log("delta observersWriter: " + observersWriterReliable);

            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.EqualTo(0));
        }

        [Test]
        public void SerializeServer_ObserversMode_ServerToClient_UnreliableOnly()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp1,
                out SyncVarTest2NetworkBehaviour comp2);

            // one Reliable, one Unreliable component
            comp1.syncMethod = SyncMethod.Reliable;
            comp2.syncMethod = SyncMethod.Hybrid;

            // pretend to be owned
            identity.isOwned = true;
            comp1.syncMode = comp2.syncMode = SyncMode.Observers;
            comp1.syncInterval = comp2.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp1.syncDirection = comp2.syncDirection = SyncDirection.ServerToClient;
            // comp1.SetValue(11); // Reliable component doesn't change this time
            comp2.SetValue("22"); // modify with helper function to avoid #3525

            // initial: should still write for owner AND observers
            identity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);
            Debug.Log("initial ownerWriter: " + ownerWriterReliable);
            Debug.Log("initial observerWriter: " + observersWriterReliable);
            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0));
            Assert.That(observersWriterReliable.Position, Is.GreaterThan(0));

            // delta: should write something for all
            // comp1.SetValue(33); // Reliable component doesn't change this time
            comp2.SetValue("44"); // modify with helper function to avoid #3525
            ownerWriterReliable.Position = 0;
            observersWriterReliable.Position = 0;
            identity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);
            Debug.Log("delta ownerWriter: " + ownerWriterReliable);
            Debug.Log("delta observersWriter: " + observersWriterReliable);

            Assert.That(ownerWriterReliable.Position, Is.EqualTo(0));
            Assert.That(observersWriterReliable.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableBaseline.Position, Is.GreaterThan(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.GreaterThan(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.GreaterThan(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.GreaterThan(0));
        }
    }
}
