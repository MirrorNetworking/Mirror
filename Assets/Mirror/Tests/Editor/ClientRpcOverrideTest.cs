using System;
using NUnit.Framework;
using UnityEngine;

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

    public class ClientRpcOverrideTest : RemoteTestBase
    {
        [Test]
        public void VirtualRpcIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualClientRpc hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualNoOverrideClientRpc hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideClientRpc hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideClientRpcWithBase hostBehaviour, NetworkServer.localConnection);

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
