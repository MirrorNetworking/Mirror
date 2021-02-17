using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class VirtualClientRpc : NetworkBehaviour
    {
        public event Action<int> onVirtualSendInt;

        [ClientRpc]
        public virtual void RpcSendInt(int someInt)
        {
            onVirtualSendInt?.Invoke(someInt);
        }
    }

    class VirtualNoOverrideClientRpc : VirtualClientRpc
    {

    }

    class VirtualOverrideClientRpc : VirtualClientRpc
    {
        public event Action<int> onOverrideSendInt;

        [ClientRpc]
        public override void RpcSendInt(int someInt)
        {
            onOverrideSendInt?.Invoke(someInt);
        }
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

    public class ClientRpcOverrideTest : RemoteTestBase
    {
        [Test]
        public void VirtualRpcIsCalled()
        {
            VirtualClientRpc hostBehaviour = CreateHostObject<VirtualClientRpc>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualCommandWithNoOverrideIsCalled()
        {
            VirtualNoOverrideClientRpc hostBehaviour = CreateHostObject<VirtualNoOverrideClientRpc>(true);

            const int someInt = 20;

            int virtualCallCount = 0;
            hostBehaviour.onVirtualSendInt += incomingInt =>
            {
                virtualCallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };

            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualRpcIsCalled()
        {
            VirtualOverrideClientRpc hostBehaviour = CreateHostObject<VirtualOverrideClientRpc>(true);

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

            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(0));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }

        [Test]
        public void OverrideVirtualWithBaseCallsBothVirtualAndBase()
        {
            VirtualOverrideClientRpcWithBase hostBehaviour = CreateHostObject<VirtualOverrideClientRpcWithBase>(true);

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

            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
        }
    }
}
