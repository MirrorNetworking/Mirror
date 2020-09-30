using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Guid = System.Guid;
using Object = UnityEngine.Object;
using static Mirror.Tests.AsyncUtil;
using NSubstitute;

namespace Mirror.Tests
{
    public class ClientServerComponentTests : ClientServerSetup<MockComponent>
    {
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
        public IEnumerator ClientConnRpc() => RunAsync(async () =>
        {
            serverComponent.ClientConnRpcTest(connectionToClient, 1, "hello");
            // process spawn message from server
            await WaitFor(() => clientComponent.targetRpcArg1 != 0);

            Assert.That(clientComponent.targetRpcConn, Is.SameAs(connectionToServer));
            Assert.That(clientComponent.targetRpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.targetRpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientOwnerRpc() => RunAsync(async () =>
        {
            serverComponent.RpcOwnerTest(1, "hello");
            // process spawn message from server
            await WaitFor(() => clientComponent.rpcOwnerArg1 != 0);

            Assert.That(clientComponent.rpcOwnerArg1, Is.EqualTo(1));
            Assert.That(clientComponent.rpcOwnerArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator OnSpawnSpawnHandlerTest() => RunAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            identity.AssetId = guid;

            client.RegisterSpawnHandler(guid, SpawnDelegateTest, go => { });
            client.RegisterPrefab(gameObject, guid);
            server.SendSpawnMessage(identity, connectionToClient);

            await WaitFor(() => spawnDelegateTestCalled != 0);

            Assert.That(spawnDelegateTestCalled, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator OnDestroySpawnHandlerTest() => RunAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            identity.AssetId = guid;

            var unspawnDelegate = Substitute.For<UnSpawnDelegate>();

            client.RegisterSpawnHandler(guid, SpawnDelegateTest, unspawnDelegate);
            client.RegisterPrefab(gameObject, guid);
            server.SendSpawnMessage(identity, connectionToClient);

            await WaitFor(() => spawnDelegateTestCalled != 0);

            client.OnObjectDestroy(new ObjectDestroyMessage
            {
                netId = identity.NetId
            });
            unspawnDelegate.Received().Invoke(Arg.Any<GameObject>());
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

        [UnityTest]
        public IEnumerator ClientDisconnectTest() => RunAsync(async () =>
        {
            client.Disconnect();

            await WaitFor(() => client.connectState == ConnectState.Disconnected);
        });
    }
}
