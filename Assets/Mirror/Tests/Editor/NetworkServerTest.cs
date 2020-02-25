using System;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class CommandTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        // weaver generates this from [Command]
        // but for tests we need to add it manually
        public static void CommandGenerated(NetworkBehaviour comp, NetworkReader reader)
        {
            ++((CommandTestNetworkBehaviour)comp).called;
        }
    }

    public class OnStartClientTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public override void OnStartClient() { ++called; }
    }

    [TestFixture]
    public class NetworkServerTest
    {
        [SetUp]
        public void SetUp()
        {
            Transport.activeTransport = Substitute.For<Transport>();
        }

        [TearDown]
        public void TearDown()
        {
            Transport.activeTransport = null;

            // reset all state
            NetworkServer.Shutdown();
        }

        [Test]
        public void IsActiveTest()
        {
            Assert.That(NetworkServer.active, Is.False);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);
            NetworkServer.Shutdown();
            Assert.That(NetworkServer.active, Is.False);
        }

        [Test]
        public void MaxConnectionsTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen with maxconnections=1
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first: should work
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // connect second: should fail
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void ConnectMessageHandlerTest()
        {
            // message handlers
            bool connectCalled = false;
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { connectCalled = true; }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(connectCalled, Is.False);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(connectCalled, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void DisconnectMessageHandlerTest()
        {
            // message handlers
            bool disconnectCalled = false;
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { disconnectCalled = true; }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(disconnectCalled, Is.False);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(disconnectCalled, Is.False);

            // disconnect
            Transport.activeTransport.OnServerDisconnected.Invoke(42);
            Assert.That(disconnectCalled, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void ConnectionsDictTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);

            // connect second
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);

            // disconnect second
            Transport.activeTransport.OnServerDisconnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);

            // disconnect first
            Transport.activeTransport.OnServerDisconnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void OnConnectedOnlyAllowsGreaterZeroConnectionIdsTest()
        {
            // OnConnected should only allow connectionIds >= 0
            // 0 is for local player
            // <0 is never used

            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect 0
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerConnected.Invoke(0);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect <0
            Transport.activeTransport.OnServerConnected.Invoke(-1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            LogAssert.ignoreFailingMessages = false;

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void ConnectDuplicateConnectionIdsTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            NetworkConnectionToClient original = NetworkServer.connections[42];

            // connect duplicate - shouldn't overwrite first one
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections[42], Is.EqualTo(original));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void SetLocalConnectionTest()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            ULocalConnectionToClient localConnection = new ULocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // try to overwrite it, which should not work
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            ULocalConnectionToClient overwrite = new ULocalConnectionToClient();
            NetworkServer.SetLocalConnection(overwrite);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));
            LogAssert.ignoreFailingMessages = false;

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void RemoveLocalConnectionTest()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            ULocalConnectionToClient localConnection = new ULocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // local connection needs a server connection because
            // RemoveLocalConnection calls localConnection.Disconnect
            localConnection.connectionToServer = new ULocalConnectionToServer();

            // remove local connection
            NetworkServer.RemoveLocalConnection();
            Assert.That(NetworkServer.localConnection, Is.Null);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void LocalClientActiveTest()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.localClientActive, Is.False);

            // set local connection
            NetworkServer.SetLocalConnection(new ULocalConnectionToClient());
            Assert.That(NetworkServer.localClientActive, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void AddConnectionTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add first connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            bool result42 = NetworkServer.AddConnection(conn42);
            Assert.That(result42, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));

            // add second connection
            NetworkConnectionToClient conn43 = new NetworkConnectionToClient(43);
            bool result43 = NetworkServer.AddConnection(conn43);
            Assert.That(result43, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);
            Assert.That(NetworkServer.connections[43], Is.EqualTo(conn43));

            // add duplicate connectionId
            NetworkConnectionToClient connDup = new NetworkConnectionToClient(42);
            bool resultDup = NetworkServer.AddConnection(connDup);
            Assert.That(resultDup, Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);
            Assert.That(NetworkServer.connections[43], Is.EqualTo(conn43));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void RemoveConnectionTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            bool result42 = NetworkServer.AddConnection(conn42);
            Assert.That(result42, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));

            // remove connection
            bool resultRemove = NetworkServer.RemoveConnection(42);
            Assert.That(resultRemove, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void DisconnectAllConnectionsTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            NetworkServer.AddConnection(conn42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // disconnect all connections
            NetworkServer.DisconnectAllConnections();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void DisconnectAllTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // set local connection
            ULocalConnectionToClient localConnection = new ULocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            NetworkServer.AddConnection(conn42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // disconnect all connections and local connection
            NetworkServer.DisconnectAll();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.localConnection, Is.Null);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void OnDataReceivedTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // add one custom message handler
            bool wasReceived = false;
            NetworkConnection connectionReceived = null;
            TestMessage messageReceived = new TestMessage();
            NetworkServer.RegisterHandler<TestMessage>((conn, msg) => {
                wasReceived = true;
                connectionReceived = conn;
                messageReceived = msg;
            }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add a connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            NetworkServer.AddConnection(connection);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // serialize a test message into an arraysegment
            TestMessage testMessage = new TestMessage{IntValue = 13, DoubleValue = 14, StringValue = "15"};
            NetworkWriter writer = new NetworkWriter();
            MessagePacker.Pack(testMessage, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived
            // -> should call NetworkServer.OnDataReceived
            //    -> conn.TransportReceive
            //       -> Handler(CommandMessage)
            Transport.activeTransport.OnServerDataReceived.Invoke(42, segment, 0);

            // was our message handler called now?
            Assert.That(wasReceived, Is.True);
            Assert.That(connectionReceived, Is.EqualTo(connection));
            Assert.That(messageReceived, Is.EqualTo(testMessage));

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void OnDataReceivedInvalidConnectionIdTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // add one custom message handler
            bool wasReceived = false;
            NetworkConnection connectionReceived = null;
            TestMessage messageReceived = new TestMessage();
            NetworkServer.RegisterHandler<TestMessage>((conn, msg) => {
                wasReceived = true;
                connectionReceived = conn;
                messageReceived = msg;
            }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // serialize a test message into an arraysegment
            TestMessage testMessage = new TestMessage{IntValue = 13, DoubleValue = 14, StringValue = "15"};
            NetworkWriter writer = new NetworkWriter();
            MessagePacker.Pack(testMessage, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived with an invalid connectionId
            // an error log is expected.
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, segment, 0);
            LogAssert.ignoreFailingMessages = false;

            // message handler should never be called
            Assert.That(wasReceived, Is.False);
            Assert.That(connectionReceived, Is.Null);

            // shutdown
            NetworkServer.Shutdown();
        }

        [Test]
        public void SetClientReadyAndNotReadyTest()
        {
            ULocalConnectionToClient connection = new ULocalConnectionToClient();
            connection.connectionToServer = new ULocalConnectionToServer();
            Assert.That(connection.isReady, Is.False);

            NetworkServer.SetClientReady(connection);
            Assert.That(connection.isReady, Is.True);

            NetworkServer.SetClientNotReady(connection);
            Assert.That(connection.isReady, Is.False);
        }

        [Test]
        public void SetAllClientsNotReadyTest()
        {
            // add first ready client
            ULocalConnectionToClient first = new ULocalConnectionToClient();
            first.connectionToServer = new ULocalConnectionToServer();
            first.isReady = true;
            NetworkServer.connections[42] = first;

            // add second ready client
            ULocalConnectionToClient second = new ULocalConnectionToClient();
            second.connectionToServer = new ULocalConnectionToServer();
            second.isReady = true;
            NetworkServer.connections[43] = second;

            // set all not ready
            NetworkServer.SetAllClientsNotReady();
            Assert.That(first.isReady, Is.False);
            Assert.That(second.isReady, Is.False);
        }

        [Test]
        public void ReadyMessageSetsClientReadyTest()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            ULocalConnectionToClient connection = new ULocalConnectionToClient();
            connection.connectionToServer = new ULocalConnectionToServer();
            NetworkServer.AddConnection(connection);

            // set as authenticated, otherwise readymessage is rejected
            connection.isAuthenticated = true;

            // serialize a ready message into an arraysegment
            ReadyMessage message = new ReadyMessage();
            NetworkWriter writer = new NetworkWriter();
            MessagePacker.Pack(message, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived with the message
            // -> calls NetworkServer.OnClientReadyMessage
            //    -> calls SetClientReady(conn)
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);

            // ready?
            Assert.That(connection.isReady, Is.True);

            // shutdown
            NetworkServer.Shutdown();
        }

        // this runs a command all the way:
        //   byte[]->transport->server->identity->component
        [Test]
        public void CommandMessageCallsCommandTest()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            ULocalConnectionToClient connection = new ULocalConnectionToClient();
            connection.connectionToServer = new ULocalConnectionToServer();
            NetworkServer.AddConnection(connection);

            // set as authenticated, otherwise removeplayer is rejected
            connection.isAuthenticated = true;

            // add an identity with two networkbehaviour components
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.netId = 42;
            identity.connectionToClient = connection; // for authority check
            CommandTestNetworkBehaviour comp0 = go.AddComponent<CommandTestNetworkBehaviour>();
            Assert.That(comp0.called, Is.EqualTo(0));
            CommandTestNetworkBehaviour comp1 = go.AddComponent<CommandTestNetworkBehaviour>();
            Assert.That(comp1.called, Is.EqualTo(0));
            connection.identity = identity;

            // register the command delegate, otherwise it's not found
            NetworkBehaviour.RegisterCommandDelegate(typeof(CommandTestNetworkBehaviour), nameof(CommandTestNetworkBehaviour.CommandGenerated), CommandTestNetworkBehaviour.CommandGenerated);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // serialize a removeplayer message into an arraysegment
            CommandMessage message = new CommandMessage {
                componentIndex = 0,
                functionHash = NetworkBehaviour.GetMethodHash(typeof(CommandTestNetworkBehaviour), nameof(CommandTestNetworkBehaviour.CommandGenerated)),
                netId = identity.netId,
                payload = new ArraySegment<byte>(new byte[0])
            };
            NetworkWriter writer = new NetworkWriter();
            MessagePacker.Pack(message, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived with the message
            // -> calls NetworkServer.OnRemovePlayerMessage
            //    -> destroys conn.identity and sets it to null
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);

            // was the command called in the first component, not in the second one?
            Assert.That(comp0.called, Is.EqualTo(1));
            Assert.That(comp1.called, Is.EqualTo(0));

            //  send another command for the second component
            comp0.called = 0;
            message.componentIndex = 1;
            writer = new NetworkWriter();
            MessagePacker.Pack(message, writer);
            segment = writer.ToArraySegment();
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);

            // was the command called in the second component, not in the first one?
            Assert.That(comp0.called, Is.EqualTo(0));
            Assert.That(comp1.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            NetworkBehaviour.ClearDelegates();
            NetworkServer.Shutdown();
            // destroy the test gameobject AFTER server was stopped.
            // otherwise isServer is true in OnDestroy, which means it would try
            // to call Destroy(go). but we need to use DestroyImmediate in
            // Editor
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void ActivateHostSceneCallsOnStartClient()
        {
            // add an identity with a networkbehaviour to .spawned
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.netId = 42;
            //identity.connectionToClient = connection; // for authority check
            OnStartClientTestNetworkBehaviour comp = go.AddComponent<OnStartClientTestNetworkBehaviour>();
            Assert.That(comp.called, Is.EqualTo(0));
            //connection.identity = identity;
            NetworkIdentity.spawned[identity.netId] = identity;

            // ActivateHostScene
            NetworkServer.ActivateHostScene();

            // was OnStartClient called for all .spawned networkidentities?
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            NetworkServer.Shutdown();
            // destroy the test gameobject AFTER server was stopped.
            // otherwise isServer is true in OnDestroy, which means it would try
            // to call Destroy(go). but we need to use DestroyImmediate in
            // Editor
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void SendToAllTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            ULocalConnectionToClient connection = new ULocalConnectionToClient();
            connection.connectionToServer = new ULocalConnectionToServer();
            // set a client handler
            int called = 0;
            TestMessage received = new TestMessage();
            connection.connectionToServer.SetHandlers(new Dictionary<int,NetworkMessageDelegate>()
            {
                { MessagePacker.GetId<TestMessage>(), (msg => ++called) }
            });
            NetworkServer.AddConnection(connection);

            // create a message
            TestMessage message = new TestMessage{ IntValue = 1, DoubleValue = 2, StringValue = "3" };

            // send it to all
            bool result = NetworkServer.SendToAll(message);
            Assert.That(result, Is.True);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it send to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));

            // clean up
            NetworkServer.Shutdown();
        }

        [Test]
        public void ShutdownCleanupTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => {}, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => {}, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // set local connection
            NetworkServer.SetLocalConnection(new ULocalConnectionToClient());
            Assert.That(NetworkServer.localClientActive, Is.True);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // shutdown
            NetworkServer.Shutdown();

            // state cleared?
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.active, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
            Assert.That(NetworkServer.localClientActive, Is.False);
        }
    }
}
