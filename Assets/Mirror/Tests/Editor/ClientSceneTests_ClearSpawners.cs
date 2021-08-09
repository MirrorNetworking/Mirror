using System;
using NUnit.Framework;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_ClearSpawners : ClientSceneTestsBase
    {
        [Test]
        public void RemovesAllPrefabsFromDictionary()
        {
            NetworkClient.prefabs.Add(Guid.NewGuid(), null);
            NetworkClient.prefabs.Add(Guid.NewGuid(), null);

            NetworkClient.ClearSpawners();
            Assert.IsEmpty(NetworkClient.prefabs);
        }

        [Test]
        public void RemovesAllSpawnHandlersFromDictionary()
        {
            NetworkClient.spawnHandlers.Add(Guid.NewGuid(), null);
            NetworkClient.spawnHandlers.Add(Guid.NewGuid(), null);

            NetworkClient.ClearSpawners();
            Assert.IsEmpty(NetworkClient.spawnHandlers);
        }

        [Test]
        public void RemovesAllUnspawnHandlersFromDictionary()
        {
            NetworkClient.unspawnHandlers.Add(Guid.NewGuid(), null);
            NetworkClient.unspawnHandlers.Add(Guid.NewGuid(), null);

            NetworkClient.ClearSpawners();

            Assert.IsEmpty(NetworkClient.unspawnHandlers);
        }

        [Test]
        public void ClearsAllDictionary()
        {
            NetworkClient.prefabs.Add(Guid.NewGuid(), null);
            NetworkClient.prefabs.Add(Guid.NewGuid(), null);

            NetworkClient.spawnHandlers.Add(Guid.NewGuid(), null);
            NetworkClient.spawnHandlers.Add(Guid.NewGuid(), null);

            NetworkClient.unspawnHandlers.Add(Guid.NewGuid(), null);
            NetworkClient.unspawnHandlers.Add(Guid.NewGuid(), null);

            NetworkClient.ClearSpawners();

            Assert.IsEmpty(NetworkClient.prefabs);
            Assert.IsEmpty(NetworkClient.spawnHandlers);
            Assert.IsEmpty(NetworkClient.unspawnHandlers);
        }
    }
}
