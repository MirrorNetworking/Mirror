using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class ClientServerComponentTests : ClientServerSetup<MockComponent>
    {
        /*
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
        */



        [Test]
        public void CheckNotHost()
        {
            Assert.That(serverPlayerGO, Is.Not.SameAs(clientPlayerGO));

            Assert.That(serverPlayerGO, Is.Not.Null);
            Assert.That(clientPlayerGO, Is.Not.Null);
        }


        [UnityTest]
        public IEnumerator Command()
        {
            clientComponent.CmdTest(1, "hello");
            yield return null;

            Assert.That(serverComponent.cmdArg1, Is.EqualTo(1));
            Assert.That(serverComponent.cmdArg2, Is.EqualTo("hello"));
        }

        
        [UnityTest]
        public IEnumerator CommandWithNetworkIdentity()
        {
            clientComponent.CmdNetworkIdentity(clientIdentity);

            yield return null;

            Assert.That(serverComponent.cmdNi, Is.SameAs(serverIdentity));
        }

        [UnityTest]
        public IEnumerator ClientRpc()
        {
            serverComponent.RpcTest(1, "hello");
            // process spawn message from server
            yield return null;

            Assert.That(clientComponent.rpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.rpcArg2, Is.EqualTo("hello"));
        }

        [UnityTest]
        public IEnumerator TargetRpc()
        {
            serverComponent.TargetRpcTest(connectionToClient, 1, "hello");
            // process spawn message from server
            yield return null;

            Assert.That(clientComponent.targetRpcConn, Is.SameAs(connectionToServer));
            Assert.That(clientComponent.targetRpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.targetRpcArg2, Is.EqualTo("hello"));
        }

        /*
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
        */

    }
}
