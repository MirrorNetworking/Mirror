using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkConnections
{
    public class LocalConnectionTest : MirrorTest
    {
        struct TestMessage : NetworkMessage {}

        LocalConnectionToClient connectionToClient;
        LocalConnectionToServer connectionToServer;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            Utils.CreateLocalConnections(out connectionToClient, out connectionToServer);

            // set up server/client connections so message handling works
            NetworkClient.connection = connectionToServer;
            NetworkServer.connections[connectionToClient.connectionId] = connectionToClient;
        }

        [TearDown]
        public override void TearDown()
        {
            connectionToServer.Disconnect();
            base.TearDown();
        }

        // ---- properties -------------------------------------------------------------

        [Test]
        public void LocalConnectionToClient_AddressIsLocalhost()
        {
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));
        }

        [Test]
        public void LocalConnectionToClient_ConnectionId_IsLocalConnectionId()
        {
            Assert.That(connectionToClient.connectionId, Is.EqualTo(NetworkConnection.LocalConnectionId));
        }

        // ---- IsAlive ----------------------------------------------------------------

        [Test]
        public void LocalConnectionToClient_IsAlive_AlwaysTrue()
        {
            // local connections never time out regardless of the timeout value
            Assert.That(connectionToClient.IsAlive(0), Is.True);
        }

        [Test]
        public void LocalConnectionToServer_IsAlive_AlwaysTrue()
        {
            // local connections never time out regardless of the timeout value
            Assert.That(connectionToServer.IsAlive(0), Is.True);
        }

        // ---- message delivery -------------------------------------------------------

        [Test]
        public void ClientToServerTest()
        {
            // address check kept here as a sanity guard for local connection setup
            Assert.That(connectionToClient.address, Is.EqualTo("localhost"));

            bool invoked = false;
            void Handler(NetworkConnectionToClient conn, TestMessage message) => invoked = true;

            NetworkServer.RegisterHandler<TestMessage>(Handler, false);

            // client sends → data is enqueued in connectionToClient.queue
            connectionToServer.Send(new TestMessage());

            // connectionToClient.Update() processes the client→server queue and
            // delivers to NetworkServer. connectionToServer.Update() is NOT
            // needed for this direction (it processes the server→client queue).
            connectionToClient.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void ServerToClient()
        {
            bool invoked = false;
            void Handler(TestMessage message) => invoked = true;

            NetworkClient.RegisterHandler<TestMessage>(Handler, false);

            // server sends → data is enqueued in connectionToServer.queue
            connectionToClient.Send(new TestMessage());

            // connectionToServer.Update() processes the server→client queue and
            // delivers to NetworkClient
            connectionToServer.Update();

            Assert.True(invoked, "handler should have been invoked");
        }

        [Test]
        public void MultipleMessages_ClientToServer_AllDelivered()
        {
            int invokeCount = 0;
            void Handler(NetworkConnectionToClient conn, TestMessage message) => invokeCount++;

            NetworkServer.RegisterHandler<TestMessage>(Handler, false);

            connectionToServer.Send(new TestMessage());
            connectionToServer.Send(new TestMessage());
            connectionToServer.Send(new TestMessage());

            // each message is independently dequeued and batched in Update(),
            // producing 3 separate OnTransportData calls → handler invoked 3×
            connectionToClient.Update();

            Assert.That(invokeCount, Is.EqualTo(3));
        }

        [Test]
        public void MultipleMessages_ServerToClient_AllDelivered()
        {
            int invokeCount = 0;
            void Handler(TestMessage message) => invokeCount++;

            NetworkClient.RegisterHandler<TestMessage>(Handler, false);

            connectionToClient.Send(new TestMessage());
            connectionToClient.Send(new TestMessage());
            connectionToClient.Send(new TestMessage());

            connectionToServer.Update();

            Assert.That(invokeCount, Is.EqualTo(3));
        }

        // ---- LocalConnectionToServer: Send zero-byte guard --------------------------

        [Test]
        public void LocalConnectionToServer_Send_ZeroBytes_LogsError()
        {
            // LocalConnectionToServer.Send(ArraySegment<byte>) guards against zero-
            // length segments and logs an error rather than enqueuing empty data.
            LogAssert.Expect(LogType.Error, "LocalConnection.SendBytes cannot send zero bytes");

            connectionToServer.Send(new System.ArraySegment<byte>(new byte[0]));

            // the guard returns early, so nothing should have been enqueued
            Assert.That(connectionToClient.queue, Is.Empty);
        }

        // ---- LocalConnectionToServer: pending-event branches in Update() ------------

        [Test]
        public void LocalConnectionToServer_QueueConnectedEvent_FiresOnNextUpdate()
        {
            // QueueConnectedEvent() sets connectedEventPending = true.
            // The next Update() call should fire NetworkClient.OnConnectedEvent
            // and then clear the flag so it does not fire again.
            bool fired = false;
            Action handler = () => fired = true;
            NetworkClient.OnConnectedEvent += handler;
            try
            {
                connectionToServer.QueueConnectedEvent();
                connectionToServer.Update();
                Assert.That(fired, Is.True);
            }
            finally
            {
                NetworkClient.OnConnectedEvent -= handler;
            }
        }

        [Test]
        public void LocalConnectionToServer_QueueConnectedEvent_FiresOnlyOnce()
        {
            // After Update() clears connectedEventPending, a second Update() must
            // not re-fire the event.
            int fireCount = 0;
            Action handler = () => fireCount++;
            NetworkClient.OnConnectedEvent += handler;
            try
            {
                connectionToServer.QueueConnectedEvent();
                connectionToServer.Update(); // fires once
                connectionToServer.Update(); // must not fire again
                Assert.That(fireCount, Is.EqualTo(1));
            }
            finally
            {
                NetworkClient.OnConnectedEvent -= handler;
            }
        }

        [Test]
        public void LocalConnectionToServer_QueueDisconnectedEvent_FiresOnNextUpdate()
        {
            // QueueDisconnectedEvent() sets disconnectedEventPending = true.
            // The next Update() call should fire NetworkClient.OnDisconnectedEvent.
            bool fired = false;
            Action handler = () => fired = true;
            NetworkClient.OnDisconnectedEvent += handler;
            try
            {
                connectionToServer.QueueDisconnectedEvent();
                connectionToServer.Update();
                Assert.That(fired, Is.True);
            }
            finally
            {
                // Unsubscribe before TearDown so Disconnect() → OnTransportDisconnected()
                // does not retrigger our test lambda.
                NetworkClient.OnDisconnectedEvent -= handler;
            }
        }

        // ---- LocalConnectionToServer: DisconnectInternal ----------------------------

        [Test]
        public void LocalConnectionToServer_DisconnectInternal_SetsIsReadyFalse()
        {
            // DisconnectInternal() is the minimal shutdown path: it resets the
            // connection's own isReady and NetworkClient.ready without tearing
            // down the server side.  TearDown's Disconnect() completes the rest.
            connectionToServer.isReady = true;
            NetworkClient.ready = true;

            connectionToServer.DisconnectInternal();

            Assert.That(connectionToServer.isReady, Is.False);
            Assert.That(NetworkClient.ready, Is.False);
        }
    }
}
