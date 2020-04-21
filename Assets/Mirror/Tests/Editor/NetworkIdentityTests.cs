using System;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using UnityEngine.Events;

using static Mirror.Tests.LocalConnections;

namespace Mirror.Tests
{
    public class NetworkIdentityTests
    {
        #region test components

        class SetHostVisibilityExceptionNetworkBehaviour : NetworkVisibility
        {
            public int called;
            public bool valuePassed;
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) {}
            public override bool OnCheckObserver(INetworkConnection conn) { return true; }
            public override void OnSetHostVisibility(bool visible)
            {
                ++called;
                valuePassed = visible;
                throw new Exception("some exception");
            }

        }

        class SetHostVisibilityNetworkBehaviour : NetworkVisibility
        {
            public int called;
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) { }
            public override bool OnCheckObserver(INetworkConnection conn) { return true; }
            public override void OnSetHostVisibility(bool visible)
            {
                ++called;
                base.OnSetHostVisibility(visible);
            }
        }

        class CheckObserverExceptionNetworkBehaviour : NetworkVisibility
        {
            public int called;
            public INetworkConnection valuePassed;
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) {}
            public override bool OnCheckObserver(INetworkConnection conn)
            {
                ++called;
                valuePassed = conn;
                throw new Exception("some exception");
            }
            public override void OnSetHostVisibility(bool visible) {}
        }

        class CheckObserverTrueNetworkBehaviour : NetworkVisibility
        {
            public int called;
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) {}
            public override bool OnCheckObserver(INetworkConnection conn)
            {
                ++called;
                return true;
            }
            public override void OnSetHostVisibility(bool visible) {}
        }

        class CheckObserverFalseNetworkBehaviour : NetworkVisibility
        {
            public int called;
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) {}
            public override bool OnCheckObserver(INetworkConnection conn)
            {
                ++called;
                return false;
            }
            public override void OnSetHostVisibility(bool visible) {}
        }

        class SerializeTest1NetworkBehaviour : NetworkBehaviour
        {
            public int value;
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                writer.WriteInt32(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadInt32();
            }
        }

        class SerializeTest2NetworkBehaviour : NetworkBehaviour
        {
            public string value;
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                writer.WriteString(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadString();
            }
        }

        class SerializeExceptionNetworkBehaviour : NetworkBehaviour
        {
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                throw new Exception("some exception");
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                throw new Exception("some exception");
            }
        }

        class SerializeMismatchNetworkBehaviour : NetworkBehaviour
        {
            public int value;
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                writer.WriteInt32(value);
                // one too many
                writer.WriteInt32(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadInt32();
            }
        }

        class RebuildObserversNetworkBehaviour : NetworkVisibility
        {
            public INetworkConnection observer;
            public override bool OnCheckObserver(INetworkConnection conn) { return true; }
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize)
            {
                observers.Add(observer);
            }
            public override void OnSetHostVisibility(bool visible) {}
        }

        class RebuildEmptyObserversNetworkBehaviour : NetworkVisibility
        {
            public override bool OnCheckObserver(INetworkConnection conn) { return true; }
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) {}
            public int hostVisibilityCalled;
            public bool hostVisibilityValue;
            public override void OnSetHostVisibility(bool visible)
            {
                ++hostVisibilityCalled;
                hostVisibilityValue = visible;
            }
        }

        #endregion

        GameObject gameObject;
        NetworkIdentity identity;
        private NetworkServer server;
        private NetworkClient client;
        private GameObject networkServerGameObject;

        IConnection tconn42;
        IConnection tconn43;

        [SetUp]
        public void SetUp()
        {
            networkServerGameObject = new GameObject();
            networkServerGameObject.AddComponent<MockTransport>();
            server = networkServerGameObject.AddComponent<NetworkServer>();
            client = networkServerGameObject.AddComponent<NetworkClient>();

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
            identity.Server = server;

            tconn42 = Substitute.For<IConnection>();
            tconn43 = Substitute.For<IConnection>();
        }

        [TearDown]
        public void TearDown()
        {
            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(networkServerGameObject);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void OnStartServerTest()
        {
            // lets add a component to check OnStartserver
            UnityAction func1 = Substitute.For<UnityAction>();
            UnityAction func2 = Substitute.For<UnityAction>();

            identity.OnStartServer.AddListener(func1);
            identity.OnStartServer.AddListener(func2);

            identity.StartServer();

            func1.Received(1).Invoke();
            func2.Received(1).Invoke();
        }

        [Test]
        public void GetSetAssetId()
        {
            // assign a guid
            var guid = new Guid(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
            identity.AssetId = guid;

            // did it work?
            Assert.That(identity.AssetId, Is.EqualTo(guid));
        }

        [Test]
        public void SetClientOwner()
        {
            // SetClientOwner
            (_, NetworkConnection original) = PipedConnections();
            identity.SetClientOwner(original);
            Assert.That(identity.ConnectionToClient, Is.EqualTo(original));
        }

        [Test]
        public void SetOverrideClientOwner()
        {
            // SetClientOwner
            (_, NetworkConnection original) = PipedConnections();
            identity.SetClientOwner(original);

            // setting it when it's already set shouldn't overwrite the original
            (_, NetworkConnection overwrite) = PipedConnections();
            // will log a warning
            Assert.Throws<InvalidOperationException>(() =>
            {
                identity.SetClientOwner(overwrite);
            });

            Assert.That(identity.ConnectionToClient, Is.EqualTo(original));
        }

        [Test]
        public void RemoveObserverInternal()
        {
            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add an observer connection
            INetworkConnection connection = Substitute.For<INetworkConnection>();
            identity.observers.Add(connection);

            INetworkConnection connection2 = Substitute.For<INetworkConnection>();
            // RemoveObserverInternal with invalid connection should do nothing
            identity.RemoveObserverInternal(connection2);
            Assert.That(identity.observers, Is.EquivalentTo (new[] { connection }));

            // RemoveObserverInternal with existing connection should remove it
            identity.RemoveObserverInternal(connection);
            Assert.That(identity.observers, Is.Empty);
        }

        [Test]
        public void AssignSceneID()
        {
            // Awake will have assigned a random sceneId of format 0x00000000FFFFFFFF
            // -> make sure that one was assigned, and that the left part was
            //    left empty for scene hash
            Assert.That(identity.sceneId, !Is.Zero);
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, Is.EqualTo(0x0000000000000000));

            // make sure that Awake added it to sceneIds dict
            Assert.That(NetworkIdentity.GetSceneIdentity(identity.sceneId), !Is.Null);
        }

        [Test]
        public void SetSceneIdSceneHashPartInternal()
        {
            // Awake will have assigned a random sceneId of format 0x00000000FFFFFFFF
            // -> make sure that one was assigned, and that the left part was
            //    left empty for scene hash
            Assert.That(identity.sceneId, !Is.Zero);
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, Is.EqualTo(0x0000000000000000));
            ulong rightPart = identity.sceneId;

            // set scene hash
            identity.SetSceneIdSceneHashPartInternal();

            // make sure that the right part is still the random sceneid
            Assert.That(identity.sceneId & 0x00000000FFFFFFFF, Is.EqualTo(rightPart));

            // make sure that the left part is a scene hash now
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, !Is.Zero);
            ulong finished = identity.sceneId;

            // calling it again should said the exact same hash again
            identity.SetSceneIdSceneHashPartInternal();
            Assert.That(identity.sceneId, Is.EqualTo(finished));
        }

        [Test]
        public void OnValidateSetupIDsSetsEmptyAssetIDForSceneObject()
        {
            // OnValidate will have been called. make sure that assetId was set
            // to 0 empty and not anything else, because this is a scene object
            Assert.That(identity.AssetId, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void OnStartServerCallsComponentsAndCatchesExceptions()
        {
            // make a mock delegate
            UnityAction func = Substitute.For<UnityAction>();

            // add it to the listener
            identity.OnStartServer.AddListener(func);

            // Since we are testing that exceptions are not swallowed,
            // when the mock is invoked, throw an exception 
            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // Make sure that the exception is not swallowed
            Assert.Throws<Exception>(() =>
            {
                identity.StartServer();
            });

            // ask the mock if it got invoked
            // if the mock is not invoked,  then this fails
            // This is a type of assert
            func.Received().Invoke();
        }

        [Test]
        public void OnStartClientCallsComponentsAndCatchesExceptions()
        {
            // add component
            UnityAction func = Substitute.For<UnityAction>();
            identity.OnStartClient.AddListener(func);

            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // make sure exceptions are not swallowed
            Assert.Throws<Exception>(() =>
            {
                identity.StartClient();
            });
            func.Received().Invoke();

            // we have checks to make sure that it's only called once.
            Assert.DoesNotThrow(() =>
            {
                identity.StartClient();
            });
            func.Received(1).Invoke();
        }

        [Test]
        public void OnStartAuthorityCallsComponentsAndCatchesExceptions()
        {
            // add component
            UnityAction func = Substitute.For<UnityAction>();
            identity.OnStartAuthority.AddListener(func);

            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // make sure exceptions are not swallowed
            Assert.Throws<Exception>(() =>
            {
                identity.StartAuthority();
            });
            func.Received(1).Invoke();
        }

        [Test]
        public void OnStopAuthorityCallsComponentsAndCatchesExceptions()
        {
            // add component
            UnityAction func = Substitute.For<UnityAction>();
            identity.OnStopAuthority.AddListener(func);

            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // make sure exceptions are not swallowed
            Assert.Throws<Exception>(() =>
            {
                identity.StopAuthority();
            });
            func.Received(1).Invoke();
        }

        [Test]
        public void NotifyAuthorityCallsOnStartStopAuthority()
        {
            // add components
            UnityAction startAuthFunc = Substitute.For<UnityAction>();
            UnityAction stopAuthFunc = Substitute.For<UnityAction>();

            identity.OnStartAuthority.AddListener(startAuthFunc);
            identity.OnStopAuthority.AddListener(stopAuthFunc);

            // set authority from false to true, which should call OnStartAuthority
            identity.HasAuthority = true;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.HasAuthority, Is.True);
            // start should be called
            startAuthFunc.Received(1).Invoke();
            stopAuthFunc.Received(0).Invoke();

            // set it to true again, should do nothing because already true
            identity.HasAuthority = true;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.HasAuthority, Is.True);
            // same as before
            startAuthFunc.Received(1).Invoke();
            stopAuthFunc.Received(0).Invoke();

            // set it to false, should call OnStopAuthority
            identity.HasAuthority = false;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.HasAuthority, Is.False);
            // same as before
            startAuthFunc.Received(1).Invoke();
            stopAuthFunc.Received(1).Invoke();

            // set it to false again, should do nothing because already false
            identity.HasAuthority = false;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.HasAuthority, Is.False);
            // same as before
            startAuthFunc.Received(1).Invoke();
            stopAuthFunc.Received(1).Invoke();
        }

        [Test]
        public void OnSetHostVisibilityCallsComponentsAndCatchesExceptions()
        {
            // add component
            SetHostVisibilityExceptionNetworkBehaviour comp = gameObject.AddComponent<SetHostVisibilityExceptionNetworkBehaviour>();

            Assert.Throws<Exception>(() =>
            {
               identity.OnSetHostVisibility(true);
            });
        }

        [Test]
        public void OnCheckObserverCatchesException()
        {
            // add component
            CheckObserverExceptionNetworkBehaviour compExc = gameObject.AddComponent<CheckObserverExceptionNetworkBehaviour>();

            var connection = new NetworkConnection(null);

            // should catch the exception internally and not throw it
            Assert.Throws<Exception>(() =>
            {
                bool result = identity.OnCheckObserver(connection);
            });
        }

        [Test]
        public void OnCheckObserverTrue()
        {
            // create a networkidentity with a component that returns true
            // result should still be true.
            var gameObjectTrue = new GameObject();
            NetworkIdentity identityTrue = gameObjectTrue.AddComponent<NetworkIdentity>();
            CheckObserverTrueNetworkBehaviour compTrue = gameObjectTrue.AddComponent<CheckObserverTrueNetworkBehaviour>();
            var connection = new NetworkConnection(null);
            Assert.That(identityTrue.OnCheckObserver(connection), Is.True);
            Assert.That(compTrue.called, Is.EqualTo(1));
        }

        [Test]
        public void OnCheckObserverFalse()
        {
            // create a networkidentity with a component that returns true and
            // one component that returns false.
            // result should still be false if any one returns false.
            var gameObjectFalse = new GameObject();
            NetworkIdentity identityFalse = gameObjectFalse.AddComponent<NetworkIdentity>();
            CheckObserverFalseNetworkBehaviour compFalse = gameObjectFalse.AddComponent<CheckObserverFalseNetworkBehaviour>();
            var connection = new NetworkConnection(null);
            Assert.That(identityFalse.OnCheckObserver(connection), Is.False);
            Assert.That(compFalse.called, Is.EqualTo(1));
        }

        [Test]
        public void OnSerializeAndDeserializeAllSafely()
        {
            // create a networkidentity with our test components
            SerializeTest1NetworkBehaviour comp1 = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            SerializeExceptionNetworkBehaviour compExc = gameObject.AddComponent<SerializeExceptionNetworkBehaviour>();
            SerializeTest2NetworkBehaviour comp2 = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();

            // set some unique values to serialize
            comp1.value = 12345;
            comp1.syncMode = SyncMode.Observers;
            compExc.syncMode = SyncMode.Observers;
            comp2.value = "67890";
            comp2.syncMode = SyncMode.Owner;

            // serialize all
            var ownerWriter = new NetworkWriter();
            var observersWriter = new NetworkWriter();

            // serialize should propagate exceptions
            Assert.Throws<Exception>(() =>
            {
                identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);
            });

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for owner - should work even if compExc throws an exception
            var reader = new NetworkReader(ownerWriter.ToArray());

            Assert.Throws<Exception>(() =>
            {
                identity.OnDeserializeAllSafely(reader, true);
            });

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for observers - should propagate exceptions
            reader = new NetworkReader(observersWriter.ToArray());
            Assert.Throws<Exception>(() =>
            {
                identity.OnDeserializeAllSafely(reader, true);
            });
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void NoMoreThan64Components()
        {
            // add 65 components
            for (int i = 0; i < 65; ++i)
            {
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            }
            // ingore error from creating cache (has its own test)
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = identity.NetworkBehaviours;
            });
        }

        // OnDeserializeSafely should be able to detect and handle serialization
        // mismatches (= if compA writes 10 bytes but only reads 8 or 12, it
        // shouldn't break compB's serialization. otherwise we end up with
        // insane runtime errors like monsters that look like npcs. that's what
        // happened back in the day with UNET).
        [Test]
        public void OnDeserializeSafelyShouldDetectAndHandleDeSerializationMismatch()
        {
            // add components
            SerializeTest1NetworkBehaviour comp1 = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            SerializeMismatchNetworkBehaviour compMiss = gameObject.AddComponent<SerializeMismatchNetworkBehaviour>();
            SerializeTest2NetworkBehaviour comp2 = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();

            // set some unique values to serialize
            comp1.value = 12345;
            comp2.value = "67890";

            // serialize
            var ownerWriter = new NetworkWriter();
            var observersWriter = new NetworkWriter();
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all
            var reader = new NetworkReader(ownerWriter.ToArray());
            Assert.Throws<InvalidMessageException>(() =>
            {
                identity.OnDeserializeAllSafely(reader, true);
            });
        }

        [Test]
        public void OnStartLocalPlayer()
        {
            // add components
            UnityAction funcEx = Substitute.For<UnityAction>();
            UnityAction func = Substitute.For<UnityAction>();

            identity.OnStartLocalPlayer.AddListener(funcEx);
            identity.OnStartLocalPlayer.AddListener(func);

            funcEx
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });


            // make sure that comp.OnStartServer was called
            // the exception was caught and not thrown in here.
            Assert.Throws<Exception>(() =>
            {
                identity.StartLocalPlayer();
            });

            funcEx.Received(1).Invoke();
            //Due to the order the listeners are added the one without exception is never called
            func.Received(0).Invoke();

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            Assert.DoesNotThrow(() =>
            {
                identity.StartLocalPlayer();
            });
            // same as before?
            funcEx.Received(1).Invoke();
            //Due to the order the listeners are added the one without exception is never called
            func.Received(0).Invoke();
        }

        [Test]
        public void OnStopClient()
        {
            UnityAction mockCallback = Substitute.For<UnityAction>();
            identity.OnStopClient.AddListener(mockCallback);

            identity.StopClient();

            mockCallback.Received().Invoke();
        }

        [Test]
        public void OnStopServer()
        {
            UnityAction mockCallback = Substitute.For<UnityAction>();
            identity.OnStopServer.AddListener(mockCallback);

            identity.StopServer();

            mockCallback.Received().Invoke();
        }

        [Test]
        public void OnStopServerEx()
        {
            UnityAction mockCallback = Substitute.For<UnityAction>();
            mockCallback
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            identity.OnStopServer.AddListener(mockCallback);

            Assert.Throws<Exception>(() => {
                identity.StopServer();
            });
        }

        [Test]
        public void AddObserver()
        {

            identity.Server = server;
            // create some connections
            var connection1 = new NetworkConnection(null);
            var connection2 = new NetworkConnection(null);

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // call AddObservers
            identity.AddObserver(connection1);
            identity.AddObserver(connection2);
            Assert.That(identity.observers, Is.EquivalentTo(new[] { connection1, connection2 }));

            // adding a duplicate connectionId shouldn't overwrite the original
            identity.AddObserver(connection1);
            Assert.That(identity.observers, Is.EquivalentTo(new[] { connection1, connection2 }));
        }

        [Test]
        public void ClearObservers()
        {
            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add some observers
            identity.observers.Add(new NetworkConnection(null));
            identity.observers.Add(new NetworkConnection(null));

            // call ClearObservers
            identity.ClearObservers();
            Assert.That(identity.observers.Count, Is.EqualTo(0));
        }


        [Test]
        public void Reset()
        {
            // creates .observers and generates a netId
            identity.StartServer();
            identity.ConnectionToClient = new NetworkConnection(null);
            identity.ConnectionToServer = new NetworkConnection(null);
            identity.observers.Add(new NetworkConnection(null));

            // mark for reset and reset
            identity.Reset();
            Assert.That(identity.NetId, Is.EqualTo(0));
            Assert.That(identity.ConnectionToClient, Is.Null);
            Assert.That(identity.ConnectionToServer, Is.Null);
        }

        [Test]
        public void ResetNextNetworkIdTest()
        {
            //Generate some Ids
            NetworkIdentity.GetNextNetworkId();
            NetworkIdentity.GetNextNetworkId();
            NetworkIdentity.GetNextNetworkId();
            NetworkIdentity.GetNextNetworkId();

            uint cache = NetworkIdentity.GetNextNetworkId();
            Assert.That(cache, Is.GreaterThan(1));

            NetworkIdentity.ResetNextNetworkId();
            cache = NetworkIdentity.GetNextNetworkId();
            Assert.That(cache, Is.EqualTo(1));
        }

        [Test]
        public void GetNewObservers()
        {
            // add components
            RebuildObserversNetworkBehaviour comp = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            comp.observer = new NetworkConnection(null);

            // get new observers
            var observers = new HashSet<INetworkConnection>();
            bool result = identity.GetNewObservers(observers, true);
            Assert.That(result, Is.True);
            Assert.That(observers.Count, Is.EqualTo(1));
            Assert.That(observers.Contains(comp.observer), Is.True);
        }

        [Test]
        public void GetNewObserversClearsHashSet()
        {
            // get new observers. no observer components so it should just clear
            // it and not do anything else
            var observers = new HashSet<INetworkConnection>
            {
                new NetworkConnection(null)
            };
            identity.GetNewObservers(observers, true);
            Assert.That(observers.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetNewObserversFalseIfNoComponents()
        {
            // get new observers. no observer components so it should be false
            var observers = new HashSet<INetworkConnection>();
            bool result = identity.GetNewObservers(observers, true);
            Assert.That(result, Is.False);
        }

        [Test]
        public void AddAllReadyServerConnectionsToObservers()
        {
            var connection1 = new NetworkConnection(tconn42) { IsReady = true };
            var connection2 = new NetworkConnection(tconn43) { IsReady = false };
            // add some server connections
            server.connections.Add(connection1);
            server.connections.Add(connection2);

            // add a host connection
            (_, IConnection localConnection) = PipeConnection.CreatePipe();

            server.SetLocalConnection(client, localConnection);
            server.LocalConnection.IsReady = true;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add all to observers. should have the two ready connections then.
            identity.AddAllReadyServerConnectionsToObservers();
            Assert.That(identity.observers, Is.EquivalentTo(new[] { connection1, server.LocalConnection }));

            // clean up
            server.Disconnect();
        }

        // RebuildObservers should always add the own ready connection
        // (if any). fixes https://github.com/vis2k/Mirror/issues/692
        [Test]
        public void RebuildObserversAddsOwnReadyPlayer()
        {
            // add at least one observers component, otherwise it will just add
            // all server connections
            gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();

            // add own player connection
            (_, NetworkConnection connection) = PipedConnections();
            connection.IsReady = true;
            identity.ConnectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // rebuild should at least add own ready player
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Does.Contain(identity.ConnectionToClient));
        }

        // RebuildObservers should always add the own ready connection
        // (if any). fixes https://github.com/vis2k/Mirror/issues/692
        [Test]
        public void RebuildObserversOnlyAddsOwnPlayerIfReady()
        {
            // add at least one observers component, otherwise it will just add
            // all server connections
            gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();

            // add own player connection that isn't ready
            (_, NetworkConnection connection) = PipedConnections();
            identity.ConnectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // rebuild shouldn't add own player because conn wasn't set ready
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Does.Not.Contains(identity.ConnectionToClient));
        }

        [Test]
        public void RebuildObserversAddsReadyConnectionsIfImplemented() { 

            // add a proximity checker
            // one with a ready connection, one with no ready connection, one with null connection
            RebuildObserversNetworkBehaviour comp = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            comp.observer = Substitute.For<INetworkConnection>();
            comp.observer.IsReady.Returns(true);

            // rebuild observers should add all component's ready observers
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Is.EquivalentTo( new[] { comp.observer }));
        }


        [Test]
        public void RebuildObserversDoesntAddNotReadyConnectionsIfImplemented()
        {
            // add a proximity checker
            // one with a ready connection, one with no ready connection, one with null connection
            RebuildObserversNetworkBehaviour comp = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            comp.observer = Substitute.For<INetworkConnection>();
            comp.observer.IsReady.Returns(false);

            // rebuild observers should add all component's ready observers
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Is.Empty);
        }

        [Test]
        public void RebuildObserversAddsReadyServerConnectionsIfNotImplemented()
        {
            INetworkConnection readyConnection = Substitute.For<INetworkConnection>();
            readyConnection.IsReady.Returns(true);
            INetworkConnection notReadyConnection = Substitute.For<INetworkConnection>();
            notReadyConnection.IsReady.Returns(false);

            // add some server connections
            server.connections.Add(readyConnection);
            server.connections.Add(notReadyConnection);

            // rebuild observers should add all ready server connections
            // because no component implements OnRebuildObservers
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Is.EquivalentTo(new[] { readyConnection }));
        }

        [Test]
        public void OnSetHostVisibilityBaseTest()
        {
            SpriteRenderer renderer;

            renderer = gameObject.AddComponent<SpriteRenderer>();
            SetHostVisibilityNetworkBehaviour comp = gameObject.AddComponent<SetHostVisibilityNetworkBehaviour>();
            comp.OnSetHostVisibility(false);

            Assert.That(comp.called, Is.EqualTo(1));
            Assert.That(renderer.enabled, Is.False);
        }
    }
}
