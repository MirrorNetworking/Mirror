using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mirror.RemoteCalls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    struct TestMessage1 : NetworkMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        public TestMessage1(int i, string s, double d)
        {
            IntValue = i;
            StringValue = s;
            DoubleValue = d;
        }

        public void Deserialize(NetworkReader reader)
        {
            IntValue = reader.ReadInt32();
            StringValue = reader.ReadString();
            DoubleValue = reader.ReadDouble();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteInt32(IntValue);
            writer.WriteString(StringValue);
            writer.WriteDouble(DoubleValue);
        }
    }

    struct TestMessage2 : NetworkMessage
    {
#pragma warning disable CS0649 // Field is never assigned to
        public int IntValue;
        public string StringValue;
        public double DoubleValue;
#pragma warning restore CS0649 // Field is never assigned to
    }

    public class CommandTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public NetworkConnection senderConnectionInCall;
        // weaver generates this from [Command]
        // but for tests we need to add it manually
        public static void CommandGenerated(NetworkBehaviour comp, NetworkReader reader, NetworkConnection senderConnection)
        {
            ++((CommandTestNetworkBehaviour)comp).called;
            ((CommandTestNetworkBehaviour)comp).senderConnectionInCall = senderConnection;
        }
    }

    public class RpcTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        // weaver generates this from [Rpc]
        // but for tests we need to add it manually
        public static void RpcGenerated(NetworkBehaviour comp, NetworkReader reader, NetworkConnection senderConnection)
        {
            ++((RpcTestNetworkBehaviour)comp).called;
        }
    }

    public class OnStartClientTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public override void OnStartClient() { ++called; }
    }

    public class OnStopClientTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public override void OnStopClient() { ++called; }
    }

    [TestFixture]
    public class NetworkServerTest
    {
        [SetUp]
        public void SetUp()
        {
            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();
        }

        [TearDown]
        public void TearDown()
        {
            // reset all state
            // shutdown should be called before setting activeTransport to null
            NetworkServer.Shutdown();

            GameObject.DestroyImmediate(Transport.activeTransport.gameObject);
            Transport.activeTransport = null;
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

            // listen with maxconnections=1
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first: should work
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // connect second: should fail
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConnectMessageHandlerTest()
        {
            // message handlers
            bool connectCalled = false;
            NetworkServer.OnConnectedEvent = conn => connectCalled = true;

            // listen
            NetworkServer.Listen(1);
            Assert.That(connectCalled, Is.False);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(connectCalled, Is.True);
        }

        [Test]
        public void DisconnectMessageHandlerTest()
        {
            // message handlers
            bool disconnectCalled = false;
            NetworkServer.OnDisconnectedEvent = conn => disconnectCalled = true;

            // listen
            NetworkServer.Listen(1);
            Assert.That(disconnectCalled, Is.False);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(disconnectCalled, Is.False);

            // disconnect
            Transport.activeTransport.OnServerDisconnected.Invoke(42);
            Assert.That(disconnectCalled, Is.True);
        }

        [Test]
        public void ConnectionsDictTest()
        {

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
        }

        [Test]
        public void OnConnectedOnlyAllowsNonZeroConnectionIdsTest()
        {
            // OnConnected should only allow connectionIds >= 0
            // 0 is for local player
            // <0 is never used


            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect 0
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerConnected.Invoke(0);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect == 0 should fail
            Transport.activeTransport.OnServerConnected.Invoke(0);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ConnectDuplicateConnectionIdsTest()
        {

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
        }

        [Test]
        public void SetLocalConnectionTest()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            LocalConnectionToClient localConnection = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // try to overwrite it, which should not work
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            LocalConnectionToClient overwrite = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(overwrite);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void RemoveLocalConnectionTest()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            LocalConnectionToClient localConnection = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // local connection needs a server connection because
            // RemoveLocalConnection calls localConnection.Disconnect
            localConnection.connectionToServer = new LocalConnectionToServer();

            // remove local connection
            NetworkServer.RemoveLocalConnection();
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        [Test]
        public void LocalClientActiveTest()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.localClientActive, Is.False);

            // set local connection
            NetworkServer.SetLocalConnection(new LocalConnectionToClient());
            Assert.That(NetworkServer.localClientActive, Is.True);
        }

        [Test]
        public void AddConnectionTest()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add first connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42, false, 0);
            bool result42 = NetworkServer.AddConnection(conn42);
            Assert.That(result42, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));

            // add second connection
            NetworkConnectionToClient conn43 = new NetworkConnectionToClient(43, false, 0);
            bool result43 = NetworkServer.AddConnection(conn43);
            Assert.That(result43, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);
            Assert.That(NetworkServer.connections[43], Is.EqualTo(conn43));

            // add duplicate connectionId
            NetworkConnectionToClient connDup = new NetworkConnectionToClient(42, false, 0);
            bool resultDup = NetworkServer.AddConnection(connDup);
            Assert.That(resultDup, Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);
            Assert.That(NetworkServer.connections[43], Is.EqualTo(conn43));
        }

        [Test]
        public void RemoveConnectionTest()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42, false, 0);
            bool result42 = NetworkServer.AddConnection(conn42);
            Assert.That(result42, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));

            // remove connection
            bool resultRemove = NetworkServer.RemoveConnection(42);
            Assert.That(resultRemove, Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisconnectAllConnectionsTest()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42, false, 0);
            NetworkServer.AddConnection(conn42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // disconnect all connections
            NetworkServer.DisconnectAllConnections();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisconnectAllTest()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // set local connection
            LocalConnectionToClient localConnection = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42, false, 0);
            NetworkServer.AddConnection(conn42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // disconnect all connections and local connection
            NetworkServer.DisconnectAll();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        [Test]
        public void OnDataReceivedTest()
        {

            // add one custom message handler
            bool wasReceived = false;
            NetworkConnection connectionReceived = null;
            TestMessage1 messageReceived = new TestMessage1();
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) =>
            {
                wasReceived = true;
                connectionReceived = conn;
                messageReceived = msg;
            }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add a connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, false, 0);
            NetworkServer.AddConnection(connection);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // serialize a test message into an arraysegment
            TestMessage1 testMessage = new TestMessage1 { IntValue = 13, DoubleValue = 14, StringValue = "15" };
            NetworkWriter writer = new NetworkWriter();
            MessagePacking.Pack(testMessage, writer);
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
        }

        [Test]
        public void OnDataReceivedInvalidConnectionIdTest()
        {

            // add one custom message handler
            bool wasReceived = false;
            NetworkConnection connectionReceived = null;
            TestMessage1 messageReceived = new TestMessage1();
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) =>
            {
                wasReceived = true;
                connectionReceived = conn;
                messageReceived = msg;
            }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // serialize a test message into an arraysegment
            TestMessage1 testMessage = new TestMessage1 { IntValue = 13, DoubleValue = 14, StringValue = "15" };
            NetworkWriter writer = new NetworkWriter();
            MessagePacking.Pack(testMessage, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived with an invalid connectionId
            // an error log is expected.
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, segment, 0);
            LogAssert.ignoreFailingMessages = false;

            // message handler should never be called
            Assert.That(wasReceived, Is.False);
            Assert.That(connectionReceived, Is.Null);
        }

        [Test]
        public void SetClientReadyAndNotReadyTest()
        {
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
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
            LocalConnectionToClient first = new LocalConnectionToClient();
            first.connectionToServer = new LocalConnectionToServer();
            first.isReady = true;
            NetworkServer.connections[42] = first;

            // add second ready client
            LocalConnectionToClient second = new LocalConnectionToClient();
            second.connectionToServer = new LocalConnectionToServer();
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
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
            NetworkServer.AddConnection(connection);

            // set as authenticated, otherwise readymessage is rejected
            connection.isAuthenticated = true;

            // serialize a ready message into an arraysegment
            ReadyMessage message = new ReadyMessage();
            NetworkWriter writer = new NetworkWriter();
            MessagePacking.Pack(message, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived with the message
            // -> calls NetworkServer.OnClientReadyMessage
            //    -> calls SetClientReady(conn)
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);

            // ready?
            Assert.That(connection.isReady, Is.True);
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
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
            NetworkServer.AddConnection(connection);

            // set as authenticated, otherwise removeplayer is rejected
            connection.isAuthenticated = true;

            // add an identity with two networkbehaviour components
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.netId = 42;
            // for authority check
            identity.connectionToClient = connection;
            CommandTestNetworkBehaviour comp0 = go.AddComponent<CommandTestNetworkBehaviour>();
            Assert.That(comp0.called, Is.EqualTo(0));
            CommandTestNetworkBehaviour comp1 = go.AddComponent<CommandTestNetworkBehaviour>();
            Assert.That(comp1.called, Is.EqualTo(0));
            connection.identity = identity;

            // register the command delegate, otherwise it's not found
            int registeredHash = RemoteCallHelper.RegisterDelegate(typeof(CommandTestNetworkBehaviour),
                nameof(CommandTestNetworkBehaviour.CommandGenerated),
                MirrorInvokeType.Command,
                CommandTestNetworkBehaviour.CommandGenerated,
                true);

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // serialize a removeplayer message into an arraysegment
            CommandMessage message = new CommandMessage
            {
                componentIndex = 0,
                functionHash = RemoteCallHelper.GetMethodHash(typeof(CommandTestNetworkBehaviour), nameof(CommandTestNetworkBehaviour.CommandGenerated)),
                netId = identity.netId,
                payload = new ArraySegment<byte>(new byte[0])
            };
            NetworkWriter writer = new NetworkWriter();
            MessagePacking.Pack(message, writer);
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
            MessagePacking.Pack(message, writer);
            segment = writer.ToArraySegment();
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);

            // was the command called in the second component, not in the first one?
            Assert.That(comp0.called, Is.EqualTo(0));
            Assert.That(comp1.called, Is.EqualTo(1));

            // sending a command without authority should fail
            // (= if connectionToClient is not what we received the data on)
            // set wrong authority
            identity.connectionToClient = new LocalConnectionToClient();
            comp0.called = 0;
            comp1.called = 0;
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);
            Assert.That(comp0.called, Is.EqualTo(0));
            Assert.That(comp1.called, Is.EqualTo(0));
            // restore authority
            identity.connectionToClient = connection;

            // sending a component with wrong netId should fail
            // wrong netid
            message.netId += 1;
            writer = new NetworkWriter();
            // need to serialize the message again with wrong netid
            MessagePacking.Pack(message, writer);
            ArraySegment<byte> segmentWrongNetId = writer.ToArraySegment();
            comp0.called = 0;
            comp1.called = 0;
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segmentWrongNetId, 0);
            Assert.That(comp0.called, Is.EqualTo(0));
            Assert.That(comp1.called, Is.EqualTo(0));

            // clean up
            NetworkIdentity.spawned.Clear();
            RemoteCallHelper.RemoveDelegate(registeredHash);
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
            // for authority check
            //identity.connectionToClient = connection;
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

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
            // set a client handler
            int called = 0;
            connection.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>()
            {
                { MessagePacking.GetId<TestMessage1>(), ((conn, reader, channelId) => ++called) }
            });
            NetworkServer.AddConnection(connection);

            // create a message
            TestMessage1 message = new TestMessage1 { IntValue = 1, DoubleValue = 2, StringValue = "3" };

            // send it to all
            NetworkServer.SendToAll(message);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it send to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void RegisterUnregisterClearHandlerTest()
        {
            // RegisterHandler(conn, msg) variant
            int variant1Called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => { ++variant1Called; }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add a connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, false, 0);
            NetworkServer.AddConnection(connection);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // serialize first message, send it to server, check if it was handled
            NetworkWriter writer = new NetworkWriter();
            MessagePacking.Pack(new TestMessage1(), writer);
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            Assert.That(variant1Called, Is.EqualTo(1));

            // unregister first handler, send, should fail
            NetworkServer.UnregisterHandler<TestMessage1>();
            writer = new NetworkWriter();
            MessagePacking.Pack(new TestMessage1(), writer);
            // log error messages are expected
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            LogAssert.ignoreFailingMessages = false;
            // still 1, not 2
            Assert.That(variant1Called, Is.EqualTo(1));

            // unregister second handler via ClearHandlers to test that one too. send, should fail
            NetworkServer.ClearHandlers();
            // (only add this one to avoid disconnect error)
            writer = new NetworkWriter();
            MessagePacking.Pack(new TestMessage1(), writer);
            // log error messages are expected
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void SendToClientOfPlayer()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            LocalConnectionToClient connection = new LocalConnectionToClient();
            connection.connectionToServer = new LocalConnectionToServer();
            // set a client handler
            int called = 0;
            connection.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>()
            {
                { MessagePacking.GetId<TestMessage1>(), ((conn, reader, channelId) => ++called) }
            });
            NetworkServer.AddConnection(connection);

            // create a message
            TestMessage1 message = new TestMessage1 { IntValue = 1, DoubleValue = 2, StringValue = "3" };

            // create a gameobject and networkidentity
            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.connectionToClient = connection;

            // send it to that player
            NetworkServer.SendToClientOfPlayer(identity, message);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it send to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));

            // clean up
            NetworkServer.Shutdown();
            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            GameObject.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void GetNetworkIdentityShouldFindNetworkIdentity()
        {
            // create a GameObject with NetworkIdentity
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            // GetNetworkIdentity
            bool result = NetworkServer.GetNetworkIdentity(go, out NetworkIdentity value);
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo(identity));

            // clean up
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void GetNetworkIdentityErrorIfNotFound()
        {
            // create a GameObject without NetworkIdentity
            GameObject goWithout = new GameObject("Another Name");

            // GetNetworkIdentity for GO without identity
            LogAssert.Expect(LogType.Error, $"GameObject {goWithout.name} doesn't have NetworkIdentity.");
            bool result = NetworkServer.GetNetworkIdentity(goWithout, out NetworkIdentity value);
            Assert.That(result, Is.False);
            Assert.That(value, Is.Null);

            // clean up
            GameObject.DestroyImmediate(goWithout);
        }

        [Test]
        public void ShowForConnection()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            LocalConnectionToClient connection = new LocalConnectionToClient();
            // required for ShowForConnection
            connection.isReady = true;
            connection.connectionToServer = new LocalConnectionToServer();
            // set a client handler
            int called = 0;
            connection.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>()
            {
                { MessagePacking.GetId<SpawnMessage>(), ((conn, reader, channelId) => ++called) }
            });
            NetworkServer.AddConnection(connection);

            // create a gameobject and networkidentity and some unique values
            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.connectionToClient = connection;

            // call ShowForConnection
            NetworkServer.ShowForConnection(identity, connection);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it sent to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));

            // it shouldn't send it if connection isn't ready, so try that too
            connection.isReady = false;
            NetworkServer.ShowForConnection(identity, connection);
            connection.connectionToServer.Update();
            // not 2 but 1 like before?
            Assert.That(called, Is.EqualTo(1));

            // clean up
            NetworkServer.Shutdown();
            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            GameObject.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void HideForConnection()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add connection
            LocalConnectionToClient connection = new LocalConnectionToClient();
            // required for ShowForConnection
            connection.isReady = true;
            connection.connectionToServer = new LocalConnectionToServer();
            // set a client handler
            int called = 0;
            connection.connectionToServer.SetHandlers(new Dictionary<int, NetworkMessageDelegate>()
            {
                { MessagePacking.GetId<ObjectHideMessage>(), ((conn, reader, channelId) => ++called) }
            });
            NetworkServer.AddConnection(connection);

            // create a gameobject and networkidentity
            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.connectionToClient = connection;

            // call HideForConnection
            NetworkServer.HideForConnection(identity, connection);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it sent to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));

            // clean up
            NetworkServer.Shutdown();
            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            GameObject.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void ValidateSceneObject()
        {
            // create a gameobject and networkidentity
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.sceneId = 42;

            // should be valid as long as it has a sceneId
            Assert.That(NetworkServer.ValidateSceneObject(identity), Is.True);

            // shouldn't be valid with 0 sceneID
            identity.sceneId = 0;
            Assert.That(NetworkServer.ValidateSceneObject(identity), Is.False);
            identity.sceneId = 42;

            // shouldn't be valid for certain hide flags
            go.hideFlags = HideFlags.NotEditable;
            Assert.That(NetworkServer.ValidateSceneObject(identity), Is.False);
            go.hideFlags = HideFlags.HideAndDontSave;
            Assert.That(NetworkServer.ValidateSceneObject(identity), Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void SpawnObjects()
        {
            // create a gameobject and networkidentity that lives in the scene(=has sceneid)
            GameObject go = new GameObject("Test");
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            // lives in the scene from the start
            identity.sceneId = 42;
            // unspawned scene objects are set to inactive before spawning
            go.SetActive(false);

            // create a gameobject that looks like it was instantiated and doesn't live in the scene
            GameObject go2 = new GameObject("Test2");
            NetworkIdentity identity2 = go2.AddComponent<NetworkIdentity>();
            // not a scene object
            identity2.sceneId = 0;
            // unspawned scene objects are set to inactive before spawning
            go2.SetActive(false);

            // calling SpawnObjects while server isn't active should do nothing
            Assert.That(NetworkServer.SpawnObjects(), Is.False);

            // start server
            NetworkServer.Listen(1);

            // calling SpawnObjects while server is active should succeed
            Assert.That(NetworkServer.SpawnObjects(), Is.True);

            // was the scene object activated, and the runtime one wasn't?
            Assert.That(go.activeSelf, Is.True);
            Assert.That(go2.activeSelf, Is.False);

            // clean up
            // reset isServer otherwise Destroy instead of DestroyImmediate is
            // called
            identity.isServer = false;
            identity2.isServer = false;
            NetworkServer.Shutdown();
            GameObject.DestroyImmediate(go);
            GameObject.DestroyImmediate(go2);
            // need to clear spawned list as SpawnObjects adds items to that list
            NetworkIdentity.spawned.Clear();
        }

        [Test]
        public void UnSpawn()
        {
            // create a gameobject and networkidentity that lives in the scene(=has sceneid)
            GameObject go = new GameObject("Test");
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            OnStopClientTestNetworkBehaviour comp = go.AddComponent<OnStopClientTestNetworkBehaviour>();
            // lives in the scene from the start
            identity.sceneId = 42;
            // spawned objects are active
            go.SetActive(true);
            identity.netId = 123;

            // unspawn
            NetworkServer.UnSpawn(go);

            // it should have been reset now
            Assert.That(identity.netId, Is.Zero);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void ShutdownCleanupTest()
        {

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // set local connection
            NetworkServer.SetLocalConnection(new LocalConnectionToClient());
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

        [Test]
        [TestCase(nameof(NetworkServer.SendToAll))]
        [TestCase(nameof(NetworkServer.SendToReady))]
        public void SendCalledWhileNotActive_ShouldGiveWarning(string functionName)
        {
            LogAssert.Expect(LogType.Warning, $"Can not send using NetworkServer.{functionName}<T>(T msg) because NetworkServer is not active");

            switch (functionName)
            {
                case nameof(NetworkServer.SendToAll):
                    NetworkServer.SendToAll(new NetworkPingMessage {});
                    break;
                case nameof(NetworkServer.SendToReady):
                    NetworkServer.SendToReady(new NetworkPingMessage {});
                    break;
                default:
                    Debug.LogError("Could not find function name");
                    break;
            }
        }


        [Test]
        public void NoConnectionsTest_WithNoConnection()
        {
            Assert.That(NetworkServer.NoConnections(), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void NoConnectionsTest_WithConnections()
        {
            NetworkServer.connections.Add(1, null);
            NetworkServer.connections.Add(2, null);
            Assert.That(NetworkServer.NoConnections(), Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));

            NetworkServer.connections.Clear();
        }

        [Test]
        public void NoConnectionsTest_WithHostOnly()
        {
            LocalConnectionToServer connectionToServer = new LocalConnectionToServer();
            LocalConnectionToClient connectionToClient = new LocalConnectionToClient();
            connectionToServer.connectionToClient = connectionToClient;
            connectionToClient.connectionToServer = connectionToServer;

            NetworkServer.SetLocalConnection(connectionToClient);
            NetworkServer.connections.Add(0, connectionToClient);

            Assert.That(NetworkServer.NoConnections(), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            NetworkServer.connections.Clear();
            NetworkServer.RemoveLocalConnection();
        }

        [Test]
        public void NoConnectionsTest_WithHostAndConnection()
        {
            LocalConnectionToServer connectionToServer = new LocalConnectionToServer();
            LocalConnectionToClient connectionToClient = new LocalConnectionToClient();
            connectionToServer.connectionToClient = connectionToClient;
            connectionToClient.connectionToServer = connectionToServer;

            NetworkServer.SetLocalConnection(connectionToClient);
            NetworkServer.connections.Add(0, connectionToClient);
            NetworkServer.connections.Add(1, null);

            Assert.That(NetworkServer.NoConnections(), Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));

            NetworkServer.connections.Clear();
            NetworkServer.RemoveLocalConnection();
        }

        // updating NetworkServer with a null entry in NetworkIdentity.spawned
        // should log a warning.
        [Test]
        public void UpdateDetectsNullEntryInSpawned()
        {
            // start
            NetworkServer.Listen(1);

            // add null
            NetworkIdentity.spawned[42] = null;

            // update
            LogAssert.Expect(LogType.Warning, new Regex("Found 'null' entry in spawned list.*"));
            NetworkServer.NetworkLateUpdate();

            // clean up
            NetworkServer.Shutdown();
        }

        // updating NetworkServer with a null entry in NetworkIdentity.spawned
        // should log a warning.
        // => need extra test because of Unity's custom null check
        [Test]
        public void UpdateDetectsDestroyedEntryInSpawned()
        {
            // start
            NetworkServer.Listen(1);

            // add destroyed
            GameObject go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            NetworkIdentity.spawned[42] = ni;
            GameObject.DestroyImmediate(go);

            // update
            LogAssert.Expect(LogType.Warning, new Regex("Found 'null' entry in spawned list.*"));
            NetworkServer.NetworkLateUpdate();

            // clean up
            NetworkServer.Shutdown();
        }
    }
}
