using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
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

    class VirtualNoOverrideTargetRpc : VirtualTargetRpc
    {

    }

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

    public class TargetRpcOverrideTest : RemoteTestBase
    {
        [Test]
        public void VirtualRpcIsCalled()
        {
            VirtualTargetRpc hostBehaviour = CreateHostObject<VirtualTargetRpc>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualCommandWithNoOverrideIsCalled()
        {
            VirtualNoOverrideTargetRpc hostBehaviour = CreateHostObject<VirtualNoOverrideTargetRpc>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualRpcIsCalled()
        {
            VirtualOverrideTargetRpc hostBehaviour = CreateHostObject<VirtualOverrideTargetRpc>(true);

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

            hostBehaviour.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(0));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualWithBaseCallsBothVirtualAndBase()
        {
            VirtualOverrideTargetRpcWithBase hostBehaviour = CreateHostObject<VirtualOverrideTargetRpcWithBase>(true);

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

            hostBehaviour.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }
    }
}
