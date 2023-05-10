using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_UnregisterPrefab : NetworkClientTestsBase
    {
        [Test]
        public void RemovesPrefabFromDictionary()
        {
            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);
            NetworkClient.UnregisterPrefab(validPrefab);
            Assert.IsFalse(NetworkClient.prefabs.ContainsKey(validPrefabAssetId));
        }

        [Test]
        public void RemovesSpawnHandlerFromDictionary()
        {
            NetworkClient.spawnHandlers.Add(validPrefabAssetId, new SpawnHandlerDelegate(x => null));
            NetworkClient.UnregisterPrefab(validPrefab);
            Assert.IsFalse(NetworkClient.spawnHandlers.ContainsKey(validPrefabAssetId));
        }

        [Test]
        public void RemovesUnSpawnHandlerFromDictionary()
        {
            NetworkClient.unspawnHandlers.Add(validPrefabAssetId, new UnSpawnDelegate(x => {}));
            NetworkClient.UnregisterPrefab(validPrefab);
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(validPrefabAssetId));
        }

        [Test]
        public void ErrorWhenPrefabIsNull()
        {
            LogAssert.Expect(LogType.Error, "Could not unregister prefab because it was null");
            NetworkClient.UnregisterPrefab(null);
        }

        [Test]
        public void ErrorWhenPrefabHasNoNetworkIdentity()
        {
            LogAssert.Expect(LogType.Error, $"Could not unregister '{invalidPrefab.name}' since it contains no NetworkIdentity component");
            NetworkClient.UnregisterPrefab(invalidPrefab);
        }

    }
}
