using System;
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

    public class OnNetworkDestroyTestNetworkBehaviour : NetworkBehaviour
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
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { connectCalled = true; }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { disconnectCalled = true; }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
        public void OnConnectedOnlyAllowsGreaterZeroConnectionIdsTest()
        {
            // OnConnected should only allow connectionIds >= 0
            // 0 is for local player
            // <0 is never used

            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
        }

        [Test]
        public void ConnectDuplicateConnectionIdsTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
        public void AddConnectionTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
        }

        [Test]
        public void RemoveConnectionTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
        }

        [Test]
        public void DisconnectAllConnectionsTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
        }

        [Test]
        public void OnDataReceivedTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            NetworkServer.AddConnection(connection);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // serialize a test message into an arraysegment
            TestMessage1 testMessage = new TestMessage1 { IntValue = 13, DoubleValue = 14, StringValue = "15" };
            NetworkWriter writer = new NetworkWriter();
            MessagePacker.Pack(testMessage, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived
            // -> should call NetworkServer.OnDataReceived
            //    -> conn.TransportReceive
            //       -> Handler(CommandMessage)
            Transport.activeTransport.OnServerDataReceived.Invoke(42, segment);

            // was our message handler called now?
            Assert.That(wasReceived, Is.True);
            Assert.That(connectionReceived, Is.EqualTo(connection));
            Assert.That(messageReceived, Is.EqualTo(testMessage));
        }

        [Test]
        public void OnDataReceivedInvalidConnectionIdTest()
        {
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

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
            MessagePacker.Pack(testMessage, writer);
            ArraySegment<byte> segment = writer.ToArraySegment();

            // call transport.OnDataReceived with an invalid connectionId
            // an error log is expected.
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, segment);
            LogAssert.ignoreFailingMessages = false;

            // message handler should never be called
            Assert.That(wasReceived, Is.False);
            Assert.That(connectionReceived, Is.Null);
        }

        [Test]
        public void RegisterUnregisterClearHandlerTest()
        {
            // message handlers that are needed for the test
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);


            // RegisterHandler(conn, msg) variant
            int variant1Called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => { ++variant1Called; }, false);

            // RegisterHandler(msg) variant
            int variant2Called = 0;
            NetworkServer.RegisterHandler<TestMessage2>(msg => { ++variant2Called; }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // add a connection
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42);
            NetworkServer.AddConnection(connection);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // serialize first message, send it to server, check if it was handled
            NetworkWriter writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage1(), writer);
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment());
            Assert.That(variant1Called, Is.EqualTo(1));

            // serialize second message, send it to server, check if it was handled
            writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage2(), writer);
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment());
            Assert.That(variant2Called, Is.EqualTo(1));

            // unregister first handler, send, should fail
            NetworkServer.UnregisterHandler<TestMessage1>();
            writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage1(), writer);
            // log error messages are expected
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment());
            LogAssert.ignoreFailingMessages = false;
            // still 1, not 2
            Assert.That(variant1Called, Is.EqualTo(1));

            // unregister second handler via ClearHandlers to test that one too. send, should fail
            NetworkServer.ClearHandlers();
            // (only add this one to avoid disconnect error)
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            writer = new NetworkWriter();
            MessagePacker.Pack(new TestMessage1(), writer);
            // log error messages are expected
            LogAssert.ignoreFailingMessages = true;
            Transport.activeTransport.OnServerDataReceived.Invoke(42, writer.ToArraySegment());
            LogAssert.ignoreFailingMessages = false;
            // still 1, not 2
            Assert.That(variant2Called, Is.EqualTo(1));
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
            OnNetworkDestroyTestNetworkBehaviour comp = go.AddComponent<OnNetworkDestroyTestNetworkBehaviour>();
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
            // message handlers
            NetworkServer.RegisterHandler<ConnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<DisconnectMessage>((conn, msg) => { }, false);
            NetworkServer.RegisterHandler<ErrorMessage>((conn, msg) => { }, false);

            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);

            // connect
            Transport.activeTransport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // shutdown
            NetworkServer.Shutdown();

            // state cleared?
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.active, Is.False);
        }

        [Test]
        [TestCase(nameof(NetworkServer.SendToAll))]
        [TestCase(nameof(NetworkServer.SendToReady))]
        public void SendCalledWhileNotActive_ShouldGiveWarning(string functionName)
        {
            LogAssert.Expect(LogType.Warning, $"Can not send using NetworkServer.{functionName}<T>(T msg) because NetworkServer is not active");
            bool success;

            switch (functionName)
            {
                case nameof(NetworkServer.SendToAll):
                    success = NetworkServer.SendToAll(new NetworkPingMessage { });
                    Assert.That(success, Is.False);
                    break;
                case nameof(NetworkServer.SendToReady):
                    success = NetworkServer.SendToReady(new NetworkPingMessage { });
                    Assert.That(success, Is.False);
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
    }
}
