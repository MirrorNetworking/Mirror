using System;
using NUnit.Framework;

namespace Mirror.Tests.Rpcs
{
    class VirtualTargetRpc : NetworkBehaviour
    {
        public event Action<int> onVirtualSendInt;

        [TargetRpc]
        public virtual void TargetSendInt(int someInt)
        {
            onVirtualSendInt?.Invoke(someInt);
        }
    }

    class VirtualNoOverrideTargetRpc : VirtualTargetRpc {}

    class VirtualOverrideTargetRpc : VirtualTargetRpc
    {
        public event Action<int> onOverrideSendInt;

        [TargetRpc]
        public override void TargetSendInt(int someInt)
        {
            onOverrideSendInt?.Invoke(someInt);
        }
    }

    class VirtualOverrideTargetRpcWithBase : VirtualTargetRpc
    {
        public event Action<int> onOverrideSendInt;

        [TargetRpc]
        public override void TargetSendInt(int someInt)
        {
            base.TargetSendInt(someInt);
            onOverrideSendInt?.Invoke(someInt);
        }
    }

    public class TargetRpcOverrideTest : MirrorTest
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
            CreateNetworkedAndSpawn(out _, out _, out VirtualTargetRpc serverComponent,
                                    out _, out _, out VirtualTargetRpc clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            clientComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            serverComponent.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualCommandWithNoOverrideIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualNoOverrideTargetRpc serverComponent,
                                    out _, out _, out VirtualNoOverrideTargetRpc clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            clientComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            serverComponent.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualRpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideTargetRpc serverComponent,
                                    out _, out _, out VirtualOverrideTargetRpc clientComponent,
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

            serverComponent.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(0));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualWithBaseCallsBothVirtualAndBase()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideTargetRpcWithBase serverComponent,
                                    out _, out _, out VirtualOverrideTargetRpcWithBase clientComponent,
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

            serverComponent.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }
    }
}
