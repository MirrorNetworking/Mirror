using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class HostComponentTests : HostSetup<MockComponent>
    {
        [Test]
        public void ServerRpcWithoutAuthority()
        {
            var gameObject2 = new GameObject();
            MockComponent rpcComponent2 = gameObject2.AddComponent<MockComponent>();

            // spawn it without client authority
            server.Spawn(gameObject2);

            // process spawn message from server
            client.Update();

            // only authorized clients can call ServerRpc
            Assert.Throws<UnauthorizedAccessException>(() =>
           {
               rpcComponent2.Test(1, "hello");
           });

        }

        [UnityTest]
        public IEnumerator ServerRpc() => RunAsync(async () =>
        {
            component.Test(1, "hello");

            await WaitFor(() => component.cmdArg1 != 0);

            Assert.That(component.cmdArg1, Is.EqualTo(1));
            Assert.That(component.cmdArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ServerRpcWithNetworkIdentity() => RunAsync(async () =>
        {
            component.CmdNetworkIdentity(identity);

            await WaitFor(() => component.cmdNi != null);

            Assert.That(component.cmdNi, Is.SameAs(identity));
        });

        [UnityTest]
        public IEnumerator ClientRpc() => RunAsync(async () =>
        {
            component.RpcTest(1, "hello");
            // process spawn message from server
            await WaitFor(() => component.rpcArg1 != 0);

            Assert.That(component.rpcArg1, Is.EqualTo(1));
            Assert.That(component.rpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientConnRpc() => RunAsync(async () =>
        {
            component.ClientConnRpcTest(manager.server.LocalConnection, 1, "hello");
            // process spawn message from server
            await WaitFor(() => component.targetRpcArg1 != 0);

            Assert.That(component.targetRpcConn, Is.SameAs(manager.client.Connection));
            Assert.That(component.targetRpcArg1, Is.EqualTo(1));
            Assert.That(component.targetRpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientOwnerRpc() => RunAsync(async () =>
        {
            component.RpcOwnerTest(1, "hello");
            // process spawn message from server
            await WaitFor(() => component.rpcOwnerArg1 != 0);

            Assert.That(component.rpcOwnerArg1, Is.EqualTo(1));
            Assert.That(component.rpcOwnerArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator DisconnectHostTest() => RunAsync(async () =>
        {
            // set local connection
            Assert.That(server.LocalClientActive, Is.True);
            Assert.That(server.connections, Has.Count.EqualTo(1));

            server.Disconnect();

            // wait for messages to get dispatched
            await WaitFor(() => !server.LocalClientActive);

            // state cleared?
            Assert.That(server.connections, Is.Empty);
            Assert.That(server.Active, Is.False);
            Assert.That(server.LocalConnection, Is.Null);
            Assert.That(server.LocalClientActive, Is.False);
        });

    }
}
