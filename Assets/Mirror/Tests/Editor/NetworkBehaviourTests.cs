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

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourSendRPCInternalComponent : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;

        // weaver generates this from [Command]
        // but for tests we need to add it manually
        public static void RPCGenerated(NetworkBehaviour comp, NetworkReader reader)
        {
            ++((NetworkBehaviourSendRPCInternalComponent)comp).called;
        }

        // SendCommandInternal is protected. let's expose it so we can test it.
        public void CallSendRPCInternal()
        {
            SendRPCInternal(GetType(), nameof(RPCGenerated), new NetworkWriter(), 0);
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourSendTargetRPCInternalComponent : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;

        // weaver generates this from [Command]
        // but for tests we need to add it manually
        public static void TargetRPCGenerated(NetworkBehaviour comp, NetworkReader reader)
        {
            ++((NetworkBehaviourSendTargetRPCInternalComponent)comp).called;
        }

        // SendCommandInternal is protected. let's expose it so we can test it.
        public void CallSendTargetRPCInternal(NetworkConnection conn)
        {
            SendTargetRPCInternal(conn, GetType(), nameof(TargetRPCGenerated), new NetworkWriter(), 0);
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourSendEventInternalComponent : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;

        // weaver generates this from [Command]
        // but for tests we need to add it manually
        public static void EventGenerated(NetworkBehaviour comp, NetworkReader reader)
        {
            ++((NetworkBehaviourSendEventInternalComponent)comp).called;
        }

        // SendCommandInternal is protected. let's expose it so we can test it.
        public void CallSendEventInternal()
        {
            SendEventInternal(GetType(), nameof(EventGenerated), new NetworkWriter(), 0);
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourDelegateComponent : NetworkBehaviour
    {
        public static void Delegate(NetworkBehaviour comp, NetworkReader reader) {}
        public static void Delegate2(NetworkBehaviour comp, NetworkReader reader) {}
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
            NetworkServer.RemoveLocalConnection();
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
            Assert.That(NetworkServer.active, Is.True);

            // add command component
            NetworkBehaviourSendCommandInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendCommandInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

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

            // calling command before client is connected shouldn't work
            LogAssert.ignoreFailingMessages = true; // error log is expected
            comp.CallSendCommandInternal();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp.called, Is.EqualTo(0));

            // connect client
            NetworkClient.Connect("localhost");
            Assert.That(NetworkClient.active, Is.True);

            // calling command before we have authority should fail
            LogAssert.ignoreFailingMessages = true; // error log is expected
            comp.CallSendCommandInternal();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp.called, Is.EqualTo(0));

            // give authority so we can call commands
            identity.netId = 42;
            identity.hasAuthority = true;
            Assert.That(identity.hasAuthority, Is.True);

            // isClient needs to be true, otherwise we can't call commands
            identity.isClient = true;

            // register our connection at the server so that it sets up the
            // connection's handlers
            NetworkServer.AddConnection(connection);

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterCommandDelegate(typeof(NetworkBehaviourSendCommandInternalComponent),
                nameof(NetworkBehaviourSendCommandInternalComponent.CommandGenerated),
                NetworkBehaviourSendCommandInternalComponent.CommandGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // calling command before clientscene has ready connection shouldn't work
            LogAssert.ignoreFailingMessages = true; // error log is expected
            comp.CallSendCommandInternal();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(comp.called, Is.EqualTo(0));

            // clientscene.readyconnection needs to be set for commands
            ClientScene.Ready(connection.connectionToServer);

            // call command
            comp.CallSendCommandInternal();
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
            ClientScene.Shutdown(); // clear clientscene.readyconnection
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void InvokeCommand()
        {
            // add command component
            NetworkBehaviourSendCommandInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendCommandInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterCommandDelegate(typeof(NetworkBehaviourSendCommandInternalComponent),
                nameof(NetworkBehaviourSendCommandInternalComponent.CommandGenerated),
                NetworkBehaviourSendCommandInternalComponent.CommandGenerated);

            // invoke command
            int cmdHash = NetworkBehaviour.GetMethodHash(
                typeof(NetworkBehaviourSendCommandInternalComponent),
                nameof(NetworkBehaviourSendCommandInternalComponent.CommandGenerated));
            comp.InvokeCommand(cmdHash, new NetworkReader(new byte[0]));
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void SendRPCInternal()
        {
            // add rpc component
            NetworkBehaviourSendRPCInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendRPCInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

            // transport is needed by server and client.
            // it needs to be on a gameobject because client.connect enables it,
            // which throws a NRE if not on a gameobject
            GameObject transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

            // calling rpc before server is active shouldn't work
            LogAssert.Expect(LogType.Error, "RPC Function " + nameof(NetworkBehaviourSendRPCInternalComponent.RPCGenerated) + " called on Client.");
            comp.CallSendRPCInternal();
            Assert.That(comp.called, Is.EqualTo(0));

            // we need to start a server and connect a client in order to be
            // able to send commands
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<SpawnMessage>((conn, msg) => {}, false);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // connect host client
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.active, Is.True);

            // get the host connection which already has client->server and
            // server->client set up
            ULocalConnectionToServer connectionToServer = (ULocalConnectionToServer)NetworkClient.connection;

            // set host connection as ready and authenticated
            connectionToServer.isReady = true;
            connectionToServer.isAuthenticated = true;
            connectionToServer.connectionToClient.isReady = true;
            connectionToServer.connectionToClient.isAuthenticated = true;
            connectionToServer.connectionToClient.identity = identity;

            // calling rpc before isServer is true shouldn't work
            LogAssert.Expect(LogType.Warning, "ClientRpc " + nameof(NetworkBehaviourSendRPCInternalComponent.RPCGenerated) + " called on un-spawned object: " + gameObject.name);
            comp.CallSendRPCInternal();
            Assert.That(comp.called, Is.EqualTo(0));

            // we need an observer because sendrpc sends to ready observers
            identity.OnStartServer(); // creates observers
            identity.observers[connectionToServer.connectionToClient.connectionId] = connectionToServer.connectionToClient;

            identity.netId = 42;

            // isServer needs to be true, otherwise we can't call rpcs
            Assert.That(comp.isServer, Is.True);

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterRpcDelegate(typeof(NetworkBehaviourSendRPCInternalComponent),
                nameof(NetworkBehaviourSendRPCInternalComponent.RPCGenerated),
                NetworkBehaviourSendRPCInternalComponent.RPCGenerated);

            // identity needs to be in spawned dict, otherwise rpc handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call rpc
            comp.CallSendRPCInternal();

            // update client's connection so that pending messages are processed
            connectionToServer.Update();

            // rpc should have been called now
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
            ClientScene.Shutdown(); // clear clientscene.readyconnection
            NetworkServer.RemoveLocalConnection();
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void SendTargetRPCInternal()
        {
            // add rpc component
            NetworkBehaviourSendTargetRPCInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendTargetRPCInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

            // transport is needed by server and client.
            // it needs to be on a gameobject because client.connect enables it,
            // which throws a NRE if not on a gameobject
            GameObject transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

            // calling rpc before server is active shouldn't work
            LogAssert.Expect(LogType.Error, "TargetRPC Function " + nameof(NetworkBehaviourSendTargetRPCInternalComponent.TargetRPCGenerated) + " called on client.");
            comp.CallSendTargetRPCInternal(null);
            Assert.That(comp.called, Is.EqualTo(0));

            // we need to start a server and connect a client in order to be
            // able to send commands
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<SpawnMessage>((conn, msg) => {}, false);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // connect host client
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.active, Is.True);

            // get the host connection which already has client->server and
            // server->client set up
            ULocalConnectionToServer connectionToServer = (ULocalConnectionToServer)NetworkClient.connection;

            // set host connection as ready and authenticated
            connectionToServer.isReady = true;
            connectionToServer.isAuthenticated = true;
            connectionToServer.connectionToClient.isReady = true;
            connectionToServer.connectionToClient.isAuthenticated = true;
            connectionToServer.connectionToClient.identity = identity;

            // calling rpc before isServer is true shouldn't work
            LogAssert.Expect(LogType.Warning, "TargetRpc " + nameof(NetworkBehaviourSendTargetRPCInternalComponent.TargetRPCGenerated) + " called on un-spawned object: " + gameObject.name);
            comp.CallSendTargetRPCInternal(null);
            Assert.That(comp.called, Is.EqualTo(0));

            identity.netId = 42;

            // calling rpc on connectionToServer shouldn't work
            LogAssert.Expect(LogType.Error, "TargetRPC Function " + nameof(NetworkBehaviourSendTargetRPCInternalComponent.TargetRPCGenerated) + " called on connection to server");
            comp.CallSendTargetRPCInternal(new NetworkConnectionToServer());
            Assert.That(comp.called, Is.EqualTo(0));

            // set proper connection to client
            identity.connectionToClient = connectionToServer.connectionToClient;

            // isServer needs to be true, otherwise we can't call rpcs
            Assert.That(comp.isServer, Is.True);

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterRpcDelegate(typeof(NetworkBehaviourSendTargetRPCInternalComponent),
                nameof(NetworkBehaviourSendTargetRPCInternalComponent.TargetRPCGenerated),
                NetworkBehaviourSendTargetRPCInternalComponent.TargetRPCGenerated);

            // identity needs to be in spawned dict, otherwise rpc handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call rpc
            comp.CallSendTargetRPCInternal(null);

            // update client's connection so that pending messages are processed
            connectionToServer.Update();

            // rpc should have been called now
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
            ClientScene.Shutdown(); // clear clientscene.readyconnection
            NetworkServer.RemoveLocalConnection();
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void InvokeRPC()
        {
            // add command component
            NetworkBehaviourSendRPCInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendRPCInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterRpcDelegate(typeof(NetworkBehaviourSendRPCInternalComponent),
                nameof(NetworkBehaviourSendRPCInternalComponent.RPCGenerated),
                NetworkBehaviourSendRPCInternalComponent.RPCGenerated);

            // invoke command
            int rpcHash = NetworkBehaviour.GetMethodHash(
                typeof(NetworkBehaviourSendRPCInternalComponent),
                nameof(NetworkBehaviourSendRPCInternalComponent.RPCGenerated));
            comp.InvokeRPC(rpcHash, new NetworkReader(new byte[0]));
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void SendEventInternal()
        {
            // add rpc component
            NetworkBehaviourSendEventInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendEventInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

            // transport is needed by server and client.
            // it needs to be on a gameobject because client.connect enables it,
            // which throws a NRE if not on a gameobject
            GameObject transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

            // calling event before server is active shouldn't work
            LogAssert.Expect(LogType.Warning, "SendEvent no server?");
            comp.CallSendEventInternal();
            Assert.That(comp.called, Is.EqualTo(0));

            // we need to start a server and connect a client in order to be
            // able to send events
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<SpawnMessage>((conn, msg) => {}, false);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // connect host client
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.active, Is.True);

            // get the host connection which already has client->server and
            // server->client set up
            ULocalConnectionToServer connectionToServer = (ULocalConnectionToServer)NetworkClient.connection;

            // set host connection as ready and authenticated
            connectionToServer.isReady = true;
            connectionToServer.isAuthenticated = true;
            connectionToServer.connectionToClient.isReady = true;
            connectionToServer.connectionToClient.isAuthenticated = true;
            connectionToServer.connectionToClient.identity = identity;

            // we need an observer because sendevent sends to ready observers
            identity.OnStartServer(); // creates observers
            identity.observers[connectionToServer.connectionToClient.connectionId] = connectionToServer.connectionToClient;

            identity.netId = 42;

            // set proper connection to client
            identity.connectionToClient = connectionToServer.connectionToClient;

            // isServer needs to be true, otherwise we can't call rpcs
            Assert.That(comp.isServer, Is.True);

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterEventDelegate(
                typeof(NetworkBehaviourSendEventInternalComponent),
                nameof(NetworkBehaviourSendEventInternalComponent.EventGenerated),
                NetworkBehaviourSendEventInternalComponent.EventGenerated);

            // identity needs to be in spawned dict, otherwise event handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call event
            comp.CallSendEventInternal();

            // update client's connection so that pending messages are processed
            connectionToServer.Update();

            // rpc should have been called now
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
            ClientScene.Shutdown(); // clear clientscene.readyconnection
            NetworkServer.RemoveLocalConnection();
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void InvokeSyncEvent()
        {
            // add command component
            NetworkBehaviourSendEventInternalComponent comp = gameObject.AddComponent<NetworkBehaviourSendEventInternalComponent>();
            Assert.That(comp.called, Is.EqualTo(0));

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterEventDelegate(typeof(NetworkBehaviourSendEventInternalComponent),
                nameof(NetworkBehaviourSendEventInternalComponent.EventGenerated),
                NetworkBehaviourSendEventInternalComponent.EventGenerated);

            // invoke command
            int eventHash = NetworkBehaviour.GetMethodHash(
                typeof(NetworkBehaviourSendEventInternalComponent),
                nameof(NetworkBehaviourSendEventInternalComponent.EventGenerated));
            comp.InvokeSyncEvent(eventHash, new NetworkReader(new byte[0]));
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void RegisterDelegateDoesntOverwrite()
        {
            // registerdelegate is protected, but we can use
            // RegisterCommandDelegate which calls RegisterDelegate
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate);

            // registering the exact same one should be fine. it should simply
            // do nothing.
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate);

            // registering the same name with a different callback shouldn't
            // work
            LogAssert.Expect(LogType.Error, "Function " + typeof(NetworkBehaviourDelegateComponent) + "." + nameof(NetworkBehaviourDelegateComponent.Delegate) + " and " + typeof(NetworkBehaviourDelegateComponent) + "." + nameof(NetworkBehaviourDelegateComponent.Delegate2) + " have the same hash.  Please rename one of them");
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate2);

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void GetDelegate()
        {
            // registerdelegate is protected, but we can use
            // RegisterCommandDelegate which calls RegisterDelegate
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate);

            // get handler
            int cmdHash = NetworkBehaviour.GetMethodHash(typeof(NetworkBehaviourDelegateComponent), nameof(NetworkBehaviourDelegateComponent.Delegate));
            NetworkBehaviour.CmdDelegate func = NetworkBehaviour.GetDelegate(cmdHash);
            NetworkBehaviour.CmdDelegate expected = NetworkBehaviourDelegateComponent.Delegate;
            Assert.That(func, Is.EqualTo(expected));

            // invalid hash should return null handler
            NetworkBehaviour.CmdDelegate funcNull = NetworkBehaviour.GetDelegate(1234);
            Assert.That(funcNull, Is.Null);

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualZeroNetIdNullIsTrue()
        {
            // null and identity.netid==0 returns true (=equal)
            //
            // later we should reevaluate if this is so smart or not. might be
            // better to return false here.
            // => we possibly return false so that resync doesn't happen when
            //    GO disappears? or not?
            bool result = emptyBehaviour.SyncVarGameObjectEqual(null, identity.netId);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualNull()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // null should return false
            bool result = emptyBehaviour.SyncVarGameObjectEqual(null, identity.netId);
            Assert.That(result, Is.False);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualZeroNetIdAndGOWithoutIdentityComponentIsTrue()
        {
            // null and identity.netid==0 returns true (=equal)
            //
            // later we should reevaluate if this is so smart or not. might be
            // better to return false here.
            // => we possibly return false so that resync doesn't happen when
            //    GO disappears? or not?
            GameObject go = new GameObject();
            bool result = emptyBehaviour.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualWithoutIdentityComponent()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject without networkidentity component should return false
            GameObject go = new GameObject();
            bool result = emptyBehaviour.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualValidGOWithDifferentNetId()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and netid that is different
            GameObject go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.netId = 43;
            bool result = emptyBehaviour.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualValidGOWithSameNetId()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and netid that is different
            GameObject go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.netId = 42;
            bool result = emptyBehaviour.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualUnspawnedGO()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and 0 netid that is unspawned
            GameObject go = new GameObject();
            go.AddComponent<NetworkIdentity>();
            LogAssert.Expect(LogType.Warning, "SetSyncVarGameObject GameObject " + go + " has a zero netId. Maybe it is not spawned yet?");
            bool result = emptyBehaviour.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
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
