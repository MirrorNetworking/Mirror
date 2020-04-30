using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    [TestFixture]
    public class ClientSceneTests
    {
        // use guid to find asset so that the path does not matter
        const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
        const string InvalidPrefabAssetGuid = "78f0a3f755d35324e959f3ecdd993fb0";

        GameObject validPrefab;
        GameObject invalidPrefab;
        Guid validPrefabGuid;
        Guid invalidPrefabGuid;

        Dictionary<Guid, GameObject> prefabs => ClientScene.prefabs;
        Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers => ClientScene.spawnHandlers;
        Dictionary<Guid, UnSpawnDelegate> unspawnHandlers => ClientScene.unspawnHandlers;

        static GameObject LoadPrefab(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            validPrefab = LoadPrefab(ValidPrefabAssetGuid);
            invalidPrefab = LoadPrefab(InvalidPrefabAssetGuid);
            validPrefabGuid = new Guid(ValidPrefabAssetGuid);
            invalidPrefabGuid = new Guid(ValidPrefabAssetGuid);
        }

        [TearDown]
        public void TearDown()
        {
            ClientScene.Shutdown();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            validPrefab = null;
            invalidPrefab = null;
        }


        [Test]
        public void GetPrefab_ReturnsFalseForEmptyGuid()
        {
            bool result = ClientScene.GetPrefab(new Guid(), out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsFalseForPrefabNotFound()
        {
            Guid guid = Guid.NewGuid();
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsFalseForPrefabIsNull()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, null);
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsTrueWhenPrefabIsFound()
        {
            prefabs.Add(validPrefabGuid, validPrefab);
            bool result = ClientScene.GetPrefab(validPrefabGuid, out GameObject prefab);

            Assert.IsTrue(result);
            Assert.NotNull(prefab);
        }

        [Test]
        public void GetPrefab_HasOutPrefabWithCorrectGuid()
        {
            prefabs.Add(validPrefabGuid, validPrefab);
            ClientScene.GetPrefab(validPrefabGuid, out GameObject prefab);


            Assert.NotNull(prefab);

            NetworkIdentity networkID = prefab.GetComponent<NetworkIdentity>();
            Assert.AreEqual(networkID.assetId, validPrefabGuid);
        }


        [Test]
        public void UnregisterPrefab_RemovesPrefabFromDictionary()
        {
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(prefabs.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterPrefab_RemovesSpawnHandlerFromDictionary()
        {
            spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(spawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterPrefab_RemovesUnSpawnHandlerFromDictionary()
        {
            unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => { }));

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterPrefab_ErrorWhenPrefabIsNull()
        {
            LogAssert.Expect(LogType.Error, "Could not unregister prefab because it was null");
            ClientScene.UnregisterPrefab(null);
        }

        [Test]
        public void UnregisterPrefab_ErrorWhenPrefabHasNoNetworkIdentity()
        {
            LogAssert.Expect(LogType.Error, $"Could not unregister '{invalidPrefab.name}' since it contains no NetworkIdentity component");
            ClientScene.UnregisterPrefab(invalidPrefab);
        }


        [Test]
        public void UnregisterSpawnHandler_RemovesSpawnHandlersFromDictionary()
        {
            spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterSpawnHandler_RemovesUnSpawnHandlersFromDictionary()
        {
            unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => { }));

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterSpawnHandler_DoesNotRemovePrefabDictionary()
        {
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            // Should not be removed
            Assert.IsTrue(prefabs.ContainsKey(validPrefabGuid));
        }


        [Test]
        public void ClearSpawners_RemovesAllPrefabsFromDictionary()
        {
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(prefabs);
        }

        [Test]
        public void ClearSpawners_RemovesAllSpawnHandlersFromDictionary()
        {
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(spawnHandlers);
        }

        [Test]
        public void ClearSpawners_RemovesAllUnspawnHandlersFromDictionary()
        {
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(unspawnHandlers);
        }

        [Test]
        public void ClearSpawners_ClearsAllDictionary()
        {
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);

            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);

            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(prefabs);
            Assert.IsEmpty(spawnHandlers);
            Assert.IsEmpty(unspawnHandlers);
        }
    }
}
