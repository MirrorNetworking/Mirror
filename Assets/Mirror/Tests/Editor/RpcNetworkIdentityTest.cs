using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class RpcNetworkIdentityBehaviour : NetworkBehaviour
    {
        public event Action<NetworkIdentity> onSendNetworkIdentityCalled;
        public event Action<GameObject> onSendGameObjectCalled;
        public event Action<NetworkBehaviour> onSendNetworkBehaviourCalled;
        public event Action<RpcNetworkIdentityBehaviour> onSendNetworkBehaviourDerivedCalled;

        [ClientRpc]
        public void SendNetworkIdentity(NetworkIdentity value)
        {
            onSendNetworkIdentityCalled?.Invoke(value);
        }

        [ClientRpc]
        public void SendGameObject(GameObject value)
        {
            onSendGameObjectCalled?.Invoke(value);
        }

        [ClientRpc]
        public void SendNetworkBehaviour(NetworkBehaviour value)
        {
            onSendNetworkBehaviourCalled?.Invoke(value);
        }

        [ClientRpc]
        public void SendNetworkBehaviourDerived(RpcNetworkIdentityBehaviour value)
        {
            onSendNetworkBehaviourDerivedCalled?.Invoke(value);
        }
    }

    [Description("Test for sending NetworkIdentity fields (NI/GO/NB) in RPC")]
    public class RpcNetworkIdentityTest : RemoteTestBase
    {
        [Test]
        public void RpcCanSendNetworkIdentity()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcNetworkIdentityBehaviour hostBehaviour, NetworkServer.localConnection);
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity expected, out RpcNetworkIdentityBehaviour _, NetworkServer.localConnection);

            int callCount = 0;
            hostBehaviour.onSendNetworkIdentityCalled += actual =>
            {
                callCount++;
                Assert.That(actual, Is.EqualTo(expected));
            };
            hostBehaviour.SendNetworkIdentity(expected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcCanSendGameObject()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcNetworkIdentityBehaviour hostBehaviour, NetworkServer.localConnection);
            CreateNetworkedAndSpawn(out GameObject expected, out NetworkIdentity _, out RpcNetworkIdentityBehaviour _, NetworkServer.localConnection);

            int callCount = 0;
            hostBehaviour.onSendGameObjectCalled += actual =>
            {
                callCount++;
                Assert.That(actual, Is.EqualTo(expected));
            };
            hostBehaviour.SendGameObject(expected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcCanSendNetworkBehaviour()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcNetworkIdentityBehaviour hostBehaviour, NetworkServer.localConnection);
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcNetworkIdentityBehaviour expected, NetworkServer.localConnection);

            int callCount = 0;
            hostBehaviour.onSendNetworkBehaviourCalled += actual =>
            {
                callCount++;
                Assert.That(actual, Is.EqualTo(expected));
            };
            hostBehaviour.SendNetworkBehaviour(expected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcCanSendNetworkBehaviourDerived()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcNetworkIdentityBehaviour hostBehaviour, NetworkServer.localConnection);
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out RpcNetworkIdentityBehaviour expected, NetworkServer.localConnection);

            int callCount = 0;
            hostBehaviour.onSendNetworkBehaviourDerivedCalled += actual =>
             {
                 callCount++;
                 Assert.That(actual, Is.EqualTo(expected));
             };
            hostBehaviour.SendNetworkBehaviourDerived(expected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}
