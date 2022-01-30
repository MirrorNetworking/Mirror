using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    class StartServerNetworkBehaviour : NetworkBehaviour
    {
        internal bool onStartServerInvoked;
        public override void OnStartServer() => onStartServerInvoked = true;
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
        public override void OnStartAuthority() => ++called;
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
        public override void OnStopAuthority() => ++called;
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
        public override void OnStartLocalPlayer() => ++called;
    }

    class StopClientExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopClient()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StopClientCalledNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopClient() => ++called;
    }

    class StopLocalPlayerCalledNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopLocalPlayer() => ++called;
    }

    class StopLocalPlayerExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopLocalPlayer()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StopServerCalledNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopServer() => ++called;
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
            writer.WriteInt(value);
            return true;
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            value = reader.ReadInt();
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
            writer.WriteInt(value);
            // one too many
            writer.WriteInt(value);
            return true;
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            value = reader.ReadInt();
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

    public class NetworkIdentityTests : MirrorEditModeTest
    {
        [Test]
        public void OnStartServerTest()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StartServerNetworkBehaviour component1, out StartServerNetworkBehaviour component2);
            identity.OnStartServer();

            Assert.That(component1.onStartServerInvoked);
            Assert.That(component2.onStartServerInvoked);
        }

        // check isClient/isServer/isLocalPlayer in server-only mode
        [Test]
        public void ServerMode_IsFlags_Test()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity _, out IsClientServerCheckComponent component);

            // start the server
            NetworkServer.Listen(1000);

            // spawn it
            NetworkServer.Spawn(gameObject);

            // OnStartServer should have been called. check the flags.
            Assert.That(component.OnStartServer_isClient, Is.EqualTo(false));
            Assert.That(component.OnStartServer_isLocalPlayer, Is.EqualTo(false));
            Assert.That(component.OnStartServer_isServer, Is.EqualTo(true));
        }

        // check isClient/isServer/isLocalPlayer in host mode
        [Test]
        public void HostMode_IsFlags_Test()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity, out IsClientServerCheckComponent component);

            // start the server
            NetworkServer.Listen(1000);

            // start the client
            NetworkClient.ConnectHost();

            // set is as local player
            NetworkClient.InternalAddPlayer(identity);

            // spawn it
            NetworkServer.Spawn(gameObject);

            // OnStartServer should have been called. check the flags.
            Assert.That(component.OnStartServer_isClient, Is.EqualTo(true));
            Assert.That(component.OnStartServer_isLocalPlayer, Is.EqualTo(true));
            Assert.That(component.OnStartServer_isServer, Is.EqualTo(true));

            // stop the client
            NetworkServer.RemoveLocalConnection();
        }

        [Test]
        public void GetSetAssetId()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            // assign a guid
            Guid guid = new Guid(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
            identity.assetId = guid;

            // did it work?
            Assert.That(identity.assetId, Is.EqualTo(guid));
        }

        [Test]
        public void SetAssetId_GivesErrorIfOneExists()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            if (identity.assetId == Guid.Empty)
            {
                identity.assetId = Guid.NewGuid();
            }

            Guid guid1 = identity.assetId;

            // assign a guid
            Guid guid2 = Guid.NewGuid();
            LogAssert.Expect(LogType.Error, $"Can not Set AssetId on NetworkIdentity '{identity.name}' because it already had an assetId, current assetId '{guid1:N}', attempted new assetId '{guid2:N}'");
            identity.assetId = guid2;

            // guid was changed
            Assert.That(identity.assetId, Is.EqualTo(guid1));
        }

        [Test]
        public void SetAssetId_GivesErrorForEmptyGuid()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            if (identity.assetId == Guid.Empty)
            {
                identity.assetId = Guid.NewGuid();
            }

            Guid guid1 = identity.assetId;

            // assign a guid
            Guid guid2 = new Guid();
            LogAssert.Expect(LogType.Error, $"Can not set AssetId to empty guid on NetworkIdentity '{identity.name}', old assetId '{guid1:N}'");
            identity.assetId = guid2;

            // guid was NOT changed
            Assert.That(identity.assetId, Is.EqualTo(guid1));
        }

        [Test]
        public void SetAssetId_DoesNotGiveErrorIfBothOldAndNewAreEmpty()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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
        public void RemoveObserver()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            // call OnStartServer so that observers dict is created
            identity.OnStartServer();

            // add an observer connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            identity.observers[connection.connectionId] = connection;

            // RemoveObserver with invalid connection should do nothing
            identity.RemoveObserver(new NetworkConnectionToClient(43));
            Assert.That(identity.observers.Count, Is.EqualTo(1));

            // RemoveObserver with existing connection should remove it
            identity.RemoveObserver(connection);
            Assert.That(identity.observers.Count, Is.EqualTo(0));
        }

        [Test]
        public void AssignSceneID()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            // OnValidate will have been called. make sure that assetId was set
            // to 0 empty and not anything else, because this is a scene object
            Assert.That(identity.assetId, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void OnStartServerCallsComponentsAndCatchesExceptions()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StartServerExceptionNetworkBehaviour comp);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StartClientExceptionNetworkBehaviour comp);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StartAuthorityExceptionNetworkBehaviour comp);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StopAuthorityExceptionNetworkBehaviour comp);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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

            // create connections
            CreateLocalConnectionPair(out LocalConnectionToClient owner, out LocalConnectionToServer clientConnection);
            owner.isReady = true;

            // setup NetworkServer/Client connections so messages are handled
            NetworkClient.connection = clientConnection;
            NetworkServer.connections[owner.connectionId] = owner;

            // add client handlers
            int spawnCalled = 0;
            void Handler(SpawnMessage _) => ++spawnCalled;
            NetworkClient.RegisterHandler<SpawnMessage>(Handler, false);

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

            // shouldn't be able to assign authority while already owned by
            // another connection
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            result = identity.AssignClientAuthority(new NetworkConnectionToClient(43));
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
        }

        [Test]
        public void NotifyAuthorityCallsOnStartStopAuthority()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StartAuthorityCalledNetworkBehaviour compStart, out StopAuthorityCalledNetworkBehaviour compStop);

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
            // should be changed
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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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
        }

        [Test]
        public void CreatingNetworkBehavioursCacheShouldLogErrorForTooComponents()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity);

            // add byte.MaxValue+1 components
            for (int i = 0; i < byte.MaxValue + 1; ++i)
            {
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            }

            // CreateNetworked already initializes the components.
            // let's reset and initialize again with the added ones.
            identity.Reset();
            identity.Awake();

            // call NetworkBehaviours property to create the cache
            LogAssert.Expect(LogType.Error, new Regex($"Only {byte.MaxValue} NetworkBehaviour components are allowed for NetworkIdentity.+"));
            _ = identity.NetworkBehaviours;
        }

        [Test]
        public void OnStartLocalPlayer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out StartLocalPlayerExceptionNetworkBehaviour compEx,
                out StartLocalPlayerCalledNetworkBehaviour comp);

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
        public void OnStopLocalPlayer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out StopLocalPlayerCalledNetworkBehaviour comp);

            // call OnStopLocalPlayer in identity
            identity.OnStopLocalPlayer();
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopLocalPlayerException()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out StopLocalPlayerExceptionNetworkBehaviour compEx,
                out StopLocalPlayerCalledNetworkBehaviour comp);

            // call OnStopLocalPlayer in identity
            // one component will throw an exception, but that shouldn't stop
            // OnStopLocalPlayer from being called in the second one
            // exception will log an error
            LogAssert.ignoreFailingMessages = true;
            identity.OnStopLocalPlayer();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(compEx.called, Is.EqualTo(1));
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopClient()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out StopClientCalledNetworkBehaviour comp);

            // call OnStopClient in identity
            identity.OnStopClient();
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopClientException()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out StopClientExceptionNetworkBehaviour compEx,
                out StopClientCalledNetworkBehaviour comp);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out StopServerCalledNetworkBehaviour comp);

            identity.OnStopServer();
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void OnStopServerException()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out StopServerExceptionNetworkBehaviour compEx);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            // create some connections
            NetworkConnectionToClient connection1 = new NetworkConnectionToClient(42);
            NetworkConnectionToClient connection2 = new NetworkConnectionToClient(43);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

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
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out OnStartClientTestNetworkBehaviour compA,
                out OnStartClientTestNetworkBehaviour compB);

            // set syncintervals so one is always dirty, one is never dirty
            compA.syncInterval = 0;
            compB.syncInterval = Mathf.Infinity;

            // set components dirty bits
            compA.SetSyncVarDirtyBit(0x0001);
            compB.SetSyncVarDirtyBit(0x1001);
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
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out OnStartClientTestNetworkBehaviour compA,
                out OnStartClientTestNetworkBehaviour compB);

            // set syncintervals so one is always dirty, one is never dirty
            compA.syncInterval = 0;
            compB.syncInterval = Mathf.Infinity;

            // set components dirty bits
            compA.SetSyncVarDirtyBit(0x0001);
            compB.SetSyncVarDirtyBit(0x1001);
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
            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            // modify it a bit
            identity.isClient = true;
            // creates .observers and generates a netId
            identity.OnStartServer();
            identity.connectionToClient = new NetworkConnectionToClient(1);
            identity.connectionToServer = new NetworkConnectionToServer();
            identity.observers[43] = new NetworkConnectionToClient(2);

            // mark for reset and reset
            identity.Reset();
            Assert.That(identity.isServer, Is.False);
            Assert.That(identity.isClient, Is.False);
            Assert.That(identity.isLocalPlayer, Is.False);
            Assert.That(identity.netId, Is.EqualTo(0));
            Assert.That(identity.connectionToClient, Is.Null);
            Assert.That(identity.connectionToServer, Is.Null);
            Assert.That(identity.hasAuthority, Is.False);
            Assert.That(identity.observers, Is.Empty);
        }

        [Test, Ignore("NetworkServerTest.SendCommand does it already")]
        public void HandleCommand() {}

        [Test, Ignore("RpcTests do it already")]
        public void HandleRpc() {}
    }
}
