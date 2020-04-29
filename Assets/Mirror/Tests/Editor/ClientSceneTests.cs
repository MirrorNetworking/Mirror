using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mirror.Tests
{
    public class ClientSceneTests
    {
        // use guid to find asset so that the path does not matter
        const string ValidPrefabGuid = "33169286da0313d45ab5bfccc6cf3775";

        GameObject validPrefab;


        static GameObject LoadPrefab(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [SetUp]
        public void SetUp()
        {
            validPrefab = LoadPrefab(ValidPrefabGuid);
        }

        [TearDown]
        public void TearDown()
        {
            ClientScene.Shutdown();
            validPrefab = null;
        }


        [Test]
        public void GetPrefab_ReturnsFalseForEmptyGuid()
        {
            bool result = ClientScene.GetPrefab(new System.Guid(), out GameObject prefab);

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
            ClientScene.prefabs.Add(guid, null);
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsTrueWhenPrefabIsFound()
        {
            Guid guid = new Guid(ValidPrefabGuid);
            ClientScene.prefabs.Add(guid, validPrefab);
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsTrue(result);
            Assert.NotNull(prefab);
        }

        [Test]
        public void GetPrefab_HasOutPrefabWithCorrectGuid()
        {
            Guid guid = new Guid(ValidPrefabGuid);
            ClientScene.prefabs.Add(guid, validPrefab);
            ClientScene.GetPrefab(guid, out GameObject prefab);


            Assert.NotNull(prefab);

            NetworkIdentity networkID = prefab.GetComponent<NetworkIdentity>();
            Assert.AreEqual(networkID.assetId, guid);
        }
    }
}
