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
    class EmptyBehaviour : NetworkBehaviour
    {
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourSendCommandInternalComponent : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;

        // weaver generates this from [Command]
        // but for tests we need to add it manually
        public static void CommandGenerated(NetworkBehaviour comp, NetworkReader reader)
        {
            ++((NetworkBehaviourSendCommandInternalComponent)comp).called;
        }

        // SendCommandInternal is protected. let's expose it so we can test it.
        public void CallSendCommandInternal()
        {
            SendCommandInternal(GetType(), nameof(CommandGenerated), new NetworkWriter(), 0);
        }
    }

    public class NetworkBehaviourTests
    {
        GameObject gameObject;
        NetworkIdentity identity;
        EmptyBehaviour emptyBehaviour; // useful in most tests, but not necessarily all tests

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();

            // add a behaviour for testing
            emptyBehaviour = gameObject.AddComponent<EmptyBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void IsServerOnly()
        {
            // start server and assign netId so that isServer is true
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1);
            identity.netId = 42;

            // isServerOnly should be true when isServer = true && isClient = false
            Assert.That(emptyBehaviour.isServer, Is.True);
            Assert.That(emptyBehaviour.isClient, Is.False);
            Assert.That(emptyBehaviour.isServerOnly, Is.True);

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        [Test]
        public void IsClientOnly()
        {
            // isClientOnly should be true when isServer = false && isClient = true
            identity.isClient = true;
            Assert.That(emptyBehaviour.isServer, Is.False);
            Assert.That(emptyBehaviour.isClient, Is.True);
            Assert.That(emptyBehaviour.isClientOnly, Is.True);
        }

        [Test]
        public void HasNoAuthorityByDefault()
        {
            // no authority by default
            Assert.That(emptyBehaviour.hasAuthority, Is.False);
        }

        [Test]
        public void HasIdentitysNetId()
        {
            identity.netId = 42;
            Assert.That(emptyBehaviour.netId, Is.EqualTo(42));
        }

        [Test]
        public void HasIdentitysConnectionToServer()
        {
            identity.connectionToServer = new ULocalConnectionToServer();
            Assert.That(emptyBehaviour.connectionToServer, Is.EqualTo(identity.connectionToServer));
        }

        [Test]
        public void HasIdentitysConnectionToClient()
        {
            identity.connectionToClient = new ULocalConnectionToClient();
            Assert.That(emptyBehaviour.connectionToClient, Is.EqualTo(identity.connectionToClient));
        }

        [Test]
        public void ComponentIndex()
        {
            // add one extra component
            EmptyBehaviour extra = gameObject.AddComponent<EmptyBehaviour>();

            // original one is first networkbehaviour, so index is 0
            Assert.That(emptyBehaviour.ComponentIndex, Is.EqualTo(0));

            // extra one is second networkbehaviour, so index is 1
            Assert.That(extra.ComponentIndex, Is.EqualTo(1));
        }

        [Test]
        public void OnCheckObserverTrueByDefault()
        {
            Assert.That(emptyBehaviour.OnCheckObserver(null), Is.True);
        }

        [Test]
        public void SendCommandInternal()
        {
            // transport is needed by server and client.
            // it needs to be on a gameobject because client.connect enables it,
            // which throws a NRE if not on a gameobject
            GameObject transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

            // we need to start a server and connect a client in order to be
            // able to send commands
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<SpawnMessage>((conn, msg) => {}, false);
            NetworkServer.Listen(1);
            NetworkClient.Connect("localhost");

            // setup worked?
            Assert.That(NetworkServer.active, Is.True);
            Assert.That(NetworkClient.active, Is.True);

            // add command component
            NetworkBehaviourSendCommandInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendCommandInternalComponent>();

            // create a connection from client to server and from server to client
            ULocalConnectionToClient connection = new ULocalConnectionToClient {
                isReady = true,
                isAuthenticated = true // commands require authentication
            };
            connection.connectionToServer = new ULocalConnectionToServer {
                isReady = true,
                isAuthenticated = true // commands require authentication
            };
            connection.connectionToServer.connectionToClient = connection;
            identity.connectionToClient = connection;

            // give authority so we can call commands
            identity.netId = 42;
            identity.hasAuthority = true;
            Assert.That(identity.hasAuthority, Is.True);

            // isClient needs to be true, otherwise we can't call commands
            identity.isClient = true;

            // register our connection at the server so that it sets up the
            // connection's handlers
            NetworkServer.AddConnection(connection);

            // clientscene.readyconnection needs to be set for commands
            ClientScene.Ready(connection.connectionToServer);

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterCommandDelegate(typeof(NetworkBehaviourSendCommandInternalComponent),
                nameof(NetworkBehaviourSendCommandInternalComponent.CommandGenerated),
                NetworkBehaviourSendCommandInternalComponent.CommandGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call command
            Assert.That(comp.called, Is.EqualTo(0));
            comp.CallSendCommandInternal();
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            ClientScene.Shutdown(); // clear clientscene.readyconnection
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(transportGO);
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourHookGuardTester : NetworkBehaviour
    {
        [Test]
        public void HookGuard()
        {
            // set hook guard for some bits
            for (int i = 0; i < 10; ++i)
            {
                ulong bit = 1ul << i;

                // should be false by default
                Assert.That(getSyncVarHookGuard(bit), Is.False);

                // set true
                setSyncVarHookGuard(bit, true);
                Assert.That(getSyncVarHookGuard(bit), Is.True);

                // set false again
                setSyncVarHookGuard(bit, false);
                Assert.That(getSyncVarHookGuard(bit), Is.False);
            }
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourInitSyncObjectTester : NetworkBehaviour
    {
        [Test]
        public void InitSyncObject()
        {
            SyncObject syncObject = new SyncListBool();
            InitSyncObject(syncObject);
            Assert.That(syncObjects.Count, Is.EqualTo(1));
            Assert.That(syncObjects[0], Is.EqualTo(syncObject));
        }
    }
}
