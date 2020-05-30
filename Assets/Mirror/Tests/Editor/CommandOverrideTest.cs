using System;
using NUnit.Framework;

namespace Mirror.Tests.CommandAttrributeTest
{
    class VirtualHookBase : NetworkBehaviour
    {
        public event Action<int> onVirtualSendInt;

        [Command]
        public virtual void CmdSendInt(int someInt)
        {
            onVirtualSendInt?.Invoke(someInt);
        }
    }

    class VirtualOverrideHook : VirtualHookBase
    {
        public event Action<int> onOverrideSendInt;

        [Command]
        public override void CmdSendInt(int someInt)
        {
            onOverrideSendInt?.Invoke(someInt);
        }
    }

    public class CommandOverrideTest : CommandTestBase
    {
        [Test]
        public void VirtualCommandIsCalled()
        {
            VirtualHookBase hostBehaviour = CreateHostObject<VirtualHookBase>(true);

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
            VirtualOverrideHook hostBehaviour = CreateHostObject<VirtualOverrideHook>(true);

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
    }
}
