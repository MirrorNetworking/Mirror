using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class VirtualClientRpc : NetworkBehaviour
    {
        public event Action<int> onVirtualSendInt;

        [ClientRpc]
        public virtual void RpcSendInt(int someInt) =>
            onVirtualSendInt?.Invoke(someInt);
    }

    class VirtualNoOverrideClientRpc : VirtualClientRpc {}

    class VirtualOverrideClientRpc : VirtualClientRpc
    {
        public event Action<int> onOverrideSendInt;

        [ClientRpc]
        public override void RpcSendInt(int someInt) =>
            onOverrideSendInt?.Invoke(someInt);
    }

    class VirtualOverrideClientRpcWithBase : VirtualClientRpc
    {
        public event Action<int> onOverrideSendInt;

        [ClientRpc]
        public override void RpcSendInt(int someInt)
        {
            base.RpcSendInt(someInt);
            onOverrideSendInt?.Invoke(someInt);
        }
    }

    public class ClientRpcOverrideTest : MirrorTest
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
        public void VirtualRpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualClientRpc serverComponent,
                                    out _, out _, out VirtualClientRpc clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            clientComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            serverComponent.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualCommandWithNoOverrideIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualNoOverrideClientRpc serverComponent,
                                    out _, out _, out VirtualNoOverrideClientRpc clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            clientComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            serverComponent.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualRpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideClientRpc serverComponent,
                                    out _, out _, out VirtualOverrideClientRpc clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            clientComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
            };
            clientComponent.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            serverComponent.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(0));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        // IMPORTANT: test to prevent the issue from:
        //            https://github.com/vis2k/Mirror/pull/3072
        [Test]
        public void OverrideVirtualWithBaseCallsBothVirtualAndBase()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideClientRpcWithBase serverComponent,
                                    out _, out _, out VirtualOverrideClientRpcWithBase clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            clientComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            clientComponent.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            serverComponent.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }
    }
}
