using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_RegisterSpawnHandler : NetworkClientTestsBase
    {
        [Test]
        public void SpawnDelegate_AddsHandlerToSpawnHandlers()
        {
            int handlerCalled = 0;

            uint assetId = 42;
            SpawnDelegate spawnHandler = new SpawnDelegate((pos, rot) =>
            {
                handlerCalled++;
                return null;
            });
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));

            // check spawnHandler above is called
            SpawnHandlerDelegate handler = NetworkClient.spawnHandlers[assetId];
            handler.Invoke(default);
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void SpawnDelegate_AddsHandlerToSpawnHandlersWithCorrectArguments()
        {
            int handlerCalled = 0;
            Vector3 somePosition = new Vector3(10, 20, 3);

            uint assetId = 42;
            SpawnDelegate spawnHandler = new SpawnDelegate((pos, id) =>
            {
                handlerCalled++;
                Assert.That(pos, Is.EqualTo(somePosition));
                Assert.That(id, Is.EqualTo(assetId));
                return null;
            });
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));

            // check spawnHandler above is called
            SpawnHandlerDelegate handler = NetworkClient.spawnHandlers[assetId];
            handler.Invoke(new SpawnMessage { position = somePosition, assetId = assetId });
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void SpawnDelegate_AddsHandlerToUnSpawnHandlers()
        {
            uint assetId = 42;
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            Assert.IsTrue(NetworkClient.unspawnHandlers.ContainsKey(assetId));
            Assert.AreEqual(NetworkClient.unspawnHandlers[assetId], unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            uint assetId = 42;
            SpawnDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {assetId}");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            uint assetId = 42;
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {assetId}");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenAssetIdIsEmpty()
        {
            uint assetId = 0;
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty assetId");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
        {
            uint assetId = 42;
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            SpawnDelegate spawnHandler2 = new SpawnDelegate((x, y) => new GameObject());
            UnSpawnDelegate unspawnHandler2 = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Warning, $"Replacing existing spawnHandlers for {assetId}");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler2, unspawnHandler2);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            uint assetId = 42;
            NetworkClient.prefabs.Add(assetId, validPrefab);

            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, $"assetId '{assetId}' is already used by prefab '{validPrefab.name}'");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }


        [Test]
        public void SpawnHandlerDelegate_AddsHandlerToSpawnHandlers()
        {
            uint assetId = 42;
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));
            Assert.AreEqual(NetworkClient.spawnHandlers[assetId], spawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_AddsHandlerToUnSpawnHandlers()
        {
            uint assetId = 42;
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            Assert.IsTrue(NetworkClient.unspawnHandlers.ContainsKey(assetId));
            Assert.AreEqual(NetworkClient.unspawnHandlers[assetId], unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            uint assetId = 42;
            SpawnHandlerDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {assetId}");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            uint assetId = 42;
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {assetId}");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenAssetIdIsEmpty()
        {
            uint assetId = 0;
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty assetId");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
        {
            uint assetId = 42;
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

            SpawnHandlerDelegate spawnHandler2 = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler2 = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Warning, $"Replacing existing spawnHandlers for {assetId}");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler2, unspawnHandler2);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            uint assetId = 42;
            NetworkClient.prefabs.Add(assetId, validPrefab);

            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Error, $"assetId '{assetId}' is already used by prefab '{validPrefab.name}'");
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

    }
}
