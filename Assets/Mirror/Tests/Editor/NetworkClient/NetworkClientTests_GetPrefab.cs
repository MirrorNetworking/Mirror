using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_GetPrefab : NetworkClientTestsBase
    {
        [Test]
        public void ReturnsFalseForEmptyGuid()
        {
            bool result = NetworkClient.GetPrefab(0, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsFalseForPrefabNotFound()
        {
            bool result = NetworkClient.GetPrefab(42, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsFalseForNullPrefab()
        {
            NetworkClient.prefabs.Add(42, null);
            bool result = NetworkClient.GetPrefab(42, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsTrueWhenPrefabIsFound()
        {
            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);
            bool result = NetworkClient.GetPrefab(validPrefabAssetId, out GameObject prefab);

            Assert.IsTrue(result);
            Assert.NotNull(prefab);
        }

        [Test]
        public void HasOutPrefabWithCorrectGuid()
        {
            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);
            NetworkClient.GetPrefab(validPrefabAssetId, out GameObject prefab);


            Assert.NotNull(prefab);

            NetworkIdentity networkID = prefab.GetComponent<NetworkIdentity>();
            Assert.AreEqual(networkID.assetId, validPrefabAssetId);
        }
    }
}
