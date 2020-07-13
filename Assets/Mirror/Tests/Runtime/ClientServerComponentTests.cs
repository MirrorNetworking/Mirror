using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Guid = System.Guid;
using Object = UnityEngine.Object;
using static Mirror.Tests.AsyncUtil;
using System.Threading.Tasks;

namespace Mirror.Tests
{
    public class ClientServerComponentTests : ClientServerSetup<MockComponent>
    {
        /*
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
        public IEnumerator ServerRpc() => RunAsync(async () =>
        {
            clientComponent.Test(1, "hello");

            await WaitFor(() => serverComponent.cmdArg1 != 0);

            Assert.That(serverComponent.cmdArg1, Is.EqualTo(1));
            Assert.That(serverComponent.cmdArg2, Is.EqualTo("hello"));
        });


        [UnityTest]
        public IEnumerator ServerRpcWithNetworkIdentity() => RunAsync(async () =>
        {
            clientComponent.CmdNetworkIdentity(clientIdentity);

            await WaitFor(() => serverComponent.cmdNi != null);

            Assert.That(serverComponent.cmdNi, Is.SameAs(serverIdentity));
        });

        [UnityTest]
        public IEnumerator ClientRpc() => RunAsync(async () =>
        {
            serverComponent.RpcTest(1, "hello");
            // process spawn message from server
            await WaitFor(() => clientComponent.rpcArg1 != 0);

            Assert.That(clientComponent.rpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.rpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator TargetRpc() => RunAsync(async () =>
        {
            serverComponent.TargetRpcTest(connectionToClient, 1, "hello");
            // process spawn message from server
            await WaitFor(() => clientComponent.targetRpcArg1 != 0);

            Assert.That(clientComponent.targetRpcConn, Is.SameAs(connectionToServer));
            Assert.That(clientComponent.targetRpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.targetRpcArg2, Is.EqualTo("hello"));
        });

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

        [UnityTest]
        public IEnumerator OnSpawnSpawnHandlerTest() => RunAsync(async () =>
        {
            Guid guid = Guid.NewGuid();
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            identity.AssetId = guid;

            client.RegisterSpawnHandler(guid, SpawnDelegateTest, UnSpawnDelegateTest);
            client.RegisterPrefab(gameObject, guid);
            server.SendSpawnMessage(identity, connectionToClient);

            await WaitFor(() => spawnDelegateTestCalled != 0);

            Assert.That(spawnDelegateTestCalled, Is.EqualTo(1));
        });

        int spawnDelegateTestCalled;
        GameObject SpawnDelegateTest(Vector3 position, Guid assetId)
        {
            spawnDelegateTestCalled++;

            if (client.GetPrefab(assetId, out GameObject prefab))
            {
                return Object.Instantiate(prefab);
            }
            return null;
        }

        int unspawnDelegateTestCalled;
        void UnSpawnDelegateTest(GameObject obj)
        {
            unspawnDelegateTestCalled++;
        }

    }
}
