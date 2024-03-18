using System;
using NUnit.Framework;

namespace Mirror.Tests.Rpcs
{
    class VirtualCommand : NetworkBehaviour
    {
        public event Action<int> onVirtualSendInt;

        [Command]
        public virtual void CmdSendInt(int someInt) =>
            onVirtualSendInt?.Invoke(someInt);
    }

    class VirtualNoOverrideCommand : VirtualCommand {}

    class VirtualOverrideCommand : VirtualCommand
    {
        public event Action<int> onOverrideSendInt;

        [Command]
        public override void CmdSendInt(int someInt) =>
            onOverrideSendInt?.Invoke(someInt);
    }

    class VirtualOverrideCommandWithBase : VirtualCommand
    {
        public event Action<int> onOverrideSendInt;

        [Command]
        public override void CmdSendInt(int someInt)
        {
            base.CmdSendInt(someInt);
            onOverrideSendInt?.Invoke(someInt);
        }
    }

    // test for 2 overrides
    class VirtualOverrideCommandWithBase2 : VirtualOverrideCommandWithBase
    {
        public event Action<int> onOverrideSendInt2;

        [Command]
        public override void CmdSendInt(int someInt)
        {
            base.CmdSendInt(someInt);
            onOverrideSendInt2?.Invoke(someInt);
        }
    }

    public class CommandOverrideTest : MirrorTest
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
        public void VirtualCommandIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualCommand serverComponent,
                                    out _, out _, out VirtualCommand clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            serverComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualCommandWithNoOverrideIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualNoOverrideCommand serverComponent,
                                    out _, out _, out VirtualNoOverrideCommand clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            serverComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualCommandIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideCommand serverComponent,
                                    out _, out _, out VirtualOverrideCommand clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            serverComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
            };
            serverComponent.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(0));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualWithBaseCallsBothVirtualAndBase()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideCommandWithBase serverComponent,
                                    out _, out _, out VirtualOverrideCommandWithBase clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            serverComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            serverComponent.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualWithBaseCallsAllMethodsThatCallBase()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideCommandWithBase2 serverComponent,
                                    out _, out _, out VirtualOverrideCommandWithBase2 clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            int override2CallCount = 0;
            serverComponent.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            serverComponent.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            serverComponent.onOverrideSendInt2 += incomingInt =>
            {
                override2CallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
            Assert.That(override2CallCount, Is.EqualTo(1));
        }
    }
}
