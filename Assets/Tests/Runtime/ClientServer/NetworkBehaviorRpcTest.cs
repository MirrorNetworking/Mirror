using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using UnityEngine;
using UnityEngine.TestTools;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class SampleBehaviorWithRpc : NetworkBehaviour
    {
        public event Action<NetworkIdentity> onSendNetworkIdentityCalled;
        public event Action<GameObject> onSendGameObjectCalled;
        public event Action<NetworkBehaviour> onSendNetworkBehaviourCalled;
        public event Action<SampleBehaviorWithRpc> onSendNetworkBehaviourDerivedCalled;

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
        public void SendNetworkBehaviourDerived(SampleBehaviorWithRpc value)
        {
            onSendNetworkBehaviourDerivedCalled?.Invoke(value);
        }

        [ServerRpc]
        public void SendNetworkIdentityToServer(NetworkIdentity value)
        {
            onSendNetworkIdentityCalled?.Invoke(value);
        }

        [ServerRpc]
        public void SendGameObjectToServer(GameObject value)
        {
            onSendGameObjectCalled?.Invoke(value);
        }

        [ServerRpc]
        public void SendNetworkBehaviourToServer(NetworkBehaviour value)
        {
            onSendNetworkBehaviourCalled?.Invoke(value);
        }

        [ServerRpc]
        public void SendNetworkBehaviourDerivedToServer(SampleBehaviorWithRpc value)
        {
            onSendNetworkBehaviourDerivedCalled?.Invoke(value);
        }


    }

    public class NetworkBehaviorRPCTest : ClientServerSetup<SampleBehaviorWithRpc>
    {
        [UnityTest]
        public IEnumerator SendNetworkIdentity() => UniTask.ToCoroutine(async () =>
        {
            Action<NetworkIdentity> callback = Substitute.For<Action<NetworkIdentity>>();
            clientComponent.onSendNetworkIdentityCalled += callback;

            serverComponent.SendNetworkIdentity(serverIdentity);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(clientIdentity);
        });

        [UnityTest]
        public IEnumerator SendNetworkBehavior() => UniTask.ToCoroutine(async () =>
        {
            Action<NetworkBehaviour> callback = Substitute.For<Action<NetworkBehaviour>>();
            clientComponent.onSendNetworkBehaviourCalled += callback;

            serverComponent.SendNetworkBehaviour(serverComponent);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(clientComponent);
        });

        [UnityTest]
        public IEnumerator SendNetworkBehaviorChild() => UniTask.ToCoroutine(async () =>
        {
            Action<SampleBehaviorWithRpc> callback = Substitute.For<Action<SampleBehaviorWithRpc>>();
            clientComponent.onSendNetworkBehaviourDerivedCalled += callback;

            serverComponent.SendNetworkBehaviourDerived(serverComponent);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(clientComponent);
        });

        [UnityTest]
        public IEnumerator SendGameObject() => UniTask.ToCoroutine(async () =>
        {
            Action<GameObject> callback = Substitute.For<Action<GameObject>>();
            clientComponent.onSendGameObjectCalled += callback;

            serverComponent.SendGameObject(serverPlayerGO);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(clientPlayerGO);
        });

        [Test]
        public void SendInvalidGO()
        {
            Action<GameObject> callback = Substitute.For<Action<GameObject>>();
            clientComponent.onSendGameObjectCalled += callback;

            // this object does not have a NI, so this should error out
            Assert.Throws<InvalidOperationException>(() =>
            {
                serverComponent.SendGameObject(serverGo);
            });
        }

        [UnityTest]
        public IEnumerator SendNullNetworkIdentity() => UniTask.ToCoroutine(async () =>
        {
            Action<NetworkIdentity> callback = Substitute.For<Action<NetworkIdentity>>();
            clientComponent.onSendNetworkIdentityCalled += callback;

            serverComponent.SendNetworkIdentity(null);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(null);
        });

        [UnityTest]
        public IEnumerator SendNullNetworkBehavior() => UniTask.ToCoroutine(async () =>
        {
            Action<NetworkBehaviour> callback = Substitute.For<Action<NetworkBehaviour>>();
            clientComponent.onSendNetworkBehaviourCalled += callback;

            serverComponent.SendNetworkBehaviour(null);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(null);
        });

        [UnityTest]
        public IEnumerator SendNullNetworkBehaviorChild() => UniTask.ToCoroutine(async () =>
        {
            Action<SampleBehaviorWithRpc> callback = Substitute.For<Action<SampleBehaviorWithRpc>>();
            clientComponent.onSendNetworkBehaviourDerivedCalled += callback;

            serverComponent.SendNetworkBehaviourDerived(null);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(null);
        });

        [UnityTest]
        public IEnumerator SendNullGameObject() => UniTask.ToCoroutine(async () =>
        {
            Action<GameObject> callback = Substitute.For<Action<GameObject>>();
            clientComponent.onSendGameObjectCalled += callback;

            serverComponent.SendGameObject(null);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(null);
        });


        [UnityTest]
        public IEnumerator SendNetworkIdentityToServer() => UniTask.ToCoroutine(async () =>
        {
            Action<NetworkIdentity> callback = Substitute.For<Action<NetworkIdentity>>();
            serverComponent.onSendNetworkIdentityCalled += callback;

            clientComponent.SendNetworkIdentityToServer(clientIdentity);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(serverIdentity);
        });

        [UnityTest]
        public IEnumerator SendNetworkBehaviorToServer() => UniTask.ToCoroutine(async () =>
        {
            Action<NetworkBehaviour> callback = Substitute.For<Action<NetworkBehaviour>>();
            serverComponent.onSendNetworkBehaviourCalled += callback;

            clientComponent.SendNetworkBehaviourToServer(clientComponent);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(serverComponent);
        });

        [UnityTest]
        public IEnumerator SendNetworkBehaviorChildToServer() => UniTask.ToCoroutine(async () =>
        {
            Action<SampleBehaviorWithRpc> callback = Substitute.For<Action<SampleBehaviorWithRpc>>();
            serverComponent.onSendNetworkBehaviourDerivedCalled += callback;

            clientComponent.SendNetworkBehaviourDerivedToServer(clientComponent);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(serverComponent);
        });

        [UnityTest]
        public IEnumerator SendGameObjectToServer() => UniTask.ToCoroutine(async () =>
        {
            Action<GameObject> callback = Substitute.For<Action<GameObject>>();
            serverComponent.onSendGameObjectCalled += callback;

            clientComponent.SendGameObjectToServer(clientPlayerGO);
            await UniTask.WaitUntil(() => callback.ReceivedCalls().Any());
            callback.Received().Invoke(serverPlayerGO);
        });

        [Test]
        public void SendInvalidGOToServer()
        {
            Action<GameObject> callback = Substitute.For<Action<GameObject>>();
            serverComponent.onSendGameObjectCalled += callback;

            // this object does not have a NI, so this should error out
            Assert.Throws<InvalidOperationException>(() =>
            {
                clientComponent.SendGameObjectToServer(clientGo);
            });
        }
    }
}
