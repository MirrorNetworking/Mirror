using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class TargetRpcBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [TargetRpc]
        public void TargetSendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    public class TargetRpcTest : RemoteTestBase
    {
        [Test]
        public void RpcIsCalled()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(true);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.TargetSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}
