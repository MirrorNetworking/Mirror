using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class VirtualCommand : NetworkBehaviour
    {
        public event Action<int> onVirtualSendInt;

        [Command]
        public virtual void CmdSendInt(int someInt)
        {
            onVirtualSendInt?.Invoke(someInt);
        }
    }

    class VirtualNoOverrideCommand : VirtualCommand
    {

    }

    class VirtualOverrideCommand : VirtualCommand
    {
        public event Action<int> onOverrideSendInt;

        [Command]
        public override void CmdSendInt(int someInt)
        {
            onOverrideSendInt?.Invoke(someInt);
        }
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

    public class CommandOverrideTest : RemoteTestBase
    {
        [Test]
        public void VirtualCommandIsCalled()
        {
            VirtualCommand hostBehaviour = CreateHostObject<VirtualCommand>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualCommandWithNoOverrideIsCalled()
        {
            VirtualNoOverrideCommand hostBehaviour = CreateHostObject<VirtualNoOverrideCommand>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualCommandIsCalled()
        {
            VirtualOverrideCommand hostBehaviour = CreateHostObject<VirtualOverrideCommand>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
            };
            hostBehaviour.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(0));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualWithBaseCallsBothVirtualAndBase()
        {
            VirtualOverrideCommandWithBase hostBehaviour = CreateHostObject<VirtualOverrideCommandWithBase>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.onOverrideSendInt += incomingInt =>
            {
                overrideCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }
    }
}
