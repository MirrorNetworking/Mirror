using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class ClientRpcBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [ClientRpc]
        public void SendInt(int someInt) =>
            onSendInt?.Invoke(someInt);
    }

    class ExcludeOwnerBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [ClientRpc(includeOwner = false)]
        public void RpcSendInt(int someInt) =>
            onSendInt?.Invoke(someInt);
    }

    class AbstractNetworkBehaviourClientRpcBehaviour : NetworkBehaviour
    {
        public abstract class MockMonsterBase : NetworkBehaviour {}
        public class MockZombie : MockMonsterBase {}
        public class MockWolf : MockMonsterBase {}

        public event Action<MockMonsterBase> onSendMonsterBase;

        [ClientRpc]
        public void RpcSendMonster(MockMonsterBase someMonster) =>
            onSendMonsterBase?.Invoke(someMonster);
    }

    class RpcOverloads : NetworkBehaviour
    {
        public int firstCalled = 0;
        public int secondCalled = 0;

        [ClientRpc]
        public void RpcTest(int _) => ++firstCalled;

        [ClientRpc]
        public void RpcTest(string _) => ++secondCalled;
    }

    public class ClientRpcTest : RemoteTestBase
    {
        [Test]
        public void RpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ClientRpcBehaviour hostBehaviour, NetworkServer.localConnection);

            const int someInt = 20;

            int called = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                called++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendInt(someInt);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void RpcIsCalledForNotOwner()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ExcludeOwnerBehaviour hostBehaviour);

            const int someInt = 20;

            int called = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                called++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void RpcNotCalledForOwner()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ExcludeOwnerBehaviour hostBehaviour, NetworkServer.localConnection);

            const int someInt = 20;

            int called = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                called++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(0));
        }

        [Test]
        public void RpcIsCalledWithAbstractNetworkBehaviourParameter()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out AbstractNetworkBehaviourClientRpcBehaviour hostBehaviour, NetworkServer.localConnection);

            // spawn clientrpc parameter targets
            CreateNetworkedAndSpawn(out _, out _, out AbstractNetworkBehaviourClientRpcBehaviour.MockWolf wolf, NetworkServer.localConnection);
            CreateNetworkedAndSpawn(out _, out _, out AbstractNetworkBehaviourClientRpcBehaviour.MockZombie zombie, NetworkServer.localConnection);

            AbstractNetworkBehaviourClientRpcBehaviour.MockMonsterBase currentMonster = null;

            int called = 0;
            hostBehaviour.onSendMonsterBase += incomingMonster =>
            {
                called++;
                Assert.That(incomingMonster, Is.EqualTo(currentMonster));
            };

            currentMonster = wolf;
            hostBehaviour.RpcSendMonster(currentMonster);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));

            currentMonster = zombie;
            hostBehaviour.RpcSendMonster(currentMonster);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(2));
        }

        // RemoteCalls uses md.FullName which gives us the full command/rpc name
        // like "System.Void Mirror.Tests.RemoteAttrributeTest.AuthorityBehaviour::SendInt(System.Int32)"
        // which means overloads with same name but different types should work.
        [Test]
        public void RpcOverload()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcOverloads hostBehaviour, NetworkServer.localConnection);

            hostBehaviour.RpcTest(42);
            hostBehaviour.RpcTest("A");
            ProcessMessages();
            Assert.That(hostBehaviour.firstCalled, Is.EqualTo(1));
            Assert.That(hostBehaviour.secondCalled, Is.EqualTo(1));
        }
    }
}
