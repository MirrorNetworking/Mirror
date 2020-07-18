using System;
using System.Collections;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;
using static Mirror.Tests.LocalConnections;
using Object = UnityEngine.Object;

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

        GameObject gameObject;
        NetworkIdentity identity;

        IConnection tconn42;
        IConnection tconn43;

        MockTransport transport;

        TaskCompletionSource<bool> tconn42Receive;
        TaskCompletionSource<bool> tconn43Receive;

        [UnitySetUp]
        public IEnumerator SetUp() => RunAsync(async () =>
        {
            serverGO = new GameObject();
            transport = serverGO.AddComponent<MockTransport>();
            server = serverGO.AddComponent<NetworkServer>();
            client = serverGO.AddComponent<NetworkClient>();
            
            server.transport = transport;
            client.Transport = transport;

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();

            tconn42 = Substitute.For<IConnection>();
            tconn43 = Substitute.For<IConnection>();

            tconn42Receive = new TaskCompletionSource<bool>();
            tconn43Receive = new TaskCompletionSource<bool>();

            Task<bool> task42 = tconn42Receive.Task;
            Task<bool> task43 = tconn42Receive.Task;

            tconn42.ReceiveAsync(null).ReturnsForAnyArgs(task42);
            tconn43.ReceiveAsync(null).ReturnsForAnyArgs(task43);

            await server.ListenAsync();
        });

        [UnityTearDown]
        public IEnumerator TearDown() => RunAsync(async () =>
        {
            Object.Destroy(gameObject);

            // reset all state
            server.Disconnect();

            await WaitFor(() => !server.Active);
            Object.Destroy(serverGO);
        });

        [UnityTest]
        public IEnumerator DisconnectIsActiveTest() => RunAsync(async () =>
        {
            Assert.That(server.Active, Is.True);
            server.Disconnect();

            await WaitFor(() => !server.Active);
            Assert.That(server.Active, Is.False);
        });

        [UnityTest]
        public IEnumerator DisconnectRemoveHandlers() => RunAsync(async () =>
        {
            Assert.That(server.Connected.GetListenerNumber(), Is.EqualTo(1));
            Assert.That(server.Active, Is.True);
            server.Disconnect();

            await WaitFor(() => !server.Active);
            Assert.That(server.Connected.GetListenerNumber(), Is.Zero);
        });

        [UnityTest]
        public IEnumerator ConnectedEventTest() => RunAsync(async () =>
        {
            UnityAction<INetworkConnection> func = Substitute.For<UnityAction<INetworkConnection>>();
            server.Connected.AddListener(func);

            transport.AcceptConnections.Enqueue(tconn42);

            await WaitFor(() => transport.AcceptConnections.Count == 0);

            func.Received().Invoke(Arg.Any<INetworkConnection>());
        });

        [UnityTest]
        public IEnumerator ConnectionTest() => RunAsync(async () =>
        {
            transport.AcceptConnections.Enqueue(tconn42);

            await WaitFor(() => transport.AcceptConnections.Count == 0);

            Assert.That(server.connections, Has.Count.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator MaxConnectionsTest() => RunAsync(async () =>
        {
            // listen with maxconnections=1
            server.MaxConnections = 1;
            Assert.That(server.connections, Is.Empty);

            // connect first: should work
            transport.AcceptConnections.Enqueue(tconn42);
            transport.AcceptConnections.Enqueue(tconn43);
            await WaitFor(() => transport.AcceptConnections.Count == 0);

            Assert.That(server.connections, Has.Count.EqualTo(1));
        });


        [UnityTest]
        public IEnumerator DisconnectMessageHandlerTest() => RunAsync(async () =>
        {
            // subscribe to disconnected
            UnityAction<INetworkConnection> func = Substitute.For<UnityAction<INetworkConnection>>();
            server.Disconnected.AddListener(func);

            // accept a connection and disconnect
            transport.AcceptConnections.Enqueue(tconn42);
            await WaitFor(() => transport.AcceptConnections.Count == 0);

            tconn42Receive.SetResult(false);

            // make sure the callback got invoked
            func.Received().Invoke(Arg.Any<NetworkConnection>());
        });

        [UnityTest]
        public IEnumerator MultipleConnectionsTest() => RunAsync(async () =>
        {
            transport.AcceptConnections.Enqueue(tconn42);
            transport.AcceptConnections.Enqueue(tconn43);

            await WaitFor(() => transport.AcceptConnections.Count == 0);

            Assert.That(server.connections, Has.Count.EqualTo(2));
        });

        [Test]
        public void LocalClientActiveTest()
        {
            Assert.That(server.LocalClientActive, Is.False);

            client.ConnectHost(server);

            Assert.That(server.LocalClientActive, Is.True);

            client.Disconnect();
        }

        [Test]
        public void SetLocalConnectionExceptionTest()
        {
            client.ConnectHost(server);

            Assert.Throws<InvalidOperationException>(() =>
            {
                server.SetLocalConnection(null, null);
            });

            client.Disconnect();
        }

        [Test]
        public void AddConnectionTest()
        {
            // add first connection
            var conn42 = new NetworkConnection(tconn42);
            server.AddConnection(conn42);
            Assert.That(server.connections, Is.EquivalentTo(new[] { conn42 }));

            // add second connection
            var conn43 = new NetworkConnection(tconn43);
            server.AddConnection(conn43);
            Assert.That(server.connections, Is.EquivalentTo(new[] { conn42, conn43 }));

            // add duplicate connectionId
            server.AddConnection(conn42);
            Assert.That(server.connections, Is.EquivalentTo(new[] { conn42, conn43 }));
        }

        [Test]
        public void RemoveConnectionTest()
        {
            var conn42 = new NetworkConnection(tconn42);
            server.AddConnection(conn42);

            // remove connection
            server.RemoveConnection(conn42);
            Assert.That(server.connections, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DisconnectAllConnectionsTest() => RunAsync(async () =>
        {
            transport.AcceptConnections.Enqueue(tconn42);
            transport.AcceptConnections.Enqueue(tconn43);

            await WaitFor(() => transport.AcceptConnections.Count == 0);

            // disconnect all connections and local connection
            server.Disconnect();

            await WaitFor(() => transport.AcceptConnections.Count == 0);

            tconn42.Received().Disconnect();
            tconn43.Received().Disconnect();

            // connections must return EOF
            tconn42Receive.SetResult(false);
            tconn43Receive.SetResult(false);

            Assert.That(server.connections, Is.Empty);
        });

        [Test]
        public void SetClientReadyAndNotReadyTest()
        {
            (_, NetworkConnection connection) = PipedConnections();
            Assert.That(connection.IsReady, Is.False);

            server.SetClientReady(connection);
            Assert.That(connection.IsReady, Is.True);

            server.SetClientNotReady(connection);
            Assert.That(connection.IsReady, Is.False);
        }

        [Test]
        public void SetAllClientsNotReadyTest()
        {
            // add first ready client
            (_, NetworkConnection first) = PipedConnections();
            first.IsReady = true;
            server.connections.Add(first);

            // add second ready client
            (_, NetworkConnection second) = PipedConnections();
            second.IsReady = true;
            server.connections.Add(second);

            // set all not ready
            server.SetAllClientsNotReady();
            Assert.That(first.IsReady, Is.False);
            Assert.That(second.IsReady, Is.False);
        }

        [Test]
        public void ActivateHostSceneCallsOnStartClient()
        {
            // add an identity with a networkbehaviour to .spawned
            var go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.NetId = 42;
            // for authority check
            //identity.connectionToClient = connection;
            OnStartClientTestNetworkBehaviour comp = go.AddComponent<OnStartClientTestNetworkBehaviour>();
            Assert.That(comp.called, Is.EqualTo(0));
            //connection.identity = identity;
            server.Spawned[identity.NetId] = identity;
            identity.OnStartClient.AddListener(comp.OnStartClient);

            // ActivateHostScene
            server.ActivateHostScene();

            // was OnStartClient called for all .spawned networkidentities?
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            server.Spawned.Clear();
            // destroy the test gameobject AFTER server was stopped.
            // otherwise isServer is true in OnDestroy, which means it would try
            // to call Destroy(go). but we need to use DestroyImmediate in
            // Editor
            Object.Destroy(go);
        }

        [Test]
        public void GetNetworkIdentity()
        {
            // create a GameObject with NetworkIdentity
            var go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            // GetNetworkIdentity
            NetworkIdentity value = server.GetNetworkIdentity(go);
            Assert.That(value, Is.EqualTo(identity));
        }

        [Test]
        public void GetNoNetworkIdentity()
        {
            // create a GameObject without NetworkIdentity
            var goWithout = new GameObject();

            // GetNetworkIdentity for GO without identity
            // (error log is expected)
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = server.GetNetworkIdentity(goWithout);
            });

            // clean up
            Object.Destroy(goWithout);
        }

        [Test]
        public void HideForConnection()
        {
            // add connection

            NetworkConnection connectionToClient = Substitute.For<NetworkConnection>((IConnection)null);

            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();

            server.HideForConnection(identity, connectionToClient);

            connectionToClient.Received().Send(Arg.Is<ObjectHideMessage>(msg => msg.netId == identity.NetId));

            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.Destroy(identity.gameObject);
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
            Object.Destroy(go);
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
            identity.NetId = 20;

            // unspawn
            server.UnSpawn(go);

            // it should have been marked for reset now
            Assert.That(identity.NetId, Is.Zero);

            // clean up
            Object.Destroy(go);
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
    }
}
