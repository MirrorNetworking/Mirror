using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public abstract class ClientSceneTestsBase
    {
        protected const string NewAssetIdIgnoreMessage = "Ignoring this test till we know how to fix it, see https://github.com/vis2k/Mirror/issues/1831";

        // use guid to find asset so that the path does not matter
        protected const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
        protected const string PrefabWithChildrenAssetGuid = "a78e009e3f2dee44e8859516974ede43";
        protected const string InvalidPrefabAssetGuid = "78f0a3f755d35324e959f3ecdd993fb0";
        // random guid, not used anywhere
        protected const string AnotherGuidString = "5794128cdfda04542985151f82990d05";

        protected GameObject validPrefab;
        protected NetworkIdentity validPrefabNetId;
        protected GameObject prefabWithChildren;
        protected GameObject invalidPrefab;
        protected Guid validPrefabGuid;
        protected Guid anotherGuid;

        protected Dictionary<Guid, GameObject> prefabs => ClientScene.prefabs;
        protected Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers => ClientScene.spawnHandlers;
        protected Dictionary<Guid, UnSpawnDelegate> unspawnHandlers => ClientScene.unspawnHandlers;

        static GameObject LoadPrefab(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            validPrefab = LoadPrefab(ValidPrefabAssetGuid);
            validPrefabNetId = validPrefab.GetComponent<NetworkIdentity>();
            prefabWithChildren = LoadPrefab(PrefabWithChildrenAssetGuid);
            invalidPrefab = LoadPrefab(InvalidPrefabAssetGuid);
            validPrefabGuid = new Guid(ValidPrefabAssetGuid);
            anotherGuid = new Guid(AnotherGuidString);
        }

        [TearDown]
        public virtual void TearDown()
        {
            ClientScene.Shutdown();
            // reset asset id incase they are changed by tests
            validPrefabNetId.assetId = validPrefabGuid;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            validPrefab = null;
            prefabWithChildren = null;
            invalidPrefab = null;
        }
    }
}
