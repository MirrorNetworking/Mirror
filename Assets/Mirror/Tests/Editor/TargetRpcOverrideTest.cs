using System;
using NUnit.Framework;
using UnityEngine;

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

    public class TargetRpcOverrideTest : RemoteTestBase
    {
        [Test]
        public void VirtualRpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualTargetRpc hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualNoOverrideTargetRpc hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideTargetRpc hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideTargetRpcWithBase hostBehaviour, NetworkServer.localConnection);

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
