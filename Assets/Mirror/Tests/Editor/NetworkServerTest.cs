using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    struct TestMessage1 : NetworkMessage {}

    struct VariableSizedMessage : NetworkMessage
    {
        // weaver serializes byte[] wit WriteBytesAndSize
        public byte[] payload;
        // so payload := size - 4
        // then the message is exactly maxed size.
        //
        // NOTE: we have a LargerMaxMessageSize test which guarantees that
        //       variablesized + 1 is exactly transport.max + 1
        public VariableSizedMessage(int size) => payload = new byte[size - 4];
    }

    public class CommandTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;

        [Command]
        public void TestCommand() => ++called;
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
        public override void OnStartClient() => ++called;
    }

    public class OnStopClientTestNetworkBehaviour : NetworkBehaviour
    {
        // counter to make sure that it's called exactly once
        public int called;
        public override void OnStopClient() => ++called;
    }

    [TestFixture]
    public class NetworkServerTest : MirrorEditModeTest
    {
        [Test]
        public void IsActive()
        {
            Assert.That(NetworkServer.active, Is.False);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);
            NetworkServer.Shutdown();
            Assert.That(NetworkServer.active, Is.False);
        }

        [Test]
        public void MaxConnections()
        {
            // listen with maxconnections=1
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first: should work
            transport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // connect second: should fail
            transport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnConnectedEventCalled()
        {
            // message handlers
            bool connectCalled = false;
            NetworkServer.OnConnectedEvent = conn => connectCalled = true;

            // listen & connect
            NetworkServer.Listen(1);
            transport.OnServerConnected.Invoke(42);
            Assert.That(connectCalled, Is.True);
        }

        [Test]
        public void OnDisconnectedEventCalled()
        {
            // message handlers
            bool disconnectCalled = false;
            NetworkServer.OnDisconnectedEvent = conn => disconnectCalled = true;

            // listen & connect
            NetworkServer.Listen(1);
            transport.OnServerConnected.Invoke(42);

            // disconnect
            transport.OnServerDisconnected.Invoke(42);
            Assert.That(disconnectCalled, Is.True);
        }

        [Test]
        public void ConnectionsDict()
        {
            // listen
            NetworkServer.Listen(2);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));

            // connect first
            transport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);

            // connect second
            transport.OnServerConnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections.ContainsKey(43), Is.True);

            // disconnect second
            transport.OnServerDisconnected.Invoke(43);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections.ContainsKey(42), Is.True);

            // disconnect first
            transport.OnServerDisconnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnConnectedOnlyAllowsNonZeroConnectionIds()
        {
            // OnConnected should only allow connectionIds >= 0
            // 0 is for local player
            // <0 is never used

            // listen
            NetworkServer.Listen(2);

            // connect with connectionId == 0 should fail
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            transport.OnServerConnected.Invoke(0);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ConnectDuplicateConnectionIds()
        {
            // listen
            NetworkServer.Listen(2);

            // connect first
            transport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            NetworkConnectionToClient original = NetworkServer.connections[42];

            // connect duplicate - shouldn't overwrite first one
            transport.OnServerConnected.Invoke(42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections[42], Is.EqualTo(original));
        }

        [Test]
        public void SetLocalConnection()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            LocalConnectionToClient localConnection = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));
        }

        [Test]
        public void SetLocalConnection_PreventsOverwrite()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            LocalConnectionToClient localConnection = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);

            // try to overwrite it, which should not work
            // (it will show an error message, which is expected)
            LogAssert.ignoreFailingMessages = true;
            NetworkServer.SetLocalConnection(new LocalConnectionToClient());
            Assert.That(NetworkServer.localConnection, Is.EqualTo(localConnection));
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void RemoveLocalConnection()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            CreateLocalConnectionPair(out LocalConnectionToClient connectionToClient, out _);
            NetworkServer.SetLocalConnection(connectionToClient);

            // remove local connection
            NetworkServer.RemoveLocalConnection();
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        [Test]
        public void LocalClientActive()
        {
            // listen
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.localClientActive, Is.False);

            // set local connection
            NetworkServer.SetLocalConnection(new LocalConnectionToClient());
            Assert.That(NetworkServer.localClientActive, Is.True);
        }

        [Test]
        public void AddConnection()
        {
            // listen
            NetworkServer.Listen(1);

            // add first connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            Assert.That(NetworkServer.AddConnection(conn42), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));

            // add second connection
            NetworkConnectionToClient conn43 = new NetworkConnectionToClient(43);
            Assert.That(NetworkServer.AddConnection(conn43), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));
            Assert.That(NetworkServer.connections[43], Is.EqualTo(conn43));
        }

        [Test]
        public void AddConnection_PreventsDuplicates()
        {
            // listen
            NetworkServer.Listen(1);

            // add a connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            Assert.That(NetworkServer.AddConnection(conn42), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));

            // add duplicate connectionId
            NetworkConnectionToClient connDup = new NetworkConnectionToClient(42);
            Assert.That(NetworkServer.AddConnection(connDup), Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            Assert.That(NetworkServer.connections[42], Is.EqualTo(conn42));
        }

        [Test]
        public void RemoveConnection()
        {
            // listen
            NetworkServer.Listen(1);

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            Assert.That(NetworkServer.AddConnection(conn42), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // remove connection
            Assert.That(NetworkServer.RemoveConnection(42), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisconnectAllTest_RemoteConnection()
        {
            // listen
            NetworkServer.Listen(1);

            // add connection
            NetworkConnectionToClient conn42 = new NetworkConnectionToClient(42);
            NetworkServer.AddConnection(conn42);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // disconnect all connections
            NetworkServer.DisconnectAll();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisconnectAllTest_LocalConnection()
        {
            // listen
            NetworkServer.Listen(1);

            // set local connection
            LocalConnectionToClient localConnection = new LocalConnectionToClient();
            NetworkServer.SetLocalConnection(localConnection);

            // disconnect all connections should remove local connection
            NetworkServer.DisconnectAll();
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        // send a message all the way from client to server
        [Test]
        public void Send_ClientToServerMessage()
        {
            // register a message handler
            int called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send message & process
            NetworkClient.Send(new TestMessage1());
            ProcessMessages();

            // did it get through?
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void Send_ServerToClientMessage()
        {
            // register a message handler
            int called = 0;
            NetworkClient.RegisterHandler<TestMessage1>(msg => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send message & process
            connectionToClient.Send(new TestMessage1());
            ProcessMessages();

            // did it get through?
            Assert.That(called, Is.EqualTo(1));
        }

        // guarantee that exactly max packet size messages work
        [Test]
        public void Send_ClientToServerMessage_MaxMessageSize()
        {
            // register a message handler
            int called = 0;
            NetworkServer.RegisterHandler<VariableSizedMessage>((conn, msg) => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send message & process
            int max = MessagePacking.MaxContentSize;
            NetworkClient.Send(new VariableSizedMessage(max));
            ProcessMessages();

            // did it get through?
            Assert.That(called, Is.EqualTo(1));
        }

        // guarantee that exactly max packet size messages work
        [Test]
        public void Send_ServerToClientMessage_MaxMessageSize()
        {
            // register a message handler
            int called = 0;
            NetworkClient.RegisterHandler<VariableSizedMessage>(msg => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send message & process
            int max = MessagePacking.MaxContentSize;
            connectionToClient.Send(new VariableSizedMessage(max));
            ProcessMessages();

            // did it get through?
            Assert.That(called, Is.EqualTo(1));
        }

        // guarantee that exactly max message size + 1 doesn't work anymore
        [Test]
        public void Send_ClientToServerMessage_LargerThanMaxMessageSize()
        {
            // register a message handler
            int called = 0;
            NetworkServer.RegisterHandler<VariableSizedMessage>((conn, msg) => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // calculate max := transport.max - message header

            // send message & process
            int transportMax = transport.GetMaxPacketSize(Channels.Reliable);
            int messageMax = MessagePacking.MaxContentSize;
            LogAssert.Expect(LogType.Error, $"NetworkConnection.ValidatePacketSize: cannot send packet larger than {transportMax} bytes, was {transportMax + 1} bytes");
            NetworkClient.Send(new VariableSizedMessage(messageMax + 1));
            ProcessMessages();

            // should be too big to send
            Assert.That(called, Is.EqualTo(0));
        }

        // guarantee that exactly max message size + 1 doesn't work anymore
        [Test]
        public void Send_ServerToClientMessage_LargerThanMaxMessageSize()
        {
            // register a message handler
            int called = 0;
            NetworkClient.RegisterHandler<VariableSizedMessage>(msg => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send message & process
            int transportMax = transport.GetMaxPacketSize(Channels.Reliable);
            int messageMax = MessagePacking.MaxContentSize;
            LogAssert.Expect(LogType.Error, $"NetworkConnection.ValidatePacketSize: cannot send packet larger than {transportMax} bytes, was {transportMax + 1} bytes");
            connectionToClient.Send(new VariableSizedMessage(messageMax + 1));
            ProcessMessages();

            // should be too big to send
            Assert.That(called, Is.EqualTo(0));
        }

        // transport recommends a max batch size.
        // but we support up to max packet size.
        // for example, with KCP it makes sense to always send MTU sized batches.
        // but we can send up to 144 KB messages.
        // => make sure this works. it's a special path in the code and used to
        //    cause a bug in uMMORPG where SpawnMessage would be > MTU, the
        //    timestamp would not be included because > max batch, hence client
        //    couldn't parse it properly.
        [Test]
        public void Send_ClientToServerMessage_LargerThanBatchThreshold()
        {
            // register a message handler
            int called = 0;
            NetworkServer.RegisterHandler<VariableSizedMessage>((conn, msg) => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send message & process
            int threshold = transport.GetBatchThreshold(Channels.Reliable);
            NetworkClient.Send(new VariableSizedMessage(threshold + 1));
            ProcessMessages();

            // did it get through?
            Assert.That(called, Is.EqualTo(1));
        }

        // transport recommends a max batch size.
        // but we support up to max packet size.
        // for example, with KCP it makes sense to always send MTU sized batches.
        // but we can send up to 144 KB messages.
        // => make sure this works. it's a special path in the code and used to
        //    cause a bug in uMMORPG where SpawnMessage would be > MTU, the
        //    timestamp would not be included because > max batch, hence client
        //    couldn't parse it properly.
        [Test]
        public void Send_ServerToClientMessage_LargerThanBatchThreshold()
        {
            // register handler
            int called = 0;
            NetworkClient.RegisterHandler<VariableSizedMessage>(msg => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send large message & process
            int threshold = transport.GetBatchThreshold(Channels.Reliable);
            connectionToClient.Send(new VariableSizedMessage(threshold + 1));
            ProcessMessages();

            // did it get through?
            Assert.That(called, Is.EqualTo(1));
        }

        // there used to be a data race where messages > batch threshold would
        // be sent directly, instead of being flushed at the end of the frame
        // like all the smaller messages.
        // make sure this never happens again.
        [Test]
        public void Send_ClientToServerMessage_LargerThanBatchThreshold_SentInOrder()
        {
            // register two message handlers
            List<string> received = new List<string>();
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => received.Add("smol"), false);
            NetworkServer.RegisterHandler<VariableSizedMessage>((conn, msg) => received.Add("big"), false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send small message first
            NetworkClient.Send(new TestMessage1());

            // send large message
            int threshold = transport.GetBatchThreshold(Channels.Reliable);
            NetworkClient.Send(new VariableSizedMessage(threshold + 1));

            // process everything
            ProcessMessages();

            // both arrived, and small arrived before large?
            Assert.That(received.Count, Is.EqualTo(2));
            Assert.That(received[0], Is.EqualTo("smol"));
            Assert.That(received[1], Is.EqualTo("big"));
        }

        // there used to be a data race where messages > batch threshold would
        // be sent directly, instead of being flushed at the end of the frame
        // like all the smaller messages.
        // make sure this never happens again.
        [Test]
        public void Send_ServerToClientMessage_LargerThanBatchThreshold_SentInOrder()
        {
            // register two message handlers
            List<string> received = new List<string>();
            NetworkClient.RegisterHandler<TestMessage1>(msg => received.Add("smol"), false);
            NetworkClient.RegisterHandler<VariableSizedMessage>(msg => received.Add("big"), false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send small message first
            connectionToClient.Send(new TestMessage1());

            // send large message
            int threshold = transport.GetBatchThreshold(Channels.Reliable);
            connectionToClient.Send(new VariableSizedMessage(threshold + 1));

            // process everything
            ProcessMessages();

            // both arrived, and small arrived before large?
            Assert.That(received.Count, Is.EqualTo(2));
            Assert.That(received[0], Is.EqualTo("smol"));
            Assert.That(received[1], Is.EqualTo("big"));
        }

        // make sure NetworkConnection.remoteTimeStamp is always the time on the
        // remote end when the message was sent
        [Test]
        public void Send_ClientToServerMessage_SetsRemoteTimeStamp()
        {
            // register a message handler
            int called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send message
            NetworkClient.Send(new TestMessage1());

            // remember current time & update NetworkClient IMMEDIATELY so the
            // batch is finished with timestamp.
            double sendTime = NetworkTime.localTime;
            NetworkClient.NetworkLateUpdate();

            // let some time pass before processing
            const int waitTime = 100;
            Thread.Sleep(waitTime);
            ProcessMessages();

            // is the remote timestamp set to when we sent it?
            // remember the time when we sent the message
            // (within 1/10th of the time we waited. we need some tolerance
            //  because we don't capture NetworkTime.localTime exactly when we
            //  finish the batch. but the difference should not be > 'waitTime')
            Assert.That(called, Is.EqualTo(1));
            Assert.That(connectionToClient.remoteTimeStamp, Is.EqualTo(sendTime).Within(waitTime / 10));
        }

        [Test]
        public void Send_ServerToClientMessage_SetsRemoteTimeStamp()
        {
            // register a message handler
            int called = 0;
            NetworkClient.RegisterHandler<TestMessage1>(msg => ++called, false);

            // listen & connect a client
            NetworkServer.Listen(1);
            ConnectClientBlocking(out NetworkConnectionToClient connectionToClient);

            // send message
            connectionToClient.Send(new TestMessage1());

            // remember current time & update NetworkClient IMMEDIATELY so the
            // batch is finished with timestamp.
            double sendTime = NetworkTime.localTime;
            NetworkServer.NetworkLateUpdate();

            // let some time pass before processing
            const int waitTime = 100;
            Thread.Sleep(waitTime);
            ProcessMessages();

            // is the remote timestamp set to when we sent it?
            // remember the time when we sent the message
            // (within 1/10th of the time we waited. we need some tolerance
            //  because we don't capture NetworkTime.localTime exactly when we
            //  finish the batch. but the difference should not be > 'waitTime')
            Assert.That(called, Is.EqualTo(1));
            Assert.That(NetworkClient.connection.remoteTimeStamp, Is.EqualTo(sendTime).Within(waitTime / 10));
        }

        [Test]
        public void OnDataReceivedInvalidConnectionId()
        {
            // register a message handler
            int called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => ++called, false);

            // listen
            NetworkServer.Listen(1);

            // serialize a test message into an arraysegment
            byte[] message = MessagePackingTest.PackToByteArray(new TestMessage1());

            // call transport.OnDataReceived with an invalid connectionId
            // an error log is expected.
            LogAssert.ignoreFailingMessages = true;
            transport.OnServerDataReceived.Invoke(42, new ArraySegment<byte>(message), 0);
            LogAssert.ignoreFailingMessages = false;

            // message handler should never be called
            Assert.That(called, Is.EqualTo(0));
        }

        [Test]
        public void SetClientReadyAndNotReady()
        {
            CreateLocalConnectionPair(out LocalConnectionToClient connectionToClient, out _);
            Assert.That(connectionToClient.isReady, Is.False);

            NetworkServer.SetClientReady(connectionToClient);
            Assert.That(connectionToClient.isReady, Is.True);

            NetworkServer.SetClientNotReady(connectionToClient);
            Assert.That(connectionToClient.isReady, Is.False);
        }

        [Test]
        public void SetAllClientsNotReady()
        {
            // add first ready client
            CreateLocalConnectionPair(out LocalConnectionToClient first, out _);
            first.isReady = true;
            NetworkServer.connections[42] = first;

            // add second ready client
            CreateLocalConnectionPair(out LocalConnectionToClient second, out _);
            second.isReady = true;
            NetworkServer.connections[43] = second;

            // set all not ready
            NetworkServer.SetAllClientsNotReady();
            Assert.That(first.isReady, Is.False);
            Assert.That(second.isReady, Is.False);
        }

        [Test]
        public void ReadyMessageSetsClientReady()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);
            Assert.That(connectionToClient.isReady, Is.True);
        }

        // simply send a [Command] from client to server
        [Test]
        public void SendCommand()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            // add an identity with two networkbehaviour components
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out CommandTestNetworkBehaviour comp);
            identity.netId = 42;
            identity.isLocalPlayer = true;
            // for authority check
            identity.connectionToClient = connectionToClient;
            connectionToClient.identity = identity;

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call the command
            comp.TestCommand();
            ProcessMessages();
            Assert.That(comp.called, Is.EqualTo(1));
        }

        // send a [Command] to an entity with TWO command components.
        // make sure the correct one is called.
        [Test]
        public void SendCommand_CalledOnCorrectComponent()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            // add an identity with two networkbehaviour components
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out CommandTestNetworkBehaviour comp0, out CommandTestNetworkBehaviour comp1);
            identity.netId = 42;
            identity.isLocalPlayer = true;
            // for authority check
            identity.connectionToClient = connectionToClient;
            connectionToClient.identity = identity;

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call the command
            comp1.TestCommand();
            ProcessMessages();
            Assert.That(comp0.called, Is.EqualTo(0));
            Assert.That(comp1.called, Is.EqualTo(1));
        }

        [Test]
        public void SendCommand_OnlyAllowedOnOwnedObjects()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            // add an identity with two networkbehaviour components
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out CommandTestNetworkBehaviour comp);
            identity.netId = 42;
            identity.isLocalPlayer = true;
            // for authority check
            identity.connectionToClient = connectionToClient;
            connectionToClient.identity = identity;

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // change identity's owner connection so we can't call [Commands] on it
            identity.connectionToClient = new LocalConnectionToClient();

            // call the command
            comp.TestCommand();
            ProcessMessages();
            Assert.That(comp.called, Is.EqualTo(0));
        }

        [Test]
        public void SendCommand_RequiresAuthority()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            // add an identity with two networkbehaviour components
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out CommandTestNetworkBehaviour comp);
            identity.netId = 42;
            identity.isLocalPlayer = false; // NO AUTHORITY
            // for authority check
            identity.connectionToClient = connectionToClient;
            connectionToClient.identity = identity;

            // identity needs to be in spawned dict, otherwise command handler
            // won't find it
            NetworkIdentity.spawned[identity.netId] = identity;

            // call the command
            comp.TestCommand();
            ProcessMessages();
            Assert.That(comp.called, Is.EqualTo(0));
        }

        [Test]
        public void ActivateHostSceneCallsOnStartClient()
        {
            // add an identity with a networkbehaviour to .spawned
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out OnStartClientTestNetworkBehaviour comp);
            identity.netId = 42;
            NetworkIdentity.spawned[identity.netId] = identity;

            // ActivateHostScene
            NetworkServer.ActivateHostScene();

            // was OnStartClient called for all .spawned networkidentities?
            Assert.That(comp.called, Is.EqualTo(1));
        }

        [Test]
        public void SendToAll()
        {
            // message handler
            int called = 0;
            NetworkClient.RegisterHandler<TestMessage1>(msg => ++called, false);

            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send & process
            NetworkServer.SendToAll(new TestMessage1());
            ProcessMessages();

            // called?
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterHandler()
        {
            // RegisterHandler(conn, msg) variant
            int variant1Called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => ++variant1Called, false);

            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send a message, check if it was handled
            NetworkClient.Send(new TestMessage1());
            ProcessMessages();
            Assert.That(variant1Called, Is.EqualTo(1));

            // unregister, send again, should not be called again
            NetworkServer.UnregisterHandler<TestMessage1>();
            NetworkClient.Send(new TestMessage1());
            ProcessMessages();
            Assert.That(variant1Called, Is.EqualTo(1));
        }

        [Test]
        public void ClearHandler()
        {
            // RegisterHandler(conn, msg) variant
            int variant1Called = 0;
            NetworkServer.RegisterHandler<TestMessage1>((conn, msg) => ++variant1Called, false);

            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // send a message, check if it was handled
            NetworkClient.Send(new TestMessage1());
            ProcessMessages();
            Assert.That(variant1Called, Is.EqualTo(1));

            // clear handlers, send again, should not be called again
            NetworkServer.ClearHandlers();
            NetworkClient.Send(new TestMessage1());
            ProcessMessages();
            Assert.That(variant1Called, Is.EqualTo(1));
        }

        [Test]
        public void GetNetworkIdentity()
        {
            // create a GameObject with NetworkIdentity
            CreateNetworked(out GameObject go, out NetworkIdentity identity);

            // GetNetworkIdentity
            bool result = NetworkServer.GetNetworkIdentity(go, out NetworkIdentity value);
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo(identity));
        }

        [Test]
        public void GetNetworkIdentity_ErrorIfNotFound()
        {
            // create a GameObject without NetworkIdentity
            CreateGameObject(out GameObject goWithout);

            // GetNetworkIdentity for GO without identity
            LogAssert.Expect(LogType.Error, $"GameObject {goWithout.name} doesn't have NetworkIdentity.");
            bool result = NetworkServer.GetNetworkIdentity(goWithout, out NetworkIdentity value);
            Assert.That(result, Is.False);
            Assert.That(value, Is.Null);
        }

        [Test]
        public void ShowForConnection()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            // overwrite spawn message handler
            int called = 0;
            NetworkClient.ReplaceHandler<SpawnMessage>(msg => ++called, false);

            // create a gameobject and networkidentity and some unique values
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            identity.connectionToClient = connectionToClient;

            // call ShowForConnection
            NetworkServer.ShowForConnection(identity, connectionToClient);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));

            // destroy manually to avoid 'Destroy can't be called in edit mode'
            GameObject.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void ShowForConnection_OnlyWorksIfReady()
        {
            // listen & connect
            // DO NOT set ready this time
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticated(out NetworkConnectionToClient connectionToClient);

            // overwrite spawn message handler
            int called = 0;
            NetworkClient.ReplaceHandler<SpawnMessage>(msg => ++called, false);

            // create a gameobject and networkidentity and some unique values
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            identity.connectionToClient = connectionToClient;

            // call ShowForConnection - should not work if not ready
            NetworkServer.ShowForConnection(identity, connectionToClient);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(0));

            // destroy manually to avoid 'Destroy can't be called in edit mode'
            GameObject.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void HideForConnection()
        {
            // listen & connect
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            // overwrite spawn message handler
            int called = 0;
            NetworkClient.ReplaceHandler<ObjectHideMessage>(msg => ++called, false);

            // create a gameobject and networkidentity and some unique values
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            identity.connectionToClient = connectionToClient;

            // call HideForConnection
            NetworkServer.HideForConnection(identity, connectionToClient);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));

            // destroy manually to avoid 'Destroy can't be called in edit mode'
            GameObject.DestroyImmediate(identity.gameObject);
        }

        [Test]
        public void ValidateSceneObject()
        {
            // create a gameobject and networkidentity
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
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
        }

        [Test]
        public void SpawnObjects()
        {
            // create a scene object and set inactive before spawning
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            identity.sceneId = 42;
            go.SetActive(false);

            // create a NON scene object and set inactive before spawning
            CreateNetworked(out GameObject go2, out NetworkIdentity identity2);
            identity2.sceneId = 0;
            go2.SetActive(false);

            // start server
            NetworkServer.Listen(1);

            // SpawnObjects() should return true and activate the scene object
            Assert.That(NetworkServer.SpawnObjects(), Is.True);
            Assert.That(go.activeSelf, Is.True);
            Assert.That(go2.activeSelf, Is.False);

            // reset isServer to avoid Destroy instead of DestroyImmediate
            identity.isServer = false;
            identity2.isServer = false;
        }

        [Test]
        public void SpawnObjects_OnlyIfServerActive()
        {
            // calling SpawnObjects while server isn't active should do nothing
            Assert.That(NetworkServer.SpawnObjects(), Is.False);
        }

        [Test]
        public void UnSpawn()
        {
            // create scene object with valid netid and set active
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            identity.sceneId = 42;
            identity.netId = 123;
            go.SetActive(true);

            // unspawn should reset netid
            NetworkServer.UnSpawn(go);
            Assert.That(identity.netId, Is.Zero);
        }

        [Test]
        public void ShutdownCleanup()
        {
            // listen
            NetworkServer.Listen(1);

            // add some test event hooks to make sure they are cleaned up.
            // there used to be a bug where they wouldn't be cleaned up.
            NetworkServer.OnConnectedEvent = connection => {};
            NetworkServer.OnDisconnectedEvent = connection => {};

            // set local connection
            NetworkServer.SetLocalConnection(new LocalConnectionToClient());

            // connect a client
            transport.ClientConnect("localhost");
            UpdateTransport();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // shutdown
            NetworkServer.Shutdown();

            // state cleared?
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.active, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
            Assert.That(NetworkServer.localClientActive, Is.False);
            Assert.That(NetworkServer.OnConnectedEvent, Is.Null);
            Assert.That(NetworkServer.OnDisconnectedEvent, Is.Null);
        }

        [Test]
        public void SendToAll_CalledWhileNotActive_ShouldGiveWarning()
        {
            LogAssert.Expect(LogType.Warning, $"Can not send using NetworkServer.SendToAll<T>(T msg) because NetworkServer is not active");
            NetworkServer.SendToAll(new NetworkPingMessage {});
        }

        [Test]
        public void SendToReady_CalledWhileNotActive_ShouldGiveWarning()
        {
            LogAssert.Expect(LogType.Warning, $"Can not send using NetworkServer.SendToReady<T>(T msg) because NetworkServer is not active");
            NetworkServer.SendToReady(new NetworkPingMessage {});
        }

        [Test]
        public void NoExternalConnections_WithNoConnection()
        {
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(0));
            Assert.That(NetworkServer.NoExternalConnections(), Is.True);
        }

        [Test]
        public void NoExternalConnections_WithConnections()
        {
            NetworkServer.connections.Add(1, null);
            NetworkServer.connections.Add(2, null);
            Assert.That(NetworkServer.NoExternalConnections(), Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));
        }

        [Test]
        public void NoExternalConnections_WithHostOnly()
        {
            CreateLocalConnectionPair(out LocalConnectionToClient connectionToClient, out _);

            NetworkServer.SetLocalConnection(connectionToClient);
            NetworkServer.connections.Add(0, connectionToClient);

            Assert.That(NetworkServer.NoExternalConnections(), Is.True);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            NetworkServer.RemoveLocalConnection();
        }

        [Test]
        public void NoExternalConnections_WithHostAndConnection()
        {
            CreateLocalConnectionPair(out LocalConnectionToClient connectionToClient, out _);

            NetworkServer.SetLocalConnection(connectionToClient);
            NetworkServer.connections.Add(0, connectionToClient);
            NetworkServer.connections.Add(1, null);

            Assert.That(NetworkServer.NoExternalConnections(), Is.False);
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(2));

            NetworkServer.RemoveLocalConnection();
        }

        // updating NetworkServer with a null entry in connection.observing
        // should log a warning. someone probably used GameObject.Destroy
        // instead of NetworkServer.Destroy.
        [Test]
        public void UpdateDetectsNullEntryInObserving()
        {
            // start
            NetworkServer.Listen(1);

            // add a connection that is observed by a null entity
            NetworkServer.connections[42] = new FakeNetworkConnection{isReady=true};
            NetworkServer.connections[42].observing.Add(null);

            // update
            LogAssert.Expect(LogType.Warning, new Regex("Found 'null' entry in observing list.*"));
            NetworkServer.NetworkLateUpdate();
        }

        // updating NetworkServer with a null entry in connection.observing
        // should log a warning. someone probably used GameObject.Destroy
        // instead of NetworkServer.Destroy.
        //
        // => need extra test because of Unity's custom null check
        [Test]
        public void UpdateDetectsDestroyedEntryInObserving()
        {
            // start
            NetworkServer.Listen(1);

            // add a connection that is observed by a destroyed entity
            CreateNetworked(out GameObject go, out NetworkIdentity ni);
            NetworkServer.connections[42] = new FakeNetworkConnection{isReady=true};
            NetworkServer.connections[42].observing.Add(ni);
            GameObject.DestroyImmediate(go);

            // update
            LogAssert.Expect(LogType.Warning, new Regex("Found 'null' entry in observing list.*"));
            NetworkServer.NetworkLateUpdate();
        }

        // NetworkServer.Update iterates all connections.
        // a timed out connection may call Disconnect, trying to modify the
        // collection during the loop.
        // -> test to prevent https://github.com/vis2k/Mirror/pull/2718
        [Test]
        public void UpdateWithTimedOutConnection()
        {
            // configure to disconnect with '0' timeout (= immediately)
#pragma warning disable 618
            NetworkServer.disconnectInactiveConnections = true;
            NetworkServer.disconnectInactiveTimeout = 0;

            // start
            NetworkServer.Listen(1);

            // add a connection
            NetworkServer.connections[42] = new FakeNetworkConnection{isReady=true};

            // update
            NetworkServer.NetworkLateUpdate();

            // clean up
            NetworkServer.disconnectInactiveConnections = false;
#pragma warning restore 618
        }
    }
}
