using System;
using NUnit.Framework;

namespace Mirror.Tests.Rpcs
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

    public class ClientRpcTest : MirrorTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // start server/client
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void RpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out ClientRpcBehaviour serverComponent,
                                    out _, out _, out ClientRpcBehaviour clientComponent,
                                    connectionToClient);
            const int someInt = 20;

            int called = 0;
            clientComponent.onSendInt += incomingInt =>
            {
                called++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            serverComponent.SendInt(someInt);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void RpcNotCalledForOwner()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out ExcludeOwnerBehaviour serverComponent,
                                    out _, out _, out ExcludeOwnerBehaviour clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int called = 0;
            clientComponent.onSendInt += incomingInt =>
            {
                called++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            serverComponent.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(0));
        }

        [Test]
        public void RpcIsCalledWithAbstractNetworkBehaviourParameter()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out AbstractNetworkBehaviourClientRpcBehaviour serverComponent,
                                    out _, out _, out AbstractNetworkBehaviourClientRpcBehaviour clientComponent,
                                    connectionToClient);

            // spawn clientrpc parameter targets
            CreateNetworkedAndSpawn(out _, out _, out AbstractNetworkBehaviourClientRpcBehaviour.MockWolf wolf,
                                    out _, out _, out _,
                                    connectionToClient);
            CreateNetworkedAndSpawn(out _, out _, out AbstractNetworkBehaviourClientRpcBehaviour.MockZombie zombie,
                                    out _, out _, out _,
                                    connectionToClient);

            AbstractNetworkBehaviourClientRpcBehaviour.MockMonsterBase currentMonster = null;

            int called = 0;
            clientComponent.onSendMonsterBase += incomingMonster =>
            {
                called++;
                Assert.That(incomingMonster, Is.EqualTo(currentMonster));
            };

            currentMonster = wolf;
            serverComponent.RpcSendMonster(currentMonster);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));

            currentMonster = zombie;
            serverComponent.RpcSendMonster(currentMonster);
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
            CreateNetworkedAndSpawn(out _, out _, out RpcOverloads serverComponent,
                                    out _, out _, out RpcOverloads clientComponent,
                                    connectionToClient);

            serverComponent.RpcTest(42);
            serverComponent.RpcTest("A");
            ProcessMessages();
            Assert.That(clientComponent.firstCalled, Is.EqualTo(1));
            Assert.That(clientComponent.secondCalled, Is.EqualTo(1));
        }
    }

    // still need host mode for this one test
    public class ClientRpcTest_HostMode : MirrorTest
    {
        [SetUp]
        public void Setup()
        {
            base.SetUp();
            // start server/client
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void RpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out ClientRpcBehaviour hostBehaviour, NetworkServer.localConnection);

            const int someInt = 20;

            int called = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                called++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendInt(someInt);
            ProcessMessages(); // first update flushes the rpc on the server
            ProcessMessages(); // second update processes it on the client
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
