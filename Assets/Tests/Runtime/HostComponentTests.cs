using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NSubstitute;
using UnityEngine.Events;

namespace Mirror.Tests
{
    public class HostComponentTests : HostSetup<MockComponent>
    {
        [Test]
        public void ServerRpcWithoutAuthority()
        {
            var gameObject2 = new GameObject("rpcObject", typeof(NetworkIdentity), typeof(MockComponent));
            MockComponent rpcComponent2 = gameObject2.GetComponent<MockComponent>();

            // spawn it without client authority
            serverObjectManager.Spawn(gameObject2);

            // process spawn message from server
            client.Update();

            // only authorized clients can call ServerRpc
            Assert.Throws<UnauthorizedAccessException>(() =>
           {
               rpcComponent2.Test(1, "hello");
           });

        }

        [UnityTest]
        public IEnumerator ServerRpc() => UniTask.ToCoroutine(async () =>
        {
            component.Test(1, "hello");

            await AsyncUtil.WaitUntilWithTimeout(() => component.cmdArg1 != 0);

            Assert.That(component.cmdArg1, Is.EqualTo(1));
            Assert.That(component.cmdArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ServerRpcWithNetworkIdentity() => UniTask.ToCoroutine(async () =>
        {
            component.CmdNetworkIdentity(identity);

            await AsyncUtil.WaitUntilWithTimeout(() => component.cmdNi != null);

            Assert.That(component.cmdNi, Is.SameAs(identity));
        });

        [UnityTest]
        public IEnumerator ClientRpc() => UniTask.ToCoroutine(async () =>
        {
            component.RpcTest(1, "hello");
            // process spawn message from server
            await AsyncUtil.WaitUntilWithTimeout(() => component.rpcArg1 != 0);

            Assert.That(component.rpcArg1, Is.EqualTo(1));
            Assert.That(component.rpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientConnRpc() => UniTask.ToCoroutine(async () =>
        {
            component.ClientConnRpcTest(manager.server.LocalConnection, 1, "hello");
            // process spawn message from server
            await AsyncUtil.WaitUntilWithTimeout(() => component.targetRpcArg1 != 0);

            Assert.That(component.targetRpcConn, Is.SameAs(manager.client.Connection));
            Assert.That(component.targetRpcArg1, Is.EqualTo(1));
            Assert.That(component.targetRpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientOwnerRpc() => UniTask.ToCoroutine(async () =>
        {
            component.RpcOwnerTest(1, "hello");
            // process spawn message from server
            await AsyncUtil.WaitUntilWithTimeout(() => component.rpcOwnerArg1 != 0);

            Assert.That(component.rpcOwnerArg1, Is.EqualTo(1));
            Assert.That(component.rpcOwnerArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator DisconnectHostTest() => UniTask.ToCoroutine(async () =>
        {
            // set local connection
            Assert.That(server.LocalClientActive, Is.True);
            Assert.That(server.connections, Has.Count.EqualTo(1));

            server.Disconnect();

            // wait for messages to get dispatched
            await AsyncUtil.WaitUntilWithTimeout(() => !server.LocalClientActive);

            // state cleared?
            Assert.That(server.connections, Is.Empty);
            Assert.That(server.Active, Is.False);
            Assert.That(server.LocalConnection, Is.Null);
            Assert.That(server.LocalClientActive, Is.False);
        });

        [UnityTest]
        public IEnumerator ClientSceneChangedOnReconnect() => UniTask.ToCoroutine(async () =>
        {
            server.Disconnect();

            // wait for server to disconnect
            await UniTask.WaitUntil(() => !server.Active);

            var mockListener = Substitute.For<UnityAction<string, SceneOperation>>();
            sceneManager.ClientChangeScene.AddListener(mockListener);
            await StartHost();

            client.Update();
            mockListener.Received().Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        });
    }
}
