using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkIdentityTests
    {
        class MyTestComponent : NetworkBehaviour
        {
            internal bool onStartServerInvoked;

            public override void OnStartServer()
            {
                onStartServerInvoked = true;
                base.OnStartServer();
            }
        }

        class StartServerExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStartServer()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class StartClientExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStartClient()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class StartAuthorityExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStartAuthority()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class StartAuthorityCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStartAuthority() { ++called; }
        }

        class StopAuthorityExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStopAuthority()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class StopAuthorityCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStopAuthority() { ++called; }
        }

        class StartLocalPlayerExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStartLocalPlayer()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class StartLocalPlayerCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStartLocalPlayer() { ++called; }
        }

        class NetworkDestroyExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnNetworkDestroy()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class NetworkDestroyCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnNetworkDestroy() { ++called; }
        }

        class SetHostVisibilityExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public bool valuePassed;
            public override void OnSetHostVisibility(bool visible)
            {
                ++called;
                valuePassed = visible;
                throw new Exception("some exception");
            }
        }

        class CheckObserverExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public NetworkConnection valuePassed;
            public override bool OnCheckObserver(NetworkConnection conn)
            {
                ++called;
                valuePassed = conn;
                throw new Exception("some exception");
            }
        }

        class CheckObserverTrueNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override bool OnCheckObserver(NetworkConnection conn)
            {
                ++called;
                return true;
            }
        }

        class CheckObserverFalseNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override bool OnCheckObserver(NetworkConnection conn)
            {
                ++called;
                return false;
            }
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
                writer.WriteInt32(value); // one too many
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadInt32();
            }
        }

        class RebuildObserversNetworkBehaviour : NetworkBehaviour
        {
            public NetworkConnection observer;
            public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
            {
                observers.Add(observer);
                return true;
            }
        }

        class RebuildEmptyObserversNetworkBehaviour : NetworkBehaviour
        {
            public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
            {
                // return true so that caller knows we implemented
                // OnRebuildObservers, but return no observers
                return true;
            }

            public int hostVisibilityCalled;
            public bool hostVisibilityValue;
            public override void OnSetHostVisibility(bool visible)
            {
                ++hostVisibilityCalled;
                hostVisibilityValue = visible;
            }
        }

        class IsClientServerCheckComponent : NetworkBehaviour
        {
            // OnStartClient
            internal bool OnStartClient_isClient;
            internal bool OnStartClient_isServer;
            internal bool OnStartClient_isLocalPlayer;
            public override void OnStartClient()
            {
                OnStartClient_isClient = isClient;
                OnStartClient_isServer = isServer;
                OnStartClient_isLocalPlayer = isLocalPlayer;
            }

            // OnStartServer
            internal bool OnStartServer_isClient;
            internal bool OnStartServer_isServer;
            internal bool OnStartServer_isLocalPlayer;
            public override void OnStartServer()
            {
                OnStartServer_isClient = isClient;
                OnStartServer_isServer = isServer;
                OnStartServer_isLocalPlayer = isLocalPlayer;
            }

            // OnStartLocalPlayer
            internal bool OnStartLocalPlayer_isClient;
            internal bool OnStartLocalPlayer_isServer;
            internal bool OnStartLocalPlayer_isLocalPlayer;
            public override void OnStartLocalPlayer()
            {
                OnStartLocalPlayer_isClient = isClient;
                OnStartLocalPlayer_isServer = isServer;
                OnStartLocalPlayer_isLocalPlayer = isLocalPlayer;
            }

            // Start
            internal bool Start_isClient;
            internal bool Start_isServer;
            internal bool Start_isLocalPlayer;
            public void Start()
            {
                Start_isClient = isClient;
                Start_isServer = isServer;
                Start_isLocalPlayer = isLocalPlayer;
            }

            // OnDestroy
            internal bool OnDestroy_isClient;
            internal bool OnDestroy_isServer;
            internal bool OnDestroy_isLocalPlayer;
            public void OnDestroy()
            {
                OnDestroy_isClient = isClient;
                OnDestroy_isServer = isServer;
                OnDestroy_isLocalPlayer = isLocalPlayer;
            }
        }

        GameObject gameObject;
        NetworkIdentity identity;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(gameObject);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void OnStartServerTest()
        {
            // lets add a component to check OnStartserver
            MyTestComponent component1 = gameObject.AddComponent<MyTestComponent>();
            MyTestComponent component2 = gameObject.AddComponent<MyTestComponent>();

            identity.OnStartServer();

            Assert.That(component1.onStartServerInvoked);
            Assert.That(component2.onStartServerInvoked);
        }

        // check isClient/isServer/isLocalPlayer in server-only mode
        [Test]
        public void ServerMode_IsFlags_Test()
        {
            // start the server
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1000);

            // add component
            IsClientServerCheckComponent component = gameObject.AddComponent<IsClientServerCheckComponent>();

            // spawn it
            NetworkServer.Spawn(gameObject);

            // OnStartServer should have been called. check the flags.
            Assert.That(component.OnStartServer_isClient, Is.EqualTo(false));
            Assert.That(component.OnStartServer_isLocalPlayer, Is.EqualTo(false));
            Assert.That(component.OnStartServer_isServer, Is.EqualTo(true));

            // stop the server
            NetworkServer.Shutdown();
            Transport.activeTransport = null;

            // clean up
            NetworkIdentity.spawned.Clear();
        }

        // check isClient/isServer/isLocalPlayer in host mode
        [Test]
        public void HostMode_IsFlags_Test()
        {
            // start the server
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1000);

            // start the client
            NetworkClient.ConnectHost();

            // add component
            IsClientServerCheckComponent component = gameObject.AddComponent<IsClientServerCheckComponent>();

            // set is as local player
            ClientScene.InternalAddPlayer(identity);

            // spawn it
            NetworkServer.Spawn(gameObject);

            // OnStartServer should have been called. check the flags.
            Assert.That(component.OnStartServer_isClient, Is.EqualTo(true));
            Assert.That(component.OnStartServer_isLocalPlayer, Is.EqualTo(true));
            Assert.That(component.OnStartServer_isServer, Is.EqualTo(true));

            // stop the client
            NetworkClient.Shutdown();
            NetworkServer.RemoveLocalConnection();
            ClientScene.Shutdown();

            // stop the server
            NetworkServer.Shutdown();
            Transport.activeTransport = null;

            // clean up
            NetworkIdentity.spawned.Clear();
        }

        [Test]
        public void GetSetAssetId()
        {
            // assign a guid
            Guid guid = new Guid(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
            identity.assetId = guid;

            // did it work?
            Assert.That(identity.assetId, Is.EqualTo(guid));
        }

        [Test]
        public void SetClientOwner()
        {
            // SetClientOwner
            ULocalConnectionToClient original = new ULocalConnectionToClient();
            identity.SetClientOwner(original);
            Assert.That(identity.connectionToClient, Is.EqualTo(original));

            // setting it when it's already set shouldn't overwrite the original
            ULocalConnectionToClient overwrite = new ULocalConnectionToClient();
            LogAssert.ignoreFailingMessages = true; // will log a warning
            identity.SetClientOwner(overwrite);
            Assert.That(identity.connectionToClient, Is.EqualTo(original));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void RemoveObserverInternal()
        {
            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // add an observer connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            identity.observers[connection.connectionId] = connection;

            // RemoveObserverInternal with invalid connection should do nothing
            identity.RemoveObserverInternal(new NetworkConnectionToClient(43));
            Assert.That(identity.observers.Count, Is.EqualTo(1));

            // RemoveObserverInternal with existing connection should remove it
            identity.RemoveObserverInternal(connection);
            Assert.That(identity.observers.Count, Is.EqualTo(0));
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
            Assert.That(identity.assetId, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void OnStartServerCallsComponentsAndCatchesExceptions()
        {
            // add component
            StartServerExceptionNetworkBehaviour comp = gameObject.AddComponent<StartServerExceptionNetworkBehaviour>();

            // make sure that comp.OnStartServer was called and make sure that
            // the exception was caught and not thrown in here.
            // an exception in OnStartServer should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            identity.OnStartServer(); // should catch the exception internally and not throw it
            Assert.That(comp.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void OnStartClientCallsComponentsAndCatchesExceptions()
        {
            // add component
            StartClientExceptionNetworkBehaviour comp = gameObject.AddComponent<StartClientExceptionNetworkBehaviour>();

            // make sure that comp.OnStartClient was called and make sure that
            // the exception was caught and not thrown in here.
            // an exception in OnStartClient should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            identity.OnStartClient(); // should catch the exception internally and not throw it
            Assert.That(comp.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            identity.OnStartClient();
            Assert.That(comp.called, Is.EqualTo(1)); // same as before?
        }

        [Test]
        public void OnStartAuthorityCallsComponentsAndCatchesExceptions()
        {
            // add component
            StartAuthorityExceptionNetworkBehaviour comp = gameObject.AddComponent<StartAuthorityExceptionNetworkBehaviour>();

            // make sure that comp.OnStartAuthority was called and make sure that
            // the exception was caught and not thrown in here.
            // an exception in OnStartAuthority should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            identity.OnStartAuthority(); // should catch the exception internally and not throw it
            Assert.That(comp.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void OnStopAuthorityCallsComponentsAndCatchesExceptions()
        {
            // add component
            StopAuthorityExceptionNetworkBehaviour comp = gameObject.AddComponent<StopAuthorityExceptionNetworkBehaviour>();

            // make sure that comp.OnStopAuthority was called and make sure that
            // the exception was caught and not thrown in here.
            // an exception in OnStopAuthority should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            identity.OnStopAuthority(); // should catch the exception internally and not throw it
            Assert.That(comp.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AssignAndRemoveClientAuthority()
        {
            // netId is needed for isServer to be true
            identity.netId = 42;

            // test the callback too
            int callbackCalled = 0;
            NetworkConnection callbackConnection = null;
            NetworkIdentity callbackIdentity = null;
            bool callbackState = false;
            NetworkIdentity.clientAuthorityCallback += (conn, networkIdentity, state) => {
                ++callbackCalled;
                callbackConnection = conn;
                callbackIdentity = identity;
                callbackState = state;
            };

            // create a connection
            ULocalConnectionToClient owner = new ULocalConnectionToClient();
            owner.isReady = true;
            // add client handlers
            owner.connectionToServer = new ULocalConnectionToServer();
            int spawnCalled = 0;
            owner.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>{
                { MessagePacker.GetId<SpawnMessage>(), (msg => ++spawnCalled) }
            });

            // assigning authority should only work on server.
            // if isServer is false because server isn't running yet then it
            // should fail.
            LogAssert.ignoreFailingMessages = true; // error log is expected
            bool result = identity.AssignClientAuthority(owner);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(result, Is.False);

            // we can only handle authority on the server.
            // start the server so that isServer is true.
            Transport.activeTransport = Substitute.For<Transport>(); // needed in .Listen
            NetworkServer.Listen(1);
            Assert.That(identity.isServer, Is.True);

            // assign authority
            result = identity.AssignClientAuthority(owner);
            Assert.That(result, Is.True);
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));
            Assert.That(callbackConnection, Is.EqualTo(owner));
            Assert.That(callbackIdentity, Is.EqualTo(identity));
            Assert.That(callbackState, Is.EqualTo(true));

            // assigning authority should respawn the object with proper authority
            // on the client. that's the best way to sync the new state right now.
            owner.connectionToServer.Update(); // process pending messages
            Assert.That(spawnCalled, Is.EqualTo(1));

            // shouldn't be able to assign authority while already owned by
            // another connection
            LogAssert.ignoreFailingMessages = true; // error log is expected
            result = identity.AssignClientAuthority(new NetworkConnectionToClient(43));
            LogAssert.ignoreFailingMessages = false;
            Assert.That(result, Is.False);
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));

            // someone might try to remove authority by assigning null.
            // make sure this fails.
            LogAssert.ignoreFailingMessages = true; // error log is expected
            result = identity.AssignClientAuthority(null);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(result, Is.False);

            // removing authority while not isServer shouldn't work.
            // only allow it on server.
            NetworkServer.Shutdown();
            LogAssert.ignoreFailingMessages = true; // error log is expected
            identity.RemoveClientAuthority();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));
            NetworkServer.Listen(1); // restart it gain

            // removing authority for the main player object shouldn't work
            owner.identity = identity; // set connection's player object
            LogAssert.ignoreFailingMessages = true; // error log is expected
            identity.RemoveClientAuthority();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));

            // removing authority for a non-main-player object should work
            owner.identity = null;
            identity.RemoveClientAuthority();
            Assert.That(identity.connectionToClient, Is.Null);
            Assert.That(callbackCalled, Is.EqualTo(2));
            Assert.That(callbackConnection, Is.EqualTo(owner)); // the one that was removed
            Assert.That(callbackIdentity, Is.EqualTo(identity));
            Assert.That(callbackState, Is.EqualTo(false));

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        [Test]
        public void NotifyAuthorityCallsOnStartStopAuthority()
        {
            // add components
            StartAuthorityCalledNetworkBehaviour compStart = gameObject.AddComponent<StartAuthorityCalledNetworkBehaviour>();
            StopAuthorityCalledNetworkBehaviour compStop = gameObject.AddComponent<StopAuthorityCalledNetworkBehaviour>();

            // set authority from false to true, which should call OnStartAuthority
            identity.hasAuthority = true;
            identity.NotifyAuthority();
            Assert.That(identity.hasAuthority, Is.True); // shouldn't be touched
            Assert.That(compStart.called, Is.EqualTo(1)); // start should be called
            Assert.That(compStop.called, Is.EqualTo(0)); // stop shouldn't

            // set it to true again, should do nothing because already true
            identity.hasAuthority = true;
            identity.NotifyAuthority();
            Assert.That(identity.hasAuthority, Is.True); // shouldn't be touched
            Assert.That(compStart.called, Is.EqualTo(1)); // same as before
            Assert.That(compStop.called, Is.EqualTo(0)); // same as before

            // set it to false, should call OnStopAuthority
            identity.hasAuthority = false;
            identity.NotifyAuthority();
            Assert.That(identity.hasAuthority, Is.False); // shouldn't be touched
            Assert.That(compStart.called, Is.EqualTo(1)); // same as before
            Assert.That(compStop.called, Is.EqualTo(1)); // stop should be called

            // set it to false again, should do nothing because already false
            identity.hasAuthority = false;
            identity.NotifyAuthority();
            Assert.That(identity.hasAuthority, Is.False); // shouldn't be touched
            Assert.That(compStart.called, Is.EqualTo(1)); // same as before
            Assert.That(compStop.called, Is.EqualTo(1)); // same as before
        }

        [Test]
        public void OnSetHostVisibilityCallsComponentsAndCatchesExceptions()
        {
            // add component
            SetHostVisibilityExceptionNetworkBehaviour comp = gameObject.AddComponent<SetHostVisibilityExceptionNetworkBehaviour>();

            // make sure that comp.OnSetHostVisibility was called and make sure that
            // the exception was caught and not thrown in here.
            // an exception in OnSetHostVisibility should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;

            identity.OnSetHostVisibility(true); // should catch the exception internally and not throw it
            Assert.That(comp.called, Is.EqualTo(1));
            Assert.That(comp.valuePassed, Is.True);

            identity.OnSetHostVisibility(false); // should catch the exception internally and not throw it
            Assert.That(comp.called, Is.EqualTo(2));
            Assert.That(comp.valuePassed, Is.False);

            LogAssert.ignoreFailingMessages = false;
        }

        // OnStartServer in host mode should set isClient=true
        [Test]
        public void OnStartServerInHostModeSetsIsClientTrue()
        {
            // setup a transport so that Connect doesn't get NullRefException
            // -> needs to be on a GameObject because Connect calls .enabled=true,
            //    which only works if it's on a gameobject
            GameObject transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

            // call client connect so that internals are set up
            // (it won't actually successfully connect)
            // -> also set up connectmessage handler to avoid unhandled msg error
            NetworkClient.RegisterHandler<ConnectMessage>(msg => {}, false);
            NetworkClient.Connect("localhost");

            // manually invoke transport.OnConnected so that NetworkClient.active is set to true
            Transport.activeTransport.OnClientConnected.Invoke();
            Assert.That(NetworkClient.active, Is.True);

            // isClient needs to be true in OnStartServer if in host mode.
            // this is a test for a bug that we fixed, where isClient was false
            // in OnStartServer if in host mode because in host mode, we only
            // connect the client after starting the server, hence isClient would
            // be false in OnStartServer until way later.
            // -> we have the workaround in OnStartServer, so let's also test to
            //    make sure that nobody ever breaks it again
            Assert.That(identity.isClient, Is.False);
            identity.OnStartServer();
            Assert.That(identity.isClient, Is.True);

            // clean up
            NetworkClient.Disconnect();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void OnCheckObserver()
        {
            // add component
            CheckObserverExceptionNetworkBehaviour compExc = gameObject.AddComponent<CheckObserverExceptionNetworkBehaviour>();

            NetworkConnection connection = new NetworkConnectionToClient(42);

            // an exception in OnCheckObserver should be caught, so that one
            // component's exception doesn't stop all other components from
            // being checked
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            bool result = identity.OnCheckObserver(connection); // should catch the exception internally and not throw it
            Assert.That(result, Is.True);
            Assert.That(compExc.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;

            // let's also make sure that the correct connection was passed, just
            // to be sure
            Assert.That(compExc.valuePassed, Is.EqualTo(connection));

            // create a networkidentity with a component that returns true
            // result should still be true.
            GameObject gameObjectTrue = new GameObject();
            NetworkIdentity identityTrue = gameObjectTrue.AddComponent<NetworkIdentity>();
            CheckObserverTrueNetworkBehaviour compTrue = gameObjectTrue.AddComponent<CheckObserverTrueNetworkBehaviour>();
            result = identityTrue.OnCheckObserver(connection);
            Assert.That(result, Is.True);
            Assert.That(compTrue.called, Is.EqualTo(1));

            // create a networkidentity with a component that returns true and
            // one component that returns false.
            // result should still be false if any one returns false.
            GameObject gameObjectFalse = new GameObject();
            NetworkIdentity identityFalse = gameObjectFalse.AddComponent<NetworkIdentity>();
            compTrue = gameObjectFalse.AddComponent<CheckObserverTrueNetworkBehaviour>();
            CheckObserverFalseNetworkBehaviour compFalse = gameObjectFalse.AddComponent<CheckObserverFalseNetworkBehaviour>();
            result = identityFalse.OnCheckObserver(connection);
            Assert.That(result, Is.False);
            Assert.That(compTrue.called, Is.EqualTo(1));
            Assert.That(compFalse.called, Is.EqualTo(1));

            // clean up
            GameObject.DestroyImmediate(gameObjectFalse);
            GameObject.DestroyImmediate(gameObjectTrue);
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

            // serialize all - should work even if compExc throws an exception
            NetworkWriter ownerWriter = new NetworkWriter();
            NetworkWriter observersWriter = new NetworkWriter();
            LogAssert.ignoreFailingMessages = true; // error log because of the exception is expected
            identity.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);
            LogAssert.ignoreFailingMessages = false;

            // owner should have written all components
            Assert.That(ownerWritten, Is.EqualTo(3));

            // observers should have written only the observers components
            Assert.That(observersWritten, Is.EqualTo(2));

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for owner - should work even if compExc throws an exception
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            LogAssert.ignoreFailingMessages = true; // error log because of the exception is expected
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp1.value, Is.EqualTo(12345));
            Assert.That(comp2.value, Is.EqualTo("67890"));

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for observers - should work even if compExc throws an exception
            reader = new NetworkReader(observersWriter.ToArray());
            LogAssert.ignoreFailingMessages = true; // error log because of the exception is expected
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp1.value, Is.EqualTo(12345)); // observers mode, should be in data
            Assert.That(comp2.value, Is.EqualTo(null)); // owner mode, should not be in data
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void OnSerializeAllSafelyShouldDetectTooManyComponents()
        {
            // add 65 components
            for (int i = 0; i < 65; ++i)
            {
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            }

            // try to serialize
            NetworkWriter ownerWriter = new NetworkWriter();
            NetworkWriter observersWriter = new NetworkWriter();
            LogAssert.ignoreFailingMessages = true; // error log is expected because of too many components
            identity.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);
            LogAssert.ignoreFailingMessages = false;

            // shouldn't have written anything because too many components
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
            Assert.That(observersWriter.Position, Is.EqualTo(0));
            Assert.That(ownerWritten, Is.EqualTo(0));
            Assert.That(observersWritten, Is.EqualTo(0));
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
            NetworkWriter ownerWriter = new NetworkWriter();
            NetworkWriter observersWriter = new NetworkWriter();
            identity.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            LogAssert.ignoreFailingMessages = true; // warning log because of serialization mismatch
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;

            // the mismatch component will fail, but the one before and after
            // should still work fine. that's the whole point.
            Assert.That(comp1.value, Is.EqualTo(12345));
            Assert.That(comp2.value, Is.EqualTo("67890"));
        }

        [Test]
        public void OnStartLocalPlayer()
        {
            // add components
            StartLocalPlayerExceptionNetworkBehaviour compEx = gameObject.AddComponent<StartLocalPlayerExceptionNetworkBehaviour>();
            StartLocalPlayerCalledNetworkBehaviour comp = gameObject.AddComponent<StartLocalPlayerCalledNetworkBehaviour>();

            // make sure our test values are set to 0
            Assert.That(compEx.called, Is.EqualTo(0));
            Assert.That(comp.called, Is.EqualTo(0));

            // call OnStartLocalPlayer in identity
            // one component will throw an exception, but that shouldn't stop
            // OnStartLocalPlayer from being called in the second one
            LogAssert.ignoreFailingMessages = true; // exception will log an error
            identity.OnStartLocalPlayer();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(compEx.called, Is.EqualTo(1));
            Assert.That(comp.called, Is.EqualTo(1));

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            identity.OnStartLocalPlayer();
            Assert.That(compEx.called, Is.EqualTo(1)); // same as before?
            Assert.That(comp.called, Is.EqualTo(1)); // same as before?
        }

        [Test]
        public void OnNetworkDestroy()
        {
            // add components
            NetworkDestroyExceptionNetworkBehaviour compEx = gameObject.AddComponent<NetworkDestroyExceptionNetworkBehaviour>();
            NetworkDestroyCalledNetworkBehaviour comp = gameObject.AddComponent<NetworkDestroyCalledNetworkBehaviour>();

            // make sure our test values are set to 0
            Assert.That(compEx.called, Is.EqualTo(0));
            Assert.That(comp.called, Is.EqualTo(0));

            // call OnNetworkDestroy in identity
            // one component will throw an exception, but that shouldn't stop
            // OnNetworkDestroy from being called in the second one
            LogAssert.ignoreFailingMessages = true; // exception will log an error
            identity.OnNetworkDestroy();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(compEx.called, Is.EqualTo(1));
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void AddObserver()
        {
            // create some connections
            NetworkConnectionToClient connection1 = new NetworkConnectionToClient(42);
            NetworkConnectionToClient connection2 = new NetworkConnectionToClient(43);

            // AddObserver should return early if called before .observers was
            // created
            Assert.That(identity.observers, Is.Null);
            LogAssert.ignoreFailingMessages = true; // error log is expected
            identity.AddObserver(connection1);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(identity.observers, Is.Null);

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // call AddObservers
            identity.AddObserver(connection1);
            identity.AddObserver(connection2);
            Assert.That(identity.observers.Count, Is.EqualTo(2));
            Assert.That(identity.observers.ContainsKey(connection1.connectionId));
            Assert.That(identity.observers[connection1.connectionId], Is.EqualTo(connection1));
            Assert.That(identity.observers.ContainsKey(connection2.connectionId));
            Assert.That(identity.observers[connection2.connectionId], Is.EqualTo(connection2));

            // adding a duplicate connectionId shouldn't overwrite the original
            NetworkConnectionToClient duplicate = new NetworkConnectionToClient(connection1.connectionId);
            identity.AddObserver(duplicate);
            Assert.That(identity.observers.Count, Is.EqualTo(2));
            Assert.That(identity.observers.ContainsKey(connection1.connectionId));
            Assert.That(identity.observers[connection1.connectionId], Is.EqualTo(connection1));
            Assert.That(identity.observers.ContainsKey(connection2.connectionId));
            Assert.That(identity.observers[connection2.connectionId], Is.EqualTo(connection2));
        }

        [Test]
        public void ClearObservers()
        {
            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // add some observers
            identity.observers[42] = new NetworkConnectionToClient(42);
            identity.observers[43] = new NetworkConnectionToClient(43);

            // call ClearObservers
            identity.ClearObservers();
            Assert.That(identity.observers.Count, Is.EqualTo(0));
        }

        [Test]
        public void ClearDirtyComponentsDirtyBits()
        {
            // add components
            OnStartClientTestNetworkBehaviour compA = gameObject.AddComponent<OnStartClientTestNetworkBehaviour>();
            OnStartClientTestNetworkBehaviour compB = gameObject.AddComponent<OnStartClientTestNetworkBehaviour>();

            // set syncintervals so one is always dirty, one is never dirty
            compA.syncInterval = 0;
            compB.syncInterval = Mathf.Infinity;

            // set components dirty bits
            compA.SetDirtyBit(0x0001);
            compB.SetDirtyBit(0x1001);
            Assert.That(compA.IsDirty(), Is.True); // dirty because interval reached and mask != 0
            Assert.That(compB.IsDirty(), Is.False); // not dirty because syncinterval not reached

            // call identity.ClearDirtyComponentsDirtyBits
            identity.ClearDirtyComponentsDirtyBits();
            Assert.That(compA.IsDirty(), Is.False); // should be cleared now
            Assert.That(compB.IsDirty(), Is.False); // should be untouched

            // set compB syncinterval to 0 to check if the masks were untouched
            // (if they weren't, then it should be dirty now)
            compB.syncInterval = 0;
            Assert.That(compB.IsDirty(), Is.True);
        }

        [Test]
        public void ClearAllComponentsDirtyBits()
        {
            // add components
            OnStartClientTestNetworkBehaviour compA = gameObject.AddComponent<OnStartClientTestNetworkBehaviour>();
            OnStartClientTestNetworkBehaviour compB = gameObject.AddComponent<OnStartClientTestNetworkBehaviour>();

            // set syncintervals so one is always dirty, one is never dirty
            compA.syncInterval = 0;
            compB.syncInterval = Mathf.Infinity;

            // set components dirty bits
            compA.SetDirtyBit(0x0001);
            compB.SetDirtyBit(0x1001);
            Assert.That(compA.IsDirty(), Is.True); // dirty because interval reached and mask != 0
            Assert.That(compB.IsDirty(), Is.False); // not dirty because syncinterval not reached

            // call identity.ClearAllComponentsDirtyBits
            identity.ClearAllComponentsDirtyBits();
            Assert.That(compA.IsDirty(), Is.False); // should be cleared now
            Assert.That(compB.IsDirty(), Is.False); // should be cleared now

            // set compB syncinterval to 0 to check if the masks were cleared
            // (if they weren't, then it would still be dirty now)
            compB.syncInterval = 0;
            Assert.That(compB.IsDirty(), Is.False);
        }

        [Test]
        public void Reset()
        {
            // modify it a bit
            identity.isClient = true;
            identity.OnStartServer(); // creates .observers and generates a netId
            uint netId = identity.netId;
            identity.connectionToClient = new NetworkConnectionToClient(1);
            identity.connectionToServer = new NetworkConnectionToServer();
            identity.observers[43] = new NetworkConnectionToClient(2);

            // calling reset shouldn't do anything unless it was marked for reset
            identity.Reset();
            Assert.That(identity.isClient, Is.True);
            Assert.That(identity.netId, Is.EqualTo(netId));
            Assert.That(identity.connectionToClient, !Is.Null);
            Assert.That(identity.connectionToServer, !Is.Null);

            // mark for reset and reset
            identity.MarkForReset();
            identity.Reset();
            Assert.That(identity.isClient, Is.False);
            Assert.That(identity.netId, Is.EqualTo(0));
            Assert.That(identity.connectionToClient, Is.Null);
            Assert.That(identity.connectionToServer, Is.Null);
        }

        [Test]
        public void HandleCommand()
        {
            // add component
            CommandTestNetworkBehaviour comp0 = gameObject.AddComponent<CommandTestNetworkBehaviour>();
            Assert.That(comp0.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterCommandDelegate(typeof(CommandTestNetworkBehaviour), nameof(CommandTestNetworkBehaviour.CommandGenerated), CommandTestNetworkBehaviour.CommandGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call HandleCommand and check if the command was called in the component
            int functionHash = NetworkBehaviour.GetMethodHash(typeof(CommandTestNetworkBehaviour), nameof(CommandTestNetworkBehaviour.CommandGenerated));
            NetworkReader payload = new NetworkReader(new byte[0]);
            identity.HandleCommand(0, functionHash, payload);
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong component index. command shouldn't be called again.
            LogAssert.ignoreFailingMessages = true; // warning is expected
            identity.HandleCommand(1, functionHash, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong function hash. command shouldn't be called again.
            LogAssert.ignoreFailingMessages = true; // warning is expected
            identity.HandleCommand(0, functionHash+1, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
            NetworkIdentity.spawned.Clear();
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void HandleRpc()
        {
            // add rpc component
            RpcTestNetworkBehaviour comp0 = gameObject.AddComponent<RpcTestNetworkBehaviour>();
            Assert.That(comp0.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterRpcDelegate(typeof(RpcTestNetworkBehaviour), nameof(RpcTestNetworkBehaviour.RpcGenerated), RpcTestNetworkBehaviour.RpcGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call HandleRpc and check if the rpc was called in the component
            int functionHash = NetworkBehaviour.GetMethodHash(typeof(RpcTestNetworkBehaviour), nameof(RpcTestNetworkBehaviour.RpcGenerated));
            NetworkReader payload = new NetworkReader(new byte[0]);
            identity.HandleRPC(0, functionHash, payload);
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong component index. rpc shouldn't be called again.
            LogAssert.ignoreFailingMessages = true; // warning is expected
            identity.HandleRPC(1, functionHash, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong function hash. rpc shouldn't be called again.
            LogAssert.ignoreFailingMessages = true; // warning is expected
            identity.HandleRPC(0, functionHash+1, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void HandleSyncEvent()
        {
            // add syncevent component
            SyncEventTestNetworkBehaviour comp0 = gameObject.AddComponent<SyncEventTestNetworkBehaviour>();
            Assert.That(comp0.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterEventDelegate(typeof(SyncEventTestNetworkBehaviour), nameof(SyncEventTestNetworkBehaviour.SyncEventGenerated), SyncEventTestNetworkBehaviour.SyncEventGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call HandleSyncEvent and check if the event was called in the component
            int componentIndex = 0;
            int functionHash = NetworkBehaviour.GetMethodHash(typeof(SyncEventTestNetworkBehaviour), nameof(SyncEventTestNetworkBehaviour.SyncEventGenerated));
            NetworkReader payload = new NetworkReader(new byte[0]);
            identity.HandleSyncEvent(componentIndex, functionHash, payload);
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong component index. syncevent shouldn't be called again.
            LogAssert.ignoreFailingMessages = true; // warning is expected
            identity.HandleSyncEvent(1, functionHash, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong function hash. syncevent shouldn't be called again.
            LogAssert.ignoreFailingMessages = true; // warning is expected
            identity.HandleSyncEvent(0, functionHash+1, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void ServerUpdate()
        {
            // add components
            SerializeTest1NetworkBehaviour compA = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            compA.value = 1337; // test value
            compA.syncInterval = 0; // set syncInterval so IsDirty passes the interval check
            compA.syncMode = SyncMode.Owner; // one needs to sync to owner
            SerializeTest2NetworkBehaviour compB = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();
            compB.value = "test"; // test value
            compB.syncInterval = 0; // set syncInterval so IsDirty passes the interval check
            compB.syncMode = SyncMode.Observers; // one needs to sync to owner

            // call OnStartServer once so observers are created
            identity.OnStartServer();

            // set it dirty
            compA.SetDirtyBit(ulong.MaxValue);
            compB.SetDirtyBit(ulong.MaxValue);
            Assert.That(compA.IsDirty(), Is.True);
            Assert.That(compB.IsDirty(), Is.True);

            // calling update without observers should clear all dirty bits.
            // it would be spawned on new observers anyway.
            identity.ServerUpdate();
            Assert.That(compA.IsDirty(), Is.False);
            Assert.That(compB.IsDirty(), Is.False);

            // add an owner connection that will receive the updates
            ULocalConnectionToClient owner = new ULocalConnectionToClient();
            owner.isReady = true; // for syncing
            // add a client to server connection + handler to receive syncs
            owner.connectionToServer = new ULocalConnectionToServer();
            int ownerCalled = 0;
            owner.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>
            {
                { MessagePacker.GetId<UpdateVarsMessage>(), (msg => ++ownerCalled) }
            });
            identity.connectionToClient = owner;

            // add an observer connection that will receive the updates
            ULocalConnectionToClient observer = new ULocalConnectionToClient();
            observer.isReady = true; // we only sync to ready observers
            // add a client to server connection + handler to receive syncs
            observer.connectionToServer = new ULocalConnectionToServer();
            int observerCalled = 0;
            observer.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>
            {
                { MessagePacker.GetId<UpdateVarsMessage>(), (msg => ++observerCalled) }
            });
            identity.observers[observer.connectionId] = observer;

            // set components dirty again
            compA.SetDirtyBit(ulong.MaxValue);
            compB.SetDirtyBit(ulong.MaxValue);

            // calling update should serialize all components and send them to
            // owner/observers
            identity.ServerUpdate();

            // update connections once so that messages are processed
            owner.connectionToServer.Update();
            observer.connectionToServer.Update();

            // was it received on the clients?
            Assert.That(ownerCalled, Is.EqualTo(1));
            Assert.That(observerCalled, Is.EqualTo(1));
        }

        [Test]
        public void GetNewObservers()
        {
            // add components
            RebuildObserversNetworkBehaviour compA = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compA.observer = new NetworkConnectionToClient(12);
            RebuildObserversNetworkBehaviour compB = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compB.observer = new NetworkConnectionToClient(13);

            // get new observers
            HashSet<NetworkConnection> observers = new HashSet<NetworkConnection>();
            bool result = identity.GetNewObservers(observers, true);
            Assert.That(result, Is.True);
            Assert.That(observers.Count, Is.EqualTo(2));
            Assert.That(observers.Contains(compA.observer), Is.True);
            Assert.That(observers.Contains(compB.observer), Is.True);
        }

        [Test]
        public void GetNewObserversClearsHashSet()
        {
            // get new observers. no observer components so it should just clear
            // it and not do anything else
            HashSet<NetworkConnection> observers = new HashSet<NetworkConnection>();
            observers.Add(new NetworkConnectionToClient(42));
            identity.GetNewObservers(observers, true);
            Assert.That(observers.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetNewObserversFalseIfNoComponents()
        {
            // get new observers. no observer components so it should be false
            HashSet<NetworkConnection> observers = new HashSet<NetworkConnection>();
            bool result = identity.GetNewObservers(observers, true);
            Assert.That(result, Is.False);
        }

        [Test]
        public void AddAllReadyServerConnectionsToObservers()
        {
            // AddObserver will call transport.send and validpacketsize, so we
            // actually need a transport
            Transport.activeTransport = new MemoryTransport();

            // add some server connections
            NetworkServer.connections[12] = new NetworkConnectionToClient(12){isReady = true};
            NetworkServer.connections[13] = new NetworkConnectionToClient(13){isReady = false};

            // add a host connection
            ULocalConnectionToClient localConnection = new ULocalConnectionToClient();
            localConnection.connectionToServer = new ULocalConnectionToServer();
            localConnection.isReady = true;
            NetworkServer.SetLocalConnection(localConnection);

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // add all to observers. should have the two ready connections then.
            identity.AddAllReadyServerConnectionsToObservers();
            Assert.That(identity.observers.Count, Is.EqualTo(2));
            Assert.That(identity.observers.ContainsKey(12));
            Assert.That(identity.observers.ContainsKey(NetworkServer.localConnection.connectionId));

            // clean up
            NetworkServer.RemoveLocalConnection();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
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
            ULocalConnectionToClient connection = new ULocalConnectionToClient();
            connection.connectionToServer = new ULocalConnectionToServer();
            connection.isReady = true;
            identity.connectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild should at least add own ready player
            identity.RebuildObservers(true);
            Assert.That(identity.observers.ContainsKey(identity.connectionToClient.connectionId));
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
            ULocalConnectionToClient connection = new ULocalConnectionToClient();
            connection.connectionToServer = new ULocalConnectionToServer();
            identity.connectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild shouldn't add own player because conn wasn't set ready
            identity.RebuildObservers(true);
            Assert.That(!identity.observers.ContainsKey(identity.connectionToClient.connectionId));
        }

        [Test]
        public void RebuildObserversAddsReadyComponentConnectionsIfImplemented()
        {
            // AddObserver will call transport.send and validpacketsize, so we
            // actually need a transport
            Transport.activeTransport = new MemoryTransport();

            // add three observers components
            // one with a ready connection, one with no ready connection, one with null connection
            RebuildObserversNetworkBehaviour compA = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compA.observer = null;
            RebuildObserversNetworkBehaviour compB = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compB.observer = new NetworkConnectionToClient(42){ isReady = false };
            RebuildObserversNetworkBehaviour compC = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compC.observer = new NetworkConnectionToClient(43){ isReady = true };

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild observers should add all component's ready observers
            identity.RebuildObservers(true);
            Assert.That(identity.observers.Count, Is.EqualTo(1));
            Assert.That(identity.observers.ContainsKey(43));

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        [Test]
        public void RebuildObserversAddsReadyServerConnectionsIfNotImplemented()
        {
            // AddObserver will call transport.send and validpacketsize, so we
            // actually need a transport
            Transport.activeTransport = new MemoryTransport();

            // add some server connections
            NetworkServer.connections[12] = new NetworkConnectionToClient(12){isReady = true};
            NetworkServer.connections[13] = new NetworkConnectionToClient(13){isReady = false};

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild observers should add all ready server connections
            // because no component implements OnRebuildObservers
            identity.RebuildObservers(true);
            Assert.That(identity.observers.Count, Is.EqualTo(1));
            Assert.That(identity.observers.ContainsKey(12));

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        [Test]
        public void RebuildObserversDoesNotAddServerConnectionsIfImplemented()
        {
            // AddObserver will call transport.send and validpacketsize, so we
            // actually need a transport
            Transport.activeTransport = new MemoryTransport();

            // add a server connection
            NetworkServer.connections[12] = new NetworkConnectionToClient(12){isReady = true};

            // add at least one observers component, otherwise it will just add
            // all server connections
            gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild observers should NOT add all server connections now
            identity.RebuildObservers(true);
            Assert.That(identity.observers.Count, Is.EqualTo(0));

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        // RebuildObservers is complex. let's do one full test where we check
        // add, remove and vislist.
        [Test]
        public void RebuildObserversAddRemoveAndVisListTest()
        {
            // AddObserver will call transport.send and validpacketsize, so we
            // actually need a transport
            Transport.activeTransport = new MemoryTransport();

            // add observer component with ready observer
            RebuildObserversNetworkBehaviour comp = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            NetworkConnectionToClient observerA = new NetworkConnectionToClient(42){ isReady = true };
            comp.observer = observerA;

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild observers should add that one observer
            identity.RebuildObservers(true);
            Assert.That(identity.observers.Count, Is.EqualTo(1));
            Assert.That(identity.observers.ContainsKey(observerA.connectionId));

            // identity should have added itself to the observer's visList
            Assert.That(observerA.visList.Count, Is.EqualTo(1));
            Assert.That(observerA.visList.Contains(identity), Is.True);

            // let the component find another observer
            NetworkConnectionToClient observerB = new NetworkConnectionToClient(43){ isReady = true };
            comp.observer = observerB;

            // rebuild observers should remove the old observer and add the new one
            identity.RebuildObservers(true);
            Assert.That(identity.observers.Count, Is.EqualTo(1));
            Assert.That(identity.observers.ContainsKey(observerB.connectionId));

            // identity should have removed itself from the old observer's visList
            // and added itself to new observer's vislist
            Assert.That(observerA.visList.Count, Is.EqualTo(0));
            Assert.That(observerB.visList.Count, Is.EqualTo(1));
            Assert.That(observerB.visList.Contains(identity), Is.True);

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        [Test]
        public void RebuildObserversSetsHostVisibility()
        {
            // set local connection for host mode
            ULocalConnectionToClient localConnection = new ULocalConnectionToClient();
            localConnection.connectionToServer = new ULocalConnectionToServer();
            localConnection.isReady = true;
            NetworkServer.SetLocalConnection(localConnection);

            // add at least one observers component, otherwise it will just add
            // all server connections
            RebuildEmptyObserversNetworkBehaviour comp = gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();
            Assert.That(comp.hostVisibilityCalled, Is.EqualTo(0));

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // rebuild will result in 0 observers. it won't contain host
            // connection so it should call OnSetHostVisibility(false)
            identity.RebuildObservers(true);
            Assert.That(identity.observers.Count, Is.EqualTo(0));
            Assert.That(comp.hostVisibilityCalled, Is.EqualTo(1));
            Assert.That(comp.hostVisibilityValue, Is.False);

            // clean up
            NetworkServer.RemoveLocalConnection();
            NetworkServer.Shutdown();
        }

        [Test]
        public void RebuildObserversReturnsIfNull()
        {
            // add a server connection
            NetworkServer.connections[12] = new NetworkConnectionToClient(12){isReady = true};

            // call RebuildObservers without calling OnStartServer first.
            // .observers will be null and it should simply return early.
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Is.Null);
        }
    }
}
