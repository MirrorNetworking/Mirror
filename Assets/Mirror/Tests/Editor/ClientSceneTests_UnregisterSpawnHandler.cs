using NUnit.Framework;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_UnregisterSpawnHandler : ClientSceneTestsBase
    {
        [Test]
        public void RemovesSpawnHandlersFromDictionary()
        {
            NetworkClient.spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));
            NetworkClient.UnregisterSpawnHandler(validPrefabGuid);
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void RemovesUnSpawnHandlersFromDictionary()
        {
            NetworkClient.unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => {}));
            NetworkClient.UnregisterSpawnHandler(validPrefabGuid);
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void DoesNotRemovePrefabDictionary()
        {
            NetworkClient.prefabs.Add(validPrefabGuid, validPrefab);
            NetworkClient.UnregisterSpawnHandler(validPrefabGuid);
            // Should not be removed
            Assert.IsTrue(NetworkClient.prefabs.ContainsKey(validPrefabGuid));
        }

    }
}
