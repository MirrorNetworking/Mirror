using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class ClientRpcBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [ClientRpc]
        public void SendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class ExcludeOwnerBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [ClientRpc(includeOwner = false)]
        public void RpcSendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class AbstractNetworkBehaviourClientRpcBehaviour : NetworkBehaviour
    {
        public abstract class MockMonsterBase : NetworkBehaviour
        {
            public abstract string GetName();
        }

        public class MockZombie : MockMonsterBase
        {
            public override string GetName()
            {
                return "Zombie";
            }
        }

        public class MockWolf : MockMonsterBase
        {
            public override string GetName()
            {
                return "Wolf";
            }
        }

        public event Action<MockMonsterBase> onSendMonsterBase;

        [ClientRpc]
        public void RpcSendMonster(MockMonsterBase someMonster)
        {
            onSendMonsterBase?.Invoke(someMonster);
        }
    }

    public class ClientRpcTest : RemoteTestBase
    {
        [Test]
        public void RpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ClientRpcBehaviour hostBehaviour, NetworkServer.localConnection);

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
        public void RpcIsCalledForNotOwner()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ExcludeOwnerBehaviour hostBehaviour);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcNotCalledForOwner()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ExcludeOwnerBehaviour hostBehaviour, NetworkServer.localConnection);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void RpcIsCalledWithAbstractNetworkBehaviourParameter()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out AbstractNetworkBehaviourClientRpcBehaviour hostBehaviour, NetworkServer.localConnection);

            // spawn clientrpc parameter targets
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity wolfIdentity, out AbstractNetworkBehaviourClientRpcBehaviour.MockWolf wolf, NetworkServer.localConnection);
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity zombieIdentity, out AbstractNetworkBehaviourClientRpcBehaviour.MockZombie zombie, NetworkServer.localConnection);

            AbstractNetworkBehaviourClientRpcBehaviour.MockMonsterBase currentMonster = null;

            int callCount = 0;
            hostBehaviour.onSendMonsterBase += incomingMonster =>
            {
                callCount++;
                Assert.That(incomingMonster, Is.EqualTo(currentMonster));
            };

            currentMonster = wolf;
            hostBehaviour.RpcSendMonster(currentMonster);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));

            currentMonster = zombie;
            hostBehaviour.RpcSendMonster(currentMonster);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(2));
        }
    }
}
