using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class TargetRpcBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [TargetRpc]
        public void SendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }

        [TargetRpc]
        public void SendIntWithTarget(NetworkConnection target, int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class TargetRpcOverloads : NetworkBehaviour
    {
        public int firstCalled = 0;
        public int secondCalled = 0;

        [TargetRpc]
        public void TargetRpcTest(int _) => ++firstCalled;

        [TargetRpc]
        public void TargetRpcTest(string _) => ++secondCalled;
    }

    public class TargetRpcTest : RemoteTestBase
    {
        [Test]
        public void TargetRpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour, NetworkServer.localConnection);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void TargetRpcIsCalledOnTarget()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendIntWithTarget(NetworkServer.localConnection, someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void ErrorForTargetRpcWithNoOwner()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(LogType.Error, $"TargetRPC System.Void Mirror.Tests.RemoteAttrributeTest.TargetRpcBehaviour::SendInt(System.Int32) was given a null connection, make sure the object has an owner or you pass in the target connection");
            hostBehaviour.SendInt(someInt);
        }

        [Test]
        public void ErrorForTargetRpcWithNullArgment()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(LogType.Error, $"TargetRPC System.Void Mirror.Tests.RemoteAttrributeTest.TargetRpcBehaviour::SendIntWithTarget(Mirror.NetworkConnection,System.Int32) was given a null connection, make sure the object has an owner or you pass in the target connection");
            hostBehaviour.SendIntWithTarget(null, someInt);
        }

        [Test]
        public void ErrorForTargetRpcWhenNotGivenConnectionToClient()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(LogType.Error, $"TargetRPC System.Void Mirror.Tests.RemoteAttrributeTest.TargetRpcBehaviour::SendIntWithTarget(Mirror.NetworkConnection,System.Int32) requires a NetworkConnectionToClient but was given {typeof(FakeConnection).Name}");
            hostBehaviour.SendIntWithTarget(new FakeConnection(), someInt);
        }
        class FakeConnection : NetworkConnection
        {
            public override string address => throw new NotImplementedException();
            public override void Disconnect() => throw new NotImplementedException();
            internal override void Send(ArraySegment<byte> segment, int channelId = 0) => throw new NotImplementedException();
            protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable) => throw new NotImplementedException();
        }

        [Test]
        public void ErrorForTargetRpcWhenServerNotActive()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            NetworkServer.active = false;
            LogAssert.Expect(LogType.Error, $"TargetRPC System.Void Mirror.Tests.RemoteAttrributeTest.TargetRpcBehaviour::SendInt(System.Int32) called when server not active");
            hostBehaviour.SendInt(someInt);
        }

        [Test]
        public void ErrorForTargetRpcWhenObjectNotSpawned()
        {
            // create without spawning
            CreateNetworked(out GameObject _, out NetworkIdentity _, out TargetRpcBehaviour hostBehaviour);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(LogType.Warning, $"TargetRpc System.Void Mirror.Tests.RemoteAttrributeTest.TargetRpcBehaviour::SendInt(System.Int32) called on {hostBehaviour.name} but that object has not been spawned or has been unspawned");
            hostBehaviour.SendInt(someInt);
        }

        // RemoteCalls uses md.FullName which gives us the full command/rpc name
        // like "System.Void Mirror.Tests.RemoteAttrributeTest.AuthorityBehaviour::SendInt(System.Int32)"
        // which means overloads with same name but different types should work.
        [Test]
        public void TargetRpcOverload()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out TargetRpcOverloads hostBehaviour, NetworkServer.localConnection);

            hostBehaviour.TargetRpcTest(42);
            hostBehaviour.TargetRpcTest("A");
            ProcessMessages();
            Assert.That(hostBehaviour.firstCalled, Is.EqualTo(1));
            Assert.That(hostBehaviour.secondCalled, Is.EqualTo(1));
        }
    }
}
