using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_GetPrefab : ClientSceneTestsBase
    {
        [Test]
        public void ReturnsFalseForEmptyGuid()
        {
            bool result = NetworkClient.GetPrefab(new Guid(), out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsFalseForPrefabNotFound()
        {
            Guid guid = Guid.NewGuid();
            bool result = NetworkClient.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsFalseForPrefabIsNull()
        {
            Guid guid = Guid.NewGuid();
            NetworkClient.prefabs.Add(guid, null);
            bool result = NetworkClient.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsTrueWhenPrefabIsFound()
        {
            NetworkClient.prefabs.Add(validPrefabGuid, validPrefab);
            bool result = NetworkClient.GetPrefab(validPrefabGuid, out GameObject prefab);

            Assert.IsTrue(result);
            Assert.NotNull(prefab);
        }

        [Test]
        public void HasOutPrefabWithCorrectGuid()
        {
            NetworkClient.prefabs.Add(validPrefabGuid, validPrefab);
            NetworkClient.GetPrefab(validPrefabGuid, out GameObject prefab);


            Assert.NotNull(prefab);

            NetworkIdentity networkID = prefab.GetComponent<NetworkIdentity>();
            Assert.AreEqual(networkID.assetId, validPrefabGuid);
        }
    }
}
