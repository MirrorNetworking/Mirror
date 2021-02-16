using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class ClientRpcBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [ClientRpc]
        public void SendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class ExcludeOwnerBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [ClientRpc(includeOwner = false)]
        public void RpcSendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    public class ClientRpcTest : RemoteTestBase
    {
        [Test]
        public void RpcIsCalled()
        {
            ClientRpcBehaviour hostBehaviour = CreateHostObject<ClientRpcBehaviour>(true);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcIsCalledForNotOwnerd()
        {
            bool owner = false;
            ExcludeOwnerBehaviour hostBehaviour = CreateHostObject<ExcludeOwnerBehaviour>(owner);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcNotCalledForOwnerd()
        {
            bool owner = true;
            ExcludeOwnerBehaviour hostBehaviour = CreateHostObject<ExcludeOwnerBehaviour>(owner);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.RpcSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(0));
        }
    }
}
