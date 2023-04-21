using NUnit.Framework;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_UnregisterSpawnHandler : NetworkClientTestsBase
    {
        [Test]
        public void RemovesSpawnHandlersFromDictionary()
        {
            NetworkClient.spawnHandlers.Add(validPrefabAssetId, new SpawnHandlerDelegate(x => null));
            NetworkClient.UnregisterSpawnHandler(validPrefabAssetId);
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(validPrefabAssetId));
        }

        [Test]
        public void RemovesUnSpawnHandlersFromDictionary()
        {
            NetworkClient.unspawnHandlers.Add(validPrefabAssetId, new UnSpawnDelegate(x => {}));
            NetworkClient.UnregisterSpawnHandler(validPrefabAssetId);
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(validPrefabAssetId));
        }

        [Test]
        public void DoesNotRemovePrefabDictionary()
        {
            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);
            NetworkClient.UnregisterSpawnHandler(validPrefabAssetId);
            // Should not be removed
            Assert.IsTrue(NetworkClient.prefabs.ContainsKey(validPrefabAssetId));
        }

    }
}
