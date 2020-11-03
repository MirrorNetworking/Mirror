using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Guid = System.Guid;
using Object = UnityEngine.Object;
using NSubstitute;
using Cysharp.Threading.Tasks;

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
        public IEnumerator ServerRpc() => UniTask.ToCoroutine(async () =>
        {
            clientComponent.Test(1, "hello");

            await AsyncUtil.WaitUntilWithTimeout(() => serverComponent.cmdArg1 != 0);

            Assert.That(serverComponent.cmdArg1, Is.EqualTo(1));
            Assert.That(serverComponent.cmdArg2, Is.EqualTo("hello"));
        });


        [UnityTest]
        public IEnumerator ServerRpcReturn() => UniTask.ToCoroutine(async () =>
        {
            int random = Random.Range(1, 100);
            serverComponent.rpcResult = random;
            int result = await clientComponent.GetResult();
            Assert.That(result, Is.EqualTo(random));
        });

        [UnityTest]
        public IEnumerator ServerRpcWithNetworkIdentity() => UniTask.ToCoroutine(async () =>
        {
            clientComponent.CmdNetworkIdentity(clientIdentity);

            await AsyncUtil.WaitUntilWithTimeout(() => serverComponent.cmdNi != null);

            Assert.That(serverComponent.cmdNi, Is.SameAs(serverIdentity));
        });

        [UnityTest]
        public IEnumerator ClientRpc() => UniTask.ToCoroutine(async () =>
        {
            serverComponent.RpcTest(1, "hello");
            // process spawn message from server
            await AsyncUtil.WaitUntilWithTimeout(() => clientComponent.rpcArg1 != 0);

            Assert.That(clientComponent.rpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.rpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientConnRpc() => UniTask.ToCoroutine(async () =>
        {
            serverComponent.ClientConnRpcTest(connectionToClient, 1, "hello");
            // process spawn message from server
            await AsyncUtil.WaitUntilWithTimeout(() => clientComponent.targetRpcArg1 != 0);

            Assert.That(clientComponent.targetRpcConn, Is.SameAs(connectionToServer));
            Assert.That(clientComponent.targetRpcArg1, Is.EqualTo(1));
            Assert.That(clientComponent.targetRpcArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator ClientOwnerRpc() => UniTask.ToCoroutine(async () =>
        {
            serverComponent.RpcOwnerTest(1, "hello");
            // process spawn message from server
            await AsyncUtil.WaitUntilWithTimeout(() => clientComponent.rpcOwnerArg1 != 0);

            Assert.That(clientComponent.rpcOwnerArg1, Is.EqualTo(1));
            Assert.That(clientComponent.rpcOwnerArg2, Is.EqualTo("hello"));
        });

        [UnityTest]
        public IEnumerator OnSpawnSpawnHandlerTest() => UniTask.ToCoroutine(async () =>
        {
            spawnDelegateTestCalled = 0;
            var guid = Guid.NewGuid();
            var gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            identity.AssetId = guid;

            clientObjectManager.RegisterSpawnHandler(guid, SpawnDelegateTest, go => { });
            clientObjectManager.RegisterPrefab(gameObject, guid);
            serverObjectManager.SendSpawnMessage(identity, connectionToClient);

            await AsyncUtil.WaitUntilWithTimeout(() => spawnDelegateTestCalled != 0);

            Assert.That(spawnDelegateTestCalled, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator OnDestroySpawnHandlerTest() => UniTask.ToCoroutine(async () =>
        {
            spawnDelegateTestCalled = 0;
            var guid = Guid.NewGuid();
            var gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            identity.AssetId = guid;

            var unspawnDelegate = Substitute.For<UnSpawnDelegate>();

            clientObjectManager.RegisterSpawnHandler(guid, SpawnDelegateTest, unspawnDelegate);
            clientObjectManager.RegisterPrefab(gameObject, guid);
            serverObjectManager.SendSpawnMessage(identity, connectionToClient);

            await AsyncUtil.WaitUntilWithTimeout(() => spawnDelegateTestCalled != 0);

            clientObjectManager.OnObjectDestroy(new ObjectDestroyMessage
            {
                netId = identity.NetId
            });
            unspawnDelegate.Received().Invoke(Arg.Any<GameObject>());
        });

        int spawnDelegateTestCalled;
        GameObject SpawnDelegateTest(Vector3 position, Guid assetId)
        {
            spawnDelegateTestCalled++;

            GameObject prefab = clientObjectManager.GetPrefab(assetId);
            if (!(prefab is null))
            {
                return Object.Instantiate(prefab);
            }
            return null;
        }

        [UnityTest]
        public IEnumerator ClientDisconnectTest() => UniTask.ToCoroutine(async () =>
        {
            client.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => client.connectState == ConnectState.Disconnected);
        });
    }
}
