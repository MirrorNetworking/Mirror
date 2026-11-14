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

            // Ensure clean state
            NetworkServer.Shutdown();
            NetworkClient.Shutdown();
            NetworkServer.connections.Clear();

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

            // unreliableBaseline=false: no baseline mask or data should be written
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

            // unreliableBaseline=false: no baseline mask or data should be written
            Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0));
            Assert.That(observersWriterUnreliableBaseline.Position, Is.EqualTo(0));

            Assert.That(ownerWriterUnreliableDelta.Position, Is.GreaterThan(0));
            Assert.That(observersWriterUnreliableDelta.Position, Is.GreaterThan(0));
        }

        [Test]
        public void UnreliableBaseline_Timing()
        {
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp);

            serverComp.syncMethod = clientComp.syncMethod = SyncMethod.Hybrid;
            serverComp.syncInterval = 0;

            // === SCENARIO 1: First baseline sync clears dirty bits ===
            serverComp.value = 50;
            serverComp.SetDirty();
            ResetWriters();
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                true); // unreliableBaseline = true

            int firstBaselineSize = observersWriterUnreliableBaseline.Position;
            Assert.That(firstBaselineSize, Is.GreaterThan(1), "First baseline: Should write mask + data");
            Assert.That(serverComp.IsDirty_BitsOnly(), Is.False, "Dirty bits should be cleared after baseline");

            // === SCENARIO 2: Change and serialize with unreliableBaseline=false (delta only) ===
            serverComp.value = 100;
            serverComp.SetDirty();

            ResetWriters();
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false); // unreliableBaseline = false - ONLY want delta

            int deltaOnlyBaselineSize = observersWriterUnreliableBaseline.Position;
            int deltaSize = observersWriterUnreliableDelta.Position;

            // Delta should be written
            Assert.That(deltaSize, Is.GreaterThan(0), "Delta should be written");

            // unreliableBaseline=false: only delta is sent, baseline writers stay empty
            Assert.That(deltaOnlyBaselineSize, Is.EqualTo(0),
                "No baseline data when unreliableBaseline=false");

            // === SCENARIO 3: Serialize with unreliableBaseline=true ===
            serverComp.value = 200;
            serverComp.SetDirty();
            ResetWriters();
            serverIdentity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                true); // unreliableBaseline = true

            Assert.That(observersWriterUnreliableDelta.Position, Is.GreaterThan(0), "Delta written");
            Assert.That(observersWriterUnreliableBaseline.Position, Is.GreaterThan(1),
                "Baseline should write mask + data");
        }

        [Test]
        public void SerializeClient_UnreliableBaseline_ClearsBitsNotSyncTime()
        {
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp);

            clientIdentity.isOwned = true;
            serverComp.syncMethod = clientComp.syncMethod = SyncMethod.Hybrid;
            serverComp.syncDirection = clientComp.syncDirection = SyncDirection.ClientToServer;

            // Set a HIGH syncInterval so delta sync is NOT triggered
            // This ensures only baseline is dirty (IsDirty_BitsOnly=true, IsDirty=false)
            clientComp.syncInterval = 999f;

            // Make dirty and capture initial lastSyncTime
            clientComp.value = 100;
            clientComp.SetDirty();

            // Update lastSyncTime to current time so interval check fails for delta
            clientComp.lastSyncTime = NetworkTime.localTime;
            double lastSyncTime = clientComp.lastSyncTime;

            // Verify: baseline dirty (bits set) but delta NOT dirty (interval not elapsed)
            Assert.That(clientComp.IsDirty_BitsOnly(), Is.True, "Baseline should be dirty");
            Assert.That(clientComp.IsDirty(), Is.False, "Delta should NOT be dirty (interval not elapsed)");

            // Serialize baseline
            clientIdentity.SerializeClient(ownerWriterReliable, ownerWriterUnreliableBaseline, ownerWriterUnreliableDelta, true);

            // Dirty bits should be cleared, but lastSyncTime should NOT change
            // (because only delta sync updates lastSyncTime, not baseline sync)
            Assert.That(clientComp.IsDirty_BitsOnly(), Is.False, "Dirty bits should be cleared after baseline");
            Assert.That(clientComp.lastSyncTime, Is.EqualTo(lastSyncTime), "lastSyncTime should NOT be updated by baseline sync");
        }

        [Test]
        public void DeserializeServer_ClientToServer_OnlyAcceptsOwnedComponents()
        {
            // SetUp() already started server and connected client
            // Use the existing connectionToClient from SetUp

            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp,
                connectionToClient); // Spawn with owner

            // Explicitly set client as owned (in real game this happens via spawn message)
            clientIdentity.isOwned = true;

            // Setup ClientToServer component
            serverComp.syncDirection = clientComp.syncDirection = SyncDirection.ClientToServer;
            clientComp.value = 42;
            clientComp.SetDirty(); // Mark as dirty so it serializes

            // Client serializes
            NetworkWriter clientWriter = new NetworkWriter();
            clientIdentity.SerializeClient(clientWriter, new NetworkWriter(), new NetworkWriter(), false);

            // Verify something was written
            Assert.That(clientWriter.Position, Is.GreaterThan(0), "Client should have serialized data");

            // Server deserializes - should accept because connection owns it
            NetworkReader reader = new NetworkReader(clientWriter.ToArray());
            bool result = serverIdentity.DeserializeServer(reader, false);

            Assert.That(result, Is.True);
            Assert.That(serverComp.value, Is.EqualTo(42));

            // Verify component was marked dirty for broadcast to other clients
            Assert.That(serverComp.IsDirty(), Is.True);
        }

        [Test]
        public void DeserializeServer_Exploit_RejectsServerToClientChanges()
        {
            // SetUp() already started server and connected client
            // Use the existing connectionToClient from SetUp

            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SerializeTest1NetworkBehaviour serverComp,
                out _, out NetworkIdentity clientIdentity, out SerializeTest1NetworkBehaviour clientComp,
                connectionToClient); // Spawn with owner

            // Explicitly set client as owned
            clientIdentity.isOwned = true;

            // Setup ServerToClient (client shouldn't be able to change this)
            serverComp.syncDirection = clientComp.syncDirection = SyncDirection.ServerToClient;
            serverComp.value = 100;

            // Malicious client tries to serialize ServerToClient component
            clientComp.value = 999; // hacker value
            clientComp.SetDirty(); // Mark as dirty

            // Build a malicious payload manually
            // The client code won't serialize ServerToClient, so we need to fake it
            NetworkWriter hackerWriter = new NetworkWriter();

            // Manually write dirty mask for component (pretending it serialized)
            Compression.CompressVarUInt(hackerWriter, 1ul); // first component dirty
            clientComp.Serialize(hackerWriter, false);

            // Server deserializes - should IGNORE because it's ServerToClient
            NetworkReader reader = new NetworkReader(hackerWriter.ToArray());
            bool result = serverIdentity.DeserializeServer(reader, false);

            // Should succeed but not change value (server ignores non-ClientToServer components)
            Assert.That(result, Is.True);
            Assert.That(serverComp.value, Is.EqualTo(100)); // unchanged
        }

        [Test]
        public void VarInt_DirtyMask_Compression()
        {
            // Test varint efficiency claims in comments
            CreateNetworked(out GameObject _, out NetworkIdentity identity, ni =>
            {
                // Add 7 components (should fit in 1 byte varint)
                for (int i = 0; i < 7; i++)
                {
                    SerializeTest1NetworkBehaviour nb = ni.gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
                    nb.syncInterval = 0;
                }
            });

            identity.InitializeNetworkBehaviours();

            // Make all 7 dirty
            foreach (var comp in identity.NetworkBehaviours)
                comp.SetDirty();

            NetworkWriter writer = new NetworkWriter();
            identity.SerializeServer_Broadcast(
                writer, new NetworkWriter(),
                new NetworkWriter(), new NetworkWriter(),
                new NetworkWriter(), new NetworkWriter(),
                false);

            byte[] data = writer.ToArray();
            // First byte should be varint-compressed mask (7 bits set = value 127 = 0x7F)
            // Varint of 127 is 1 byte
            Assert.That(data[0], Is.LessThanOrEqualTo(0xFF)); // fits in 1 byte
        }

        [Test]
        public void SerializeServer_SyncModeAndDirection_Matrix(
            [Values(SyncMode.Owner, SyncMode.Observers)] SyncMode syncMode,
            [Values(SyncDirection.ServerToClient, SyncDirection.ClientToServer)] SyncDirection syncDirection,
            [Values(SyncMethod.Reliable, SyncMethod.Hybrid)] SyncMethod syncMethod)
        {
            // isOwned is a client-side flag and does not affect server serialization.
            // Server always serializes based on syncMode/syncDirection/syncMethod only.

            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out SyncVarTest1NetworkBehaviour comp1,
                out SyncVarTest2NetworkBehaviour comp2);

            // Setup components
            comp1.syncMethod = comp2.syncMethod = syncMethod;
            comp1.syncMode = comp2.syncMode = syncMode;
            comp1.syncDirection = comp2.syncDirection = syncDirection;
            comp1.syncInterval = comp2.syncInterval = 0;

            // Set initial values
            comp1.SetValue(11);
            comp2.SetValue("22");

            // --- SPAWN SERIALIZATION ---
            // Spawn always sends to owner (all components, regardless of mode/direction).
            // Spawn sends to observers ONLY for SyncMode.Observers (SyncDirection irrelevant).
            identity.SerializeServer_Spawn(ownerWriterReliable, observersWriterReliable);

            Assert.That(ownerWriterReliable.Position, Is.GreaterThan(0),
                $"[Spawn] Owner should always receive data. SyncMode={syncMode}, SyncDirection={syncDirection}");

            if (syncMode == SyncMode.Observers)
            {
                Assert.That(observersWriterReliable.Position, Is.GreaterThan(0),
                    $"[Spawn] Observers should receive data when SyncMode=Observers. SyncDirection={syncDirection}");
            }
            else
            {
                Assert.That(observersWriterReliable.Position, Is.EqualTo(0),
                    $"[Spawn] Observers should NOT receive data when SyncMode=Owner. SyncDirection={syncDirection}");
            }

            // --- BROADCAST SERIALIZATION ---
            // Owner receives delta only when SyncDirection=ServerToClient.
            //   ClientToServer comes FROM the client - server doesn't send it back.
            // Observers receive delta only when SyncMode=Observers.
            //   SyncDirection is irrelevant for observers.
            ResetWriters();
            comp1.SetValue(33);
            comp2.SetValue("44");

            identity.SerializeServer_Broadcast(
                ownerWriterReliable, observersWriterReliable,
                ownerWriterUnreliableBaseline, observersWriterUnreliableBaseline,
                ownerWriterUnreliableDelta, observersWriterUnreliableDelta,
                false);

            bool expectOwner = syncDirection == SyncDirection.ServerToClient;
            bool expectObservers = syncMode == SyncMode.Observers;

            if (syncMethod == SyncMethod.Reliable)
            {
                // Reliable: data goes to reliable writers only
                Assert.That(ownerWriterReliable.Position, NonZeroIf(expectOwner),
                    $"[Broadcast/Reliable] Owner. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(observersWriterReliable.Position, NonZeroIf(expectObservers),
                    $"[Broadcast/Reliable] Observers. SyncMode={syncMode}, SyncDirection={syncDirection}");

                // Unreliable writers must be empty for Reliable components
                Assert.That(ownerWriterUnreliableDelta.Position, Is.EqualTo(0),
                    $"[Broadcast/Reliable] Unreliable delta must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(observersWriterUnreliableDelta.Position, Is.EqualTo(0),
                    $"[Broadcast/Reliable] Unreliable delta must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0),
                    $"[Broadcast/Reliable] Unreliable baseline must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(observersWriterUnreliableBaseline.Position, Is.EqualTo(0),
                    $"[Broadcast/Reliable] Unreliable baseline must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
            }
            else if (syncMethod == SyncMethod.Hybrid)
            {
                // Hybrid: data goes to unreliable writers only during delta broadcast
                Assert.That(ownerWriterUnreliableDelta.Position, NonZeroIf(expectOwner),
                    $"[Broadcast/Hybrid/Delta] Owner. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(observersWriterUnreliableDelta.Position, NonZeroIf(expectObservers),
                    $"[Broadcast/Hybrid/Delta] Observers. SyncMode={syncMode}, SyncDirection={syncDirection}");

                // Reliable writers must be empty for Hybrid components
                Assert.That(ownerWriterReliable.Position, Is.EqualTo(0),
                    $"[Broadcast/Hybrid] Reliable must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(observersWriterReliable.Position, Is.EqualTo(0),
                    $"[Broadcast/Hybrid] Reliable must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");

                // unreliableBaseline=false: no baseline mask or data should be written
                Assert.That(ownerWriterUnreliableBaseline.Position, Is.EqualTo(0),
                    $"[Broadcast/Hybrid] Unreliable baseline must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
                Assert.That(observersWriterUnreliableBaseline.Position, Is.EqualTo(0),
                    $"[Broadcast/Hybrid] Unreliable baseline must be empty. SyncMode={syncMode}, SyncDirection={syncDirection}");
            }
        }

        // Returns a constraint checking for non-zero or zero.
        // Needed because Unity 2020's compiler cannot infer the common type
        // of GreaterThanConstraint and EqualConstraint in a ternary expression.
        static NUnit.Framework.Constraints.IResolveConstraint NonZeroIf(bool expect) =>
            expect ? (NUnit.Framework.Constraints.IResolveConstraint)Is.GreaterThan(0) : Is.EqualTo(0);
    }
}
