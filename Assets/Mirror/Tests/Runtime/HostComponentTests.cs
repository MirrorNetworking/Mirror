using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class HostComponentTests : HostSetup<MockComponent>
    {
        [Test]
        public void CommandWithoutAuthority()
        {
            var gameObject2 = new GameObject();
            MockComponent rpcComponent2 = gameObject2.AddComponent<MockComponent>();

            // spawn it without client authority
            server.Spawn(gameObject2);

            // process spawn message from server
            client.Update();

            // only authorized clients can call command
            Assert.Throws<UnauthorizedAccessException>(() =>
           {
               rpcComponent2.CmdTest(1, "hello");
           });

        }

        [UnityTest]
        public IEnumerator Command()
        {
            component.CmdTest(1, "hello");
            yield return null;

            Assert.That(component.cmdArg1, Is.EqualTo(1));
            Assert.That(component.cmdArg2, Is.EqualTo("hello"));
        }

        [UnityTest]
        public IEnumerator CommandWithNetworkIdentity()
        {
            component.CmdNetworkIdentity(identity);

            yield return null;

            Assert.That(component.cmdNi, Is.SameAs(identity));
        }

        [UnityTest]
        public IEnumerator ClientRpc()
        {
            component.RpcTest(1, "hello");
            // process spawn message from server
            yield return null;

            Assert.That(component.rpcArg1, Is.EqualTo(1));
            Assert.That(component.rpcArg2, Is.EqualTo("hello"));
        }

        [UnityTest]
        public IEnumerator TargetRpc()
        {
            component.TargetRpcTest(manager.server.LocalConnection, 1, "hello");
            // process spawn message from server
            yield return null;

            Assert.That(component.targetRpcConn, Is.SameAs(manager.client.Connection));
            Assert.That(component.targetRpcArg1, Is.EqualTo(1));
            Assert.That(component.targetRpcArg2, Is.EqualTo("hello"));
        }

        [UnityTest]
        public IEnumerator DisconnectHostTest()
        {
            // set local connection
            Assert.That(server.LocalClientActive, Is.True);
            Assert.That(server.connections, Has.Count.EqualTo(1));

            server.Disconnect();

            // wait for messages to get dispatched
            yield return null;

            // state cleared?
            Assert.That(server.connections, Is.Empty);
            Assert.That(server.Active, Is.False);
            Assert.That(server.LocalConnection, Is.Null);
            Assert.That(server.LocalClientActive, Is.False);
        }

    }
}
