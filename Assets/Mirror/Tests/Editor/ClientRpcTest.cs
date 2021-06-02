using System;
using NUnit.Framework;
using UnityEngine;

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ClientRpcBehaviour hostBehaviour, NetworkServer.localConnection);

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
        public void RpcIsCalledForNotOwner()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ExcludeOwnerBehaviour hostBehaviour);

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
        public void RpcNotCalledForOwner()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ExcludeOwnerBehaviour hostBehaviour, NetworkServer.localConnection);

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
