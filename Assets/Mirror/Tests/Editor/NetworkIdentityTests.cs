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

        // A Test behaves as an ordinary method
        [Test]
        public void OnStartServerTest()
        {
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

            // lets add a component to check OnStartserver

            MyTestComponent component1 = gameObject.AddComponent<MyTestComponent>();
            MyTestComponent component2 = gameObject.AddComponent<MyTestComponent>();

            identity.OnStartServer();

            Assert.That(component1.onStartServerInvoked);
            Assert.That(component2.onStartServerInvoked);
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

        // check isClient/isServer/isLocalPlayer in server-only mode
        [Test]
        public void ServerMode_IsFlags_Test()
        {
            // start the server
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1000);

            // create a networkidentity+component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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
            GameObject.DestroyImmediate(gameObject);
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

            // create a networkidentity+component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void GetSetAssetId()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

            // assign a guid
            Guid guid = new Guid(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
            identity.assetId = guid;

            // did it work?
            Assert.That(identity.assetId, Is.EqualTo(guid));

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void SetClientOwner()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void RemoveObserverInternal()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void AssignSceneID()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

            // Awake will have assigned a random sceneId of format 0x00000000FFFFFFFF
            // -> make sure that one was assigned, and that the left part was
            //    left empty for scene hash
            Assert.That(identity.sceneId, !Is.Zero);
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, Is.EqualTo(0x0000000000000000));

            // make sure that Awake added it to sceneIds dict
            Assert.That(NetworkIdentity.GetSceneIdentity(identity.sceneId), !Is.Null);

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void SetSceneIdSceneHashPartInternal()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnValidateSetupIDsSetsEmptyAssetIDForSceneObject()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

            // OnValidate will have been called. make sure that assetId was set
            // to 0 empty and not anything else, because this is a scene object
            Assert.That(identity.assetId, Is.EqualTo(Guid.Empty));

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnStartServerCallsComponentsAndCatchesExceptions()
        {
            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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


            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnStartClientCallsComponentsAndCatchesExceptions()
        {
            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnStartAuthorityCallsComponentsAndCatchesExceptions()
        {
            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnStopAuthorityCallsComponentsAndCatchesExceptions()
        {
            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void NotifyAuthorityCallsOnStartStopAuthority()
        {
            // create a networkidentity with our test components
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnSetHostVisibilityCallsComponentsAndCatchesExceptions()
        {
            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
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

            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

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
            GameObject.DestroyImmediate(gameObject);
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void OnCheckObserver()
        {
            // create a networkidentity with our test components
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnSerializeAndDeserializeAllSafely()
        {
            // create a networkidentity with our test components
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void OnSerializeAllSafelyShouldDetectTooManyComponents()
        {
            // create a networkidentity with our 65
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        // OnDeserializeSafely should be able to detect and handle serialization
        // mismatches (= if compA writes 10 bytes but only reads 8 or 12, it
        // shouldn't break compB's serialization. otherwise we end up with
        // insane runtime errors like monsters that look like npcs. that's what
        // happened back in the day with UNET).
        [Test]
        public void OnDeserializeSafelyShouldDetectAndHandleDeSerializationMismatch()
        {
            // create a networkidentity with our test components
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnStartLocalPlayer()
        {
            // create a networkidentity with our test components
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnNetworkDestroy()
        {
            // create a networkidentity with our test components
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void AddObserver()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

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

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void ClearObservers()
        {
            // create a networkidentity
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // add some observers
            identity.observers[42] = new NetworkConnectionToClient(42);
            identity.observers[43] = new NetworkConnectionToClient(43);

            // call ClearObservers
            identity.ClearObservers();
            Assert.That(identity.observers.Count, Is.EqualTo(0));

            // clean up
            GameObject.DestroyImmediate(gameObject);
        }
    }
}
