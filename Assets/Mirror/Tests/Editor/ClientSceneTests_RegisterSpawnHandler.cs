using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_RegisterSpawnHandler : ClientSceneTestsBase
    {
        [Test]
        public void SpawnDelegate_AddsHandlerToSpawnHandlers()
        {
            int handlerCalled = 0;

            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((pos, rot) =>
            {
                handlerCalled++;
                return null;
            });
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));

            // check spawnHandler above is called
            SpawnHandlerDelegate handler = spawnHandlers[guid];
            handler.Invoke(default);
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void SpawnDelegate_AddsHandlerToSpawnHandlersWithCorrectArguments()
        {
            int handlerCalled = 0;
            Vector3 somePosition = new Vector3(10, 20, 3);

            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((pos, assetId) =>
            {
                handlerCalled++;
                Assert.That(pos, Is.EqualTo(somePosition));
                Assert.That(assetId, Is.EqualTo(guid));
                return null;
            });
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));

            // check spawnHandler above is called
            SpawnHandlerDelegate handler = spawnHandlers[guid];
            handler.Invoke(new SpawnMessage { position = somePosition, assetId = guid });
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void SpawnDelegate_AddsHandlerToUnSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));
            Assert.AreEqual(unspawnHandlers[guid], unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenAssetIdIsEmpty()
        {
            Guid guid = new Guid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty Guid");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            SpawnDelegate spawnHandler2 = new SpawnDelegate((x, y) => new GameObject());
            UnSpawnDelegate unspawnHandler2 = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Warning, $"Replacing existing spawnHandlers for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler2, unspawnHandler2);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, validPrefab);

            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}'");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }


        [Test]
        public void SpawnHandlerDelegate_AddsHandlerToSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));
            Assert.AreEqual(spawnHandlers[guid], spawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_AddsHandlerToUnSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));
            Assert.AreEqual(unspawnHandlers[guid], unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenAssetIdIsEmpty()
        {
            Guid guid = new Guid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty Guid");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            SpawnHandlerDelegate spawnHandler2 = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler2 = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Warning, $"Replacing existing spawnHandlers for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler2, unspawnHandler2);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, validPrefab);

            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}'");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

    }
}
