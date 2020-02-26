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
            public override void OnStartServer()
            {
                throw new Exception("some exception");
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
        public void OnStartServerComponentExceptionIsCaught()
        {
            // create a networkidentity with our test component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            gameObject.AddComponent<StartServerExceptionNetworkBehaviour>();

            // an exception in OnStartServer should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            identity.OnStartServer(); // should catch the exception internally and not throw it
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
    }
}
