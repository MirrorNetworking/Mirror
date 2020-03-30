using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    struct TestMessage : IMessageBase
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        public TestMessage(int i, string s, double d)
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

    struct WovenTestMessage : IMessageBase
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        public void Deserialize(NetworkReader reader) { }
        public void Serialize(NetworkWriter writer) { }
    }

    public class OnStartClientTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public void OnStartClient()
        {
            ++called;
        }
    }

    public class OnNetworkDestroyTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public void OnNetworkDestroy()
        {
            ++called;
        }
    }

    [TestFixture]
    public class NetworkServerTest
    {
        NetworkServer server;
        GameObject serverGO;
        NetworkClient client;
        GameObject clientGO;

        GameObject gameObject;
        NetworkIdentity identity;

        [SetUp]
        public void SetUp()
        {
            Transport.activeTransport = Substitute.For<Transport>();
            serverGO = new GameObject();
            server = serverGO.AddComponent<NetworkServer>();

            clientGO = new GameObject();
            client = clientGO.AddComponent<NetworkClient>();

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();

        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);

            // reset all state
            server.Shutdown();
            Object.DestroyImmediate(serverGO);
            Object.DestroyImmediate(clientGO);
            Transport.activeTransport = null;


        }

        [Test]
        public void IsActiveTest()
        {
            Assert.That(server.active, Is.False);
            server.Listen();
            Assert.That(server.active, Is.True);
            server.Shutdown();
            Assert.That(server.active, Is.False);
        }


        [Test]
        public void ConnectionEventTest()
        {
            // listen with maxconnections=1
            server.MaxConnections = 1;

            UnityAction<NetworkConnectionToClient> func = Substitute.For<UnityAction<NetworkConnectionToClient>>();

            server.Connected.AddListener(func);
            server.Listen();

            // connect first: should work
            Transport.activeTransport.OnServerConnected.Invoke(42);

            func.Received().Invoke(Arg.Is<NetworkConnectionToClient>(conn => conn.connectionId == 42));

            if (server.connections.TryGetValue(42, out NetworkConnectionToClient conn1))
                Assert.That(conn1.isAuthenticated, Is.True);
        }

        [Test]
        public void MaxConnectionsTest()
        {
            // listen with maxconnections=1
            server.MaxConnections = 1;
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // connect first: should work
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(server.connections.Count, Is.EqualTo(1));

            // connect second: should fail
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(server.connections.Count, Is.EqualTo(1));
        }

        
        [Test]
        public void DisconnectMessageHandlerTest()
        {
            // message handlers
            bool disconnectCalled = false;
            server.Disconnected.AddListener(conn => { disconnectCalled = true; });

            // listen
            server.Listen();
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
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // connect first
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(server.connections.Count, Is.EqualTo(1));
            Assert.That(server.connections.ContainsKey(42), Is.True);

            // connect second
            Transport.activeTransport.OnServerConnected.Invoke(43);
            Assert.That(server.connections.Count, Is.EqualTo(2));
            Assert.That(server.connections.ContainsKey(43), Is.True);

            // disconnect second
            Transport.activeTransport.OnServerDisconnected.Invoke(43);
            Assert.That(server.connections.Count, Is.EqualTo(1));
            Assert.That(server.connections.ContainsKey(42), Is.True);

            // disconnect first
            Transport.activeTransport.OnServerDisconnected.Invoke(42);
            Assert.That(server.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnConnectedOnlyAllowsGreaterZeroConnectionIdsTest()
        {
            // OnConnected should only allow connectionIds >= 0
            // 0 is for local player
            // <0 is never used

            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // connect 0
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerConnected.Invoke(0);
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // connect <0
            Transport.activeTransport.OnServerConnected.Invoke(-1);
            Assert.That(server.connections.Count, Is.EqualTo(0));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ConnectDuplicateConnectionIdsTest()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // connect first
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(server.connections.Count, Is.EqualTo(1));
            NetworkConnectionToClient original = server.connections[42];

            // connect duplicate - shouldn't overwrite first one
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(server.connections.Count, Is.EqualTo(1));
            Assert.That(server.connections[42], Is.EqualTo(original));
        }

        [Test]
        public void LocalClientActiveTest()
        {
            // listen
            server.Listen();
            Assert.That(server.LocalClientActive, Is.False);

            client.ConnectHost(server);

            Assert.That(server.LocalClientActive, Is.True);

            client.Disconnect();
        }

        [Test]
        public void AddConnectionTest()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add first connection
            var conn42 = new NetworkConnectionToClient(42);
            bool result42 = server.AddConnection(conn42);
            Assert.That(result42, Is.True);
            Assert.That(server.connections.Count, Is.EqualTo(1));
            Assert.That(server.connections.ContainsKey(42), Is.True);
            Assert.That(server.connections[42], Is.EqualTo(conn42));

            // add second connection
            var conn43 = new NetworkConnectionToClient(43);
            bool result43 = server.AddConnection(conn43);
            Assert.That(result43, Is.True);
            Assert.That(server.connections.Count, Is.EqualTo(2));
            Assert.That(server.connections.ContainsKey(42), Is.True);
            Assert.That(server.connections[42], Is.EqualTo(conn42));
            Assert.That(server.connections.ContainsKey(43), Is.True);
            Assert.That(server.connections[43], Is.EqualTo(conn43));

            // add duplicate connectionId
            var connDup = new NetworkConnectionToClient(42);
            bool resultDup = server.AddConnection(connDup);
            Assert.That(resultDup, Is.False);
            Assert.That(server.connections.Count, Is.EqualTo(2));
            Assert.That(server.connections.ContainsKey(42), Is.True);
            Assert.That(server.connections[42], Is.EqualTo(conn42));
            Assert.That(server.connections.ContainsKey(43), Is.True);
            Assert.That(server.connections[43], Is.EqualTo(conn43));
        }

        [Test]
        public void RemoveConnectionTest()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add connection
            var conn42 = new NetworkConnectionToClient(42);
            bool result42 = server.AddConnection(conn42);
            Assert.That(result42, Is.True);
            Assert.That(server.connections.Count, Is.EqualTo(1));
            Assert.That(server.connections.ContainsKey(42), Is.True);
            Assert.That(server.connections[42], Is.EqualTo(conn42));

            // remove connection
            bool resultRemove = server.RemoveConnection(42);
            Assert.That(resultRemove, Is.True);
            Assert.That(server.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisconnectAllTest()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            client.ConnectHost(server);
            Assert.That(server.localConnection, Is.Not.Null);

            // add connection
            var conn42 = new NetworkConnectionToClient(42);
            server.AddConnection(conn42);
            Assert.That(server.connections.Count, Is.EqualTo(1));

            // disconnect all connections and local connection
            server.DisconnectAll();
            Assert.That(server.connections.Count, Is.EqualTo(0));
            Assert.That(server.localConnection, Is.Null);
        }

        [Test]
        public void OnDataReceivedTest()
        {
            // add one custom message handler
            bool wasReceived = false;
            NetworkConnection connectionReceived = null;
            var messageReceived = new TestMessage();

            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add a connection
            var connection = new NetworkConnectionToClient(42);

            connection.RegisterHandler<NetworkConnectionToClient, TestMessage>((conn, msg) =>
            {
                wasReceived = true;
                connectionReceived = conn;
                messageReceived = msg;
            }, false);

            server.AddConnection(connection);
            Assert.That(server.connections.Count, Is.EqualTo(1));

            // serialize a test message into an arraysegment
            var testMessage = new TestMessage { IntValue = 13, DoubleValue = 14, StringValue = "15" };
            var writer = new NetworkWriter();
            MessagePacker.Pack(testMessage, writer);
            var segment = writer.ToArraySegment();

            // call transport.OnDataReceived
            // -> should call server.OnDataReceived
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

            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // serialize a test message into an arraysegment
            var testMessage = new TestMessage { IntValue = 13, DoubleValue = 14, StringValue = "15" };
            var writer = new NetworkWriter();
            MessagePacker.Pack(testMessage, writer);
            var segment = writer.ToArraySegment();

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
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            Assert.That(connection.isReady, Is.False);

            server.SetClientReady(connection);
            Assert.That(connection.isReady, Is.True);

            server.SetClientNotReady(connection);
            Assert.That(connection.isReady, Is.False);
        }

        [Test]
        public void SetAllClientsNotReadyTest()
        {
            // add first ready client
            (_, var first) = ULocalConnectionToClient.CreateLocalConnections();
            first.isReady = true;
            server.connections[42] = first;

            // add second ready client
            (_, var second) = ULocalConnectionToClient.CreateLocalConnections();
            second.isReady = true;
            server.connections[43] = second;

            // set all not ready
            server.SetAllClientsNotReady();
            Assert.That(first.isReady, Is.False);
            Assert.That(second.isReady, Is.False);
        }

        [Test]
        public void ReadyMessageSetsClientReadyTest()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add connection
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            server.AddConnection(connection);

            // set as authenticated, otherwise readymessage is rejected
            connection.isAuthenticated = true;

            // serialize a ready message into an arraysegment
            var message = new ReadyMessage();
            var writer = new NetworkWriter();
            MessagePacker.Pack(message, writer);
            var segment = writer.ToArraySegment();

            // call transport.OnDataReceived with the message
            // -> calls server.OnClientReadyMessage
            //    -> calls SetClientReady(conn)
            Transport.activeTransport.OnServerDataReceived.Invoke(0, segment, 0);

            // ready?
            Assert.That(connection.isReady, Is.True);
        }

        [Test]
        public void ActivateHostSceneCallsOnStartClient()
        {
            // add an identity with a networkbehaviour to .spawned
            var go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.netId = 42;
            // for authority check
            //identity.connectionToClient = connection;
            OnStartClientTestNetworkBehaviour comp = go.AddComponent<OnStartClientTestNetworkBehaviour>();
            Assert.That(comp.called, Is.EqualTo(0));
            //connection.identity = identity;
            server.spawned[identity.netId] = identity;
            identity.OnStartClient.AddListener(comp.OnStartClient);

            // ActivateHostScene
            server.ActivateHostScene();

            // was OnStartClient called for all .spawned networkidentities?
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            server.spawned.Clear();
            // destroy the test gameobject AFTER server was stopped.
            // otherwise isServer is true in OnDestroy, which means it would try
            // to call Destroy(go). but we need to use DestroyImmediate in
            // Editor
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SendToAllTest()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add connection
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            connection.isAuthenticated = true;
            connection.connectionToServer.isAuthenticated = true;
            // set a client handler
            int called = 0;
            connection.connectionToServer.RegisterHandler<TestMessage>(msg => ++called);

            server.AddConnection(connection);

            // create a message
            var message = new TestMessage { IntValue = 1, DoubleValue = 2, StringValue = "3" };

            // send it to all
            bool result = server.SendToAll(message);
            Assert.That(result, Is.True);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it send to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void RegisterUnregisterClearHandlerTest()
        {

            // listen
            server.Listen();
            // add a connection
            var connection = new NetworkConnectionToClient(42);
            server.AddConnection(connection);

            // RegisterHandler(conn, msg) variant
            int variant1Called = 0;
            connection.RegisterHandler<NetworkConnectionToClient, TestMessage>((conn, msg) => { ++variant1Called; }, false);

            // RegisterHandler(msg) variant
            int variant2Called = 0;
            connection.RegisterHandler<WovenTestMessage>(msg => { ++variant2Called; }, false);

            // serialize first message, send it to server, check if it was handled
            var writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage(), writer);
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            Assert.That(variant1Called, Is.EqualTo(1));

            // serialize second message, send it to server, check if it was handled
            writer = new NetworkWriter();
            var wovenMessage = new WovenTestMessage
            {
                IntValue = 1,
                DoubleValue = 1.0,
                StringValue = "hello"
            };

            MessagePacker.Pack(wovenMessage, writer);
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            Assert.That(variant2Called, Is.EqualTo(1));

            // unregister first handler, send, should fail
            connection.UnregisterHandler<TestMessage>();
            writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage(), writer);
            // log error messages are expected
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            LogAssert.ignoreFailingMessages = false;
            // still 1, not 2
            Assert.That(variant1Called, Is.EqualTo(1));

            // unregister second handler via ClearHandlers to test that one too. send, should fail
            connection.ClearHandlers();
            // (only add this one to avoid disconnect error)
            writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage(), writer);
            // log error messages are expected
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment(), 0);
            LogAssert.ignoreFailingMessages = false;
            // still 1, not 2
            Assert.That(variant2Called, Is.EqualTo(1));
        }

        [Test]
        public void SendToClientOfPlayer()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add connection
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            connection.isAuthenticated = true;
            connection.connectionToServer.isAuthenticated = true;

            // set a client handler
            int called = 0;
            connection.connectionToServer.RegisterHandler<TestMessage>(msg => ++called);
            server.AddConnection(connection);

            // create a message
            var message = new TestMessage { IntValue = 1, DoubleValue = 2, StringValue = "3" };

            // create a gameobject and networkidentity
            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.connectionToClient = connection;

            // send it to that player
            server.SendToClientOfPlayer(identity, message);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it send to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));
            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void GetNetworkIdentity()
        {
            // create a GameObject with NetworkIdentity
            var go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            // GetNetworkIdentity
            bool result = server.GetNetworkIdentity(go, out NetworkIdentity value);
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo(identity));

            // create a GameObject without NetworkIdentity
            var goWithout = new GameObject();

            // GetNetworkIdentity for GO without identity
            // (error log is expected)
            LogAssert.ignoreFailingMessages = true;
            result = server.GetNetworkIdentity(goWithout, out NetworkIdentity valueNull);
            Assert.That(result, Is.False);
            Assert.That(valueNull, Is.Null);
            LogAssert.ignoreFailingMessages = false;

            // clean up
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(goWithout);
        }

        [Test]
        public void ShowForConnection()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add connection
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            connection.isReady = true;
            connection.isAuthenticated = true;
            connection.connectionToServer.isAuthenticated = true;
            // set a client handler
            int called = 0;
            connection.connectionToServer.RegisterHandler<SpawnMessage>(msg => ++called);
            server.AddConnection(connection);

            // create a gameobject and networkidentity and some unique values
            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.connectionToClient = connection;

            // call ShowForConnection
            server.ShowForConnection(identity, connection);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it sent to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));

            // it shouldn't send it if connection isn't ready, so try that too
            connection.isReady = false;
            server.ShowForConnection(identity, connection);
            connection.connectionToServer.Update();
            // not 2 but 1 like before?
            Assert.That(called, Is.EqualTo(1));
            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void HideForConnection()
        {
            // listen
            server.Listen();
            Assert.That(server.connections.Count, Is.EqualTo(0));

            // add connection
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            connection.isReady = true;
            connection.isAuthenticated = true;
            connection.connectionToServer.isAuthenticated = true;
            // set a client handler
            int called = 0;
            connection.connectionToServer.RegisterHandler<ObjectHideMessage>(msg => ++called);
            server.AddConnection(connection);

            // create a gameobject and networkidentity
            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();
            identity.connectionToClient = connection;

            // call HideForConnection
            server.HideForConnection(identity, connection);

            // update local connection once so that the incoming queue is processed
            connection.connectionToServer.Update();

            // was it sent to and handled by the connection?
            Assert.That(called, Is.EqualTo(1));
            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void ValidateSceneObject()
        {
            // create a gameobject and networkidentity
            var go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.sceneId = 42;

            // should be valid as long as it has a sceneId
            Assert.That(server.ValidateSceneObject(identity), Is.True);

            // shouldn't be valid with 0 sceneID
            identity.sceneId = 0;
            Assert.That(server.ValidateSceneObject(identity), Is.False);
            identity.sceneId = 42;

            // shouldn't be valid for certain hide flags
            go.hideFlags = HideFlags.NotEditable;
            Assert.That(server.ValidateSceneObject(identity), Is.False);
            go.hideFlags = HideFlags.HideAndDontSave;
            Assert.That(server.ValidateSceneObject(identity), Is.False);

            // clean up
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SpawnObjects()
        {
            // create a gameobject and networkidentity that lives in the scene(=has sceneid)
            var go = new GameObject("Test");
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            // lives in the scene from the start
            identity.sceneId = 42;
            // unspawned scene objects are set to inactive before spawning
            go.SetActive(false);

            // create a gameobject that looks like it was instantiated and doesn't live in the scene
            var go2 = new GameObject("Test2");
            NetworkIdentity identity2 = go2.AddComponent<NetworkIdentity>();
            // not a scene object
            identity2.sceneId = 0;
            // unspawned scene objects are set to inactive before spawning
            go2.SetActive(false);

            // calling SpawnObjects while server isn't active should do nothing
            Assert.That(server.SpawnObjects(), Is.False);

            // start server
            server.Listen();

            // calling SpawnObjects while server is active should succeed
            Assert.That(server.SpawnObjects(), Is.True);

            // was the scene object activated, and the runtime one wasn't?
            Assert.That(go.activeSelf, Is.True);
            Assert.That(go2.activeSelf, Is.False);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void UnSpawn()
        {
            // create a gameobject and networkidentity that lives in the scene(=has sceneid)
            var go = new GameObject("Test");
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            go.AddComponent<OnNetworkDestroyTestNetworkBehaviour>();
            // lives in the scene from the start
            identity.sceneId = 42;
            // spawned objects are active
            go.SetActive(true);
            Assert.That(identity.IsMarkedForReset(), Is.False);

            // unspawn
            server.UnSpawn(go);

            // it should have been marked for reset now
            Assert.That(identity.IsMarkedForReset(), Is.True);

            // clean up
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ClearDirtyComponentsDirtyBits()
        {
            // create a networkidentity and add some components
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
            // create a networkidentity and add some components
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
        public void ShutdownCleanupTest()
        {
            // listen
            server.Listen();
            Assert.That(server.active, Is.True);


            client.ConnectHost(server);
            // set local connection
            Assert.That(server.LocalClientActive, Is.True);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(server.connections.Count, Is.EqualTo(1));

            server.DisconnectAll();
            server.Shutdown();

            // state cleared?
            Assert.That(server.connections.Count, Is.EqualTo(0));
            Assert.That(server.active, Is.False);
            Assert.That(server.localConnection, Is.Null);
            Assert.That(server.LocalClientActive, Is.False);
        }
    }
}
