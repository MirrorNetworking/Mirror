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
            RpcNetworkIdentityBehaviour hostBehaviour = CreateHostObject<RpcNetworkIdentityBehaviour>(true);

            NetworkIdentity expected = CreateHostObject<RpcNetworkIdentityBehaviour>(true).netIdentity;

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
            RpcNetworkIdentityBehaviour hostBehaviour = CreateHostObject<RpcNetworkIdentityBehaviour>(true);

            GameObject expected = CreateHostObject<RpcNetworkIdentityBehaviour>(true).gameObject;

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
            RpcNetworkIdentityBehaviour hostBehaviour = CreateHostObject<RpcNetworkIdentityBehaviour>(true);

            RpcNetworkIdentityBehaviour expected = CreateHostObject<RpcNetworkIdentityBehaviour>(true);

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
            RpcNetworkIdentityBehaviour hostBehaviour = CreateHostObject<RpcNetworkIdentityBehaviour>(true);

            RpcNetworkIdentityBehaviour expected = CreateHostObject<RpcNetworkIdentityBehaviour>(true);

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
