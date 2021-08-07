using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
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

    public class CommandOverrideTest : RemoteTestBase
    {
        [Test]
        public void VirtualCommandIsCalled()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualCommand hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualNoOverrideCommand hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideCommand hostBehaviour, NetworkServer.localConnection);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideCommandWithBase hostBehaviour, NetworkServer.localConnection);

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

        [Test]
        public void OverrideVirtualWithBaseCallsAllMethodsThatCallBase()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out VirtualOverrideCommandWithBase2 hostBehaviour, NetworkServer.localConnection);

            const int someInt = 20;

            int virtualCallCount = 0;
            int overrideCallCount = 0;
            int override2CallCount = 0;
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
            hostBehaviour.onOverrideSendInt2 += incomingInt =>
            {
                override2CallCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };


            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(virtualCallCount, Is.EqualTo(1));
            Assert.That(overrideCallCount, Is.EqualTo(1));
            Assert.That(override2CallCount, Is.EqualTo(1));
        }
    }
}
