using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mirror.RemoteCalls;
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
            public override void OnStopClient()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class NetworkDestroyCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStopClient() { ++called; }
        }

        class StopServerCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStopServer() { ++called; }
        }

        class StopServerExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override void OnStopServer()
            {
                ++called;
                throw new Exception("some exception");
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
                // one too many
                writer.WriteInt32(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadInt32();
            }
        }

#pragma warning disable 618
        class RebuildObserversNetworkBehaviour : NetworkVisibility
        {
            public NetworkConnection observer;
            public override bool OnCheckObserver(NetworkConnection conn) { return true; }
            public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
            {
                observers.Add(observer);
            }
            public override void OnSetHostVisibility(bool visible) {}
        }

        class RebuildEmptyObserversNetworkBehaviour : NetworkVisibility
        {
            public override bool OnCheckObserver(NetworkConnection conn) { return true; }
            public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize) {}
            public int hostVisibilityCalled;
            public bool hostVisibilityValue;
            public override void OnSetHostVisibility(bool visible)
            {
                ++hostVisibilityCalled;
                hostVisibilityValue = visible;
            }
        }
#pragma warning restore 618

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

            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();
        }

        [TearDown]
        public void TearDown()
        {
            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            identity.isServer = false;
            GameObject.DestroyImmediate(gameObject);
            // clean so that null entries are not in dictionary
            NetworkIdentity.spawned.Clear();

            GameObject.DestroyImmediate(Transport.activeTransport.gameObject);
            Transport.activeTransport = null;
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

            // clean up
            NetworkIdentity.spawned.Clear();
        }

        // check isClient/isServer/isLocalPlayer in host mode
        [Test]
        public void HostMode_IsFlags_Test()
        {
            // start the server
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
        public void SetAssetId_GivesErrorIfOneExists()
        {
            if (identity.assetId == Guid.Empty)
            {
                identity.assetId = Guid.NewGuid();
            }

            Guid guid1 = identity.assetId;

            // assign a guid
            Guid guid2 = Guid.NewGuid();
            LogAssert.Expect(LogType.Error, $"Can not Set AssetId on NetworkIdentity '{identity.name}' because it already had an assetId, current assetId '{guid1.ToString("N")}', attempted new assetId '{guid2.ToString("N")}'");
            identity.assetId = guid2;

            // guid was changed
            Assert.That(identity.assetId, Is.EqualTo(guid1));
        }

        [Test]
        public void SetAssetId_GivesErrorForEmptyGuid()
        {
            if (identity.assetId == Guid.Empty)
            {
                identity.assetId = Guid.NewGuid();
            }

            Guid guid1 = identity.assetId;

            // assign a guid
            Guid guid2 = new Guid();
            LogAssert.Expect(LogType.Error, $"Can not set AssetId to empty guid on NetworkIdentity '{identity.name}', old assetId '{guid1.ToString("N")}'");
            identity.assetId = guid2;

            // guid was NOT changed
            Assert.That(identity.assetId, Is.EqualTo(guid1));
        }
        [Test]
        public void SetAssetId_DoesNotGiveErrorIfBothOldAndNewAreEmpty()
        {
            Debug.Assert(identity.assetId == Guid.Empty, "assetId needs to be empty at the start of this test");
            // assign a guid
            Guid guid2 = new Guid();
            // expect no errors
            identity.assetId = guid2;

            // guid was still empty
            Assert.That(identity.assetId, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void SetClientOwner()
        {
            // SetClientOwner
            LocalConnectionToClient original = new LocalConnectionToClient();
            identity.SetClientOwner(original);
            Assert.That(identity.connectionToClient, Is.EqualTo(original));

            // setting it when it's already set shouldn't overwrite the original
            LocalConnectionToClient overwrite = new LocalConnectionToClient();
            // will log a warning
            LogAssert.ignoreFailingMessages = true;
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
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, false, 0);
            identity.observers[connection.connectionId] = connection;

            // RemoveObserverInternal with invalid connection should do nothing
            identity.RemoveObserverInternal(new NetworkConnectionToClient(43, false, 0));
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
            // should catch the exception internally and not throw it
            identity.OnStartServer();
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
            // should catch the exception internally and not throw it
            identity.OnStartClient();
            Assert.That(comp.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            identity.OnStartClient();
            // same as before?
            Assert.That(comp.called, Is.EqualTo(1));
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
            // should catch the exception internally and not throw it
            identity.OnStartAuthority();
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
            // should catch the exception internally and not throw it
            identity.OnStopAuthority();
            Assert.That(comp.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AssignAndRemoveClientAuthority()
        {
            // test the callback too
            int callbackCalled = 0;
            NetworkConnection callbackConnection = null;
            NetworkIdentity callbackIdentity = null;
            bool callbackState = false;
            NetworkIdentity.clientAuthorityCallback += (conn, networkIdentity, state) =>
            {
                ++callbackCalled;
                callbackConnection = conn;
                callbackIdentity = identity;
                callbackState = state;
            };

            // create a connection
            LocalConnectionToClient owner = new LocalConnectionToClient();
            owner.isReady = true;
            // add client handlers
            owner.connectionToServer = new LocalConnectionToServer();
            int spawnCalled = 0;
            owner.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>{
                { MessagePacking.GetId<SpawnMessage>(), ((conn, reader, channelId) => ++spawnCalled) }
            });

            // assigning authority should only work on server.
            // if isServer is false because server isn't running yet then it
            // should fail.
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            bool result = identity.AssignClientAuthority(owner);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(result, Is.False);

            // server is needed
            NetworkServer.Listen(1);

            // call OnStartServer so that isServer is true
            identity.OnStartServer();
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
            // process pending messages
            owner.connectionToServer.Update();
            Assert.That(spawnCalled, Is.EqualTo(1));

            // shouldn't be able to assign authority while already owned by
            // another connection
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            result = identity.AssignClientAuthority(new NetworkConnectionToClient(43, false, 0));
            LogAssert.ignoreFailingMessages = false;
            Assert.That(result, Is.False);
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));

            // someone might try to remove authority by assigning null.
            // make sure this fails.
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            result = identity.AssignClientAuthority(null);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(result, Is.False);

            // removing authority while not isServer shouldn't work.
            // only allow it on server.
            identity.isServer = false;

            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            identity.RemoveClientAuthority();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));

            // enable isServer again
            identity.isServer = true;

            // removing authority for the main player object shouldn't work
            // set connection's player object
            owner.identity = identity;
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            identity.RemoveClientAuthority();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(identity.connectionToClient, Is.EqualTo(owner));
            Assert.That(callbackCalled, Is.EqualTo(1));

            // removing authority for a non-main-player object should work
            owner.identity = null;
            identity.RemoveClientAuthority();
            Assert.That(identity.connectionToClient, Is.Null);
            Assert.That(callbackCalled, Is.EqualTo(2));
            // the one that was removed
            Assert.That(callbackConnection, Is.EqualTo(owner));
            Assert.That(callbackIdentity, Is.EqualTo(identity));
            Assert.That(callbackState, Is.EqualTo(false));

            // clean up
            NetworkServer.Shutdown();
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
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.True);
            // start should be called
            Assert.That(compStart.called, Is.EqualTo(1));
            // stop shouldn't
            Assert.That(compStop.called, Is.EqualTo(0));

            // set it to true again, should do nothing because already true
            identity.hasAuthority = true;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.True);
            // same as before
            Assert.That(compStart.called, Is.EqualTo(1));
            // same as before
            Assert.That(compStop.called, Is.EqualTo(0));

            // set it to false, should call OnStopAuthority
            identity.hasAuthority = false;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.False);
            // same as before
            Assert.That(compStart.called, Is.EqualTo(1));
            // stop should be called
            Assert.That(compStop.called, Is.EqualTo(1));

            // set it to false again, should do nothing because already false
            identity.hasAuthority = false;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.False);
            // same as before
            Assert.That(compStart.called, Is.EqualTo(1));
            // same as before
            Assert.That(compStop.called, Is.EqualTo(1));
        }

        // OnStartServer in host mode should set isClient=true
        [Test]
        public void OnStartServerInHostModeSetsIsClientTrue()
        {
            // call client connect so that internals are set up
            // (it won't actually successfully connect)
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
            // error log because of the exception is expected
            LogAssert.ignoreFailingMessages = true;
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
            // error log because of the exception is expected
            LogAssert.ignoreFailingMessages = true;
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp1.value, Is.EqualTo(12345));
            Assert.That(comp2.value, Is.EqualTo("67890"));

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for observers - should work even if compExc throws an exception
            reader = new NetworkReader(observersWriter.ToArray());
            // error log because of the exception is expected
            LogAssert.ignoreFailingMessages = true;
            identity.OnDeserializeAllSafely(reader, true);
            LogAssert.ignoreFailingMessages = false;
            // observers mode, should be in data
            Assert.That(comp1.value, Is.EqualTo(12345));
            // owner mode, should not be in data
            Assert.That(comp2.value, Is.EqualTo(null));
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void OnSerializeAllSafelyShouldNotLogErrorsForTooManyComponents()
        {
            // add 65 components
            for (int i = 0; i < 65; ++i)
            {
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            }
            // ignore error from creating cache (has its own test)
            LogAssert.ignoreFailingMessages = true;
            _ = identity.NetworkBehaviours;
            LogAssert.ignoreFailingMessages = false;


            // try to serialize
            NetworkWriter ownerWriter = new NetworkWriter();
            NetworkWriter observersWriter = new NetworkWriter();

            identity.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // Should still write with too mnay Components because NetworkBehavioursCache should handle the error
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));
            Assert.That(ownerWritten, Is.GreaterThan(0));
            Assert.That(observersWritten, Is.GreaterThan(0));
        }

        [Test]
        public void CreatingNetworkBehavioursCacheShouldLogErrorForTooComponents()
        {
            // add byte.MaxValue+1 components
            for (int i = 0; i < byte.MaxValue + 1; ++i)
            {
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            }

            // call NetworkBehaviours property to create the cache
            LogAssert.Expect(LogType.Error, new Regex($"Only {byte.MaxValue} NetworkBehaviour components are allowed for NetworkIdentity.+"));
            _ = identity.NetworkBehaviours;
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
            // warning log because of serialization mismatch
            LogAssert.ignoreFailingMessages = true;
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
            // exception will log an error
            LogAssert.ignoreFailingMessages = true;
            identity.OnStartLocalPlayer();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(compEx.called, Is.EqualTo(1));
            Assert.That(comp.called, Is.EqualTo(1));

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            identity.OnStartLocalPlayer();
            // same as before?
            Assert.That(compEx.called, Is.EqualTo(1));
            // same as before?
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopClient()
        {
            // add components
            NetworkDestroyExceptionNetworkBehaviour compEx = gameObject.AddComponent<NetworkDestroyExceptionNetworkBehaviour>();
            NetworkDestroyCalledNetworkBehaviour comp = gameObject.AddComponent<NetworkDestroyCalledNetworkBehaviour>();

            // make sure our test values are set to 0
            Assert.That(compEx.called, Is.EqualTo(0));
            Assert.That(comp.called, Is.EqualTo(0));

            // call OnStopClient in identity
            // one component will throw an exception, but that shouldn't stop
            // OnStopClient from being called in the second one
            // exception will log an error
            LogAssert.ignoreFailingMessages = true;
            identity.OnStopClient();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(compEx.called, Is.EqualTo(1));
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopServer()
        {
            // add components
            StopServerCalledNetworkBehaviour comp = gameObject.AddComponent<StopServerCalledNetworkBehaviour>();

            // make sure our test values are set to 0
            Assert.That(comp.called, Is.EqualTo(0));

            identity.OnStopServer();
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopServerEx()
        {
            // add components
            StopServerExceptionNetworkBehaviour compEx = gameObject.AddComponent<StopServerExceptionNetworkBehaviour>();

            // make sure our test values are set to 0
            Assert.That(compEx.called, Is.EqualTo(0));

            // call OnStopClient in identity
            // one component will throw an exception, but that shouldn't stop
            // OnStopClient from being called in the second one
            // exception will log an error
            LogAssert.ignoreFailingMessages = true;
            identity.OnStopServer();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(compEx.called, Is.EqualTo(1));
        }

        [Test]
        public void AddObserver()
        {
            // create some connections
            NetworkConnectionToClient connection1 = new NetworkConnectionToClient(42, false, 0);
            NetworkConnectionToClient connection2 = new NetworkConnectionToClient(43, false, 0);

            // AddObserver should return early if called before .observers was
            // created
            Assert.That(identity.observers, Is.Null);
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
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
            NetworkConnectionToClient duplicate = new NetworkConnectionToClient(connection1.connectionId, false, 0);
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
            identity.observers[42] = new NetworkConnectionToClient(42, false, 0);
            identity.observers[43] = new NetworkConnectionToClient(43, false, 0);

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
            // dirty because interval reached and mask != 0
            Assert.That(compA.IsDirty(), Is.True);
            // not dirty because syncinterval not reached
            Assert.That(compB.IsDirty(), Is.False);

            // call identity.ClearDirtyComponentsDirtyBits
            identity.ClearDirtyComponentsDirtyBits();
            // should be cleared now
            Assert.That(compA.IsDirty(), Is.False);
            // should be untouched
            Assert.That(compB.IsDirty(), Is.False);

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
            // dirty because interval reached and mask != 0
            Assert.That(compA.IsDirty(), Is.True);
            // not dirty because syncinterval not reached
            Assert.That(compB.IsDirty(), Is.False);

            // call identity.ClearAllComponentsDirtyBits
            identity.ClearAllComponentsDirtyBits();
            // should be cleared now
            Assert.That(compA.IsDirty(), Is.False);
            // should be cleared now
            Assert.That(compB.IsDirty(), Is.False);

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
            // creates .observers and generates a netId
            identity.OnStartServer();
            identity.connectionToClient = new NetworkConnectionToClient(1, false, 0);
            identity.connectionToServer = new NetworkConnectionToServer();
            identity.observers[43] = new NetworkConnectionToClient(2, false, 0);

            // mark for reset and reset
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
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1, false, 0);
            Assert.That(comp0.called, Is.EqualTo(0));
            Assert.That(comp0.senderConnectionInCall, Is.Null);

            // register the command delegate, otherwise it's not found
            int registeredHash = RemoteCallHelper.RegisterDelegate(typeof(CommandTestNetworkBehaviour),
                nameof(CommandTestNetworkBehaviour.CommandGenerated),
                MirrorInvokeType.Command,
                CommandTestNetworkBehaviour.CommandGenerated,
                false);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call HandleCommand and check if the command was called in the component
            int functionHash = RemoteCallHelper.GetMethodHash(typeof(CommandTestNetworkBehaviour), nameof(CommandTestNetworkBehaviour.CommandGenerated));
            NetworkReader payload = new NetworkReader(new byte[0]);
            identity.HandleRemoteCall(0, functionHash, MirrorInvokeType.Command, payload, connection);
            Assert.That(comp0.called, Is.EqualTo(1));
            Assert.That(comp0.senderConnectionInCall, Is.EqualTo(connection));


            // try wrong component index. command shouldn't be called again.
            // warning is expected
            LogAssert.ignoreFailingMessages = true;
            identity.HandleRemoteCall(1, functionHash, MirrorInvokeType.Command, payload, connection);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong function hash. command shouldn't be called again.
            // warning is expected
            LogAssert.ignoreFailingMessages = true;
            identity.HandleRemoteCall(0, functionHash + 1, MirrorInvokeType.Command, payload, connection);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            RemoteCallHelper.RemoveDelegate(registeredHash);
        }

        [Test]
        public void HandleRpc()
        {
            // add rpc component
            RpcTestNetworkBehaviour comp0 = gameObject.AddComponent<RpcTestNetworkBehaviour>();
            Assert.That(comp0.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            int registeredHash = RemoteCallHelper.RegisterDelegate(typeof(RpcTestNetworkBehaviour),
                nameof(RpcTestNetworkBehaviour.RpcGenerated),
                MirrorInvokeType.ClientRpc,
                RpcTestNetworkBehaviour.RpcGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call HandleRpc and check if the rpc was called in the component
            int functionHash = RemoteCallHelper.GetMethodHash(typeof(RpcTestNetworkBehaviour), nameof(RpcTestNetworkBehaviour.RpcGenerated));
            NetworkReader payload = new NetworkReader(new byte[0]);
            identity.HandleRemoteCall(0, functionHash, MirrorInvokeType.ClientRpc, payload);
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong component index. rpc shouldn't be called again.
            // warning is expected
            LogAssert.ignoreFailingMessages = true;
            identity.HandleRemoteCall(1, functionHash, MirrorInvokeType.ClientRpc, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // try wrong function hash. rpc shouldn't be called again.
            // warning is expected
            LogAssert.ignoreFailingMessages = true;
            identity.HandleRemoteCall(0, functionHash + 1, MirrorInvokeType.ClientRpc, payload);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp0.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            RemoteCallHelper.RemoveDelegate(registeredHash);
        }


        [Test]
        public void ServerUpdate()
        {
            // add components
            SerializeTest1NetworkBehaviour compA = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            // test value
            compA.value = 1337;
            // set syncInterval so IsDirty passes the interval check
            compA.syncInterval = 0;
            // one needs to sync to owner
            compA.syncMode = SyncMode.Owner;
            SerializeTest2NetworkBehaviour compB = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();
            // test value
            compB.value = "test";
            // set syncInterval so IsDirty passes the interval check
            compB.syncInterval = 0;
            // one needs to sync to owner
            compB.syncMode = SyncMode.Observers;

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
            LocalConnectionToClient owner = new LocalConnectionToClient();
            // for syncing
            owner.isReady = true;
            // add a client to server connection + handler to receive syncs
            owner.connectionToServer = new LocalConnectionToServer();
            int ownerCalled = 0;
            owner.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>
            {
                { MessagePacking.GetId<UpdateVarsMessage>(), ((conn, reader, channelId) => ++ownerCalled) }
            });
            identity.connectionToClient = owner;

            // add an observer connection that will receive the updates
            LocalConnectionToClient observer = new LocalConnectionToClient();
            // we only sync to ready observers
            observer.isReady = true;
            // add a client to server connection + handler to receive syncs
            observer.connectionToServer = new LocalConnectionToServer();
            int observerCalled = 0;
            observer.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>
            {
                { MessagePacking.GetId<UpdateVarsMessage>(), ((conn, reader, channelId) => ++observerCalled) }
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
    }
}
