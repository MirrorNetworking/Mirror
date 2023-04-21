using NUnit.Framework;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_ClearSpawners : NetworkClientTestsBase
    {
        [Test]
        public void RemovesAllPrefabsFromDictionary()
        {
            NetworkClient.prefabs.Add(1, null);
            NetworkClient.prefabs.Add(2, null);

            NetworkClient.ClearSpawners();
            Assert.IsEmpty(NetworkClient.prefabs);
        }

        [Test]
        public void RemovesAllSpawnHandlersFromDictionary()
        {
            NetworkClient.spawnHandlers.Add(1, null);
            NetworkClient.spawnHandlers.Add(2, null);

            NetworkClient.ClearSpawners();
            Assert.IsEmpty(NetworkClient.spawnHandlers);
        }

        [Test]
        public void RemovesAllUnspawnHandlersFromDictionary()
        {
            NetworkClient.unspawnHandlers.Add(1, null);
            NetworkClient.unspawnHandlers.Add(2, null);

            NetworkClient.ClearSpawners();

            Assert.IsEmpty(NetworkClient.unspawnHandlers);
        }

        [Test]
        public void ClearsAllDictionary()
        {
            NetworkClient.prefabs.Add(1, null);
            NetworkClient.prefabs.Add(2, null);

            NetworkClient.spawnHandlers.Add(1, null);
            NetworkClient.spawnHandlers.Add(2, null);

            NetworkClient.unspawnHandlers.Add(1, null);
            NetworkClient.unspawnHandlers.Add(2, null);

            NetworkClient.ClearSpawners();

            Assert.IsEmpty(NetworkClient.prefabs);
            Assert.IsEmpty(NetworkClient.spawnHandlers);
            Assert.IsEmpty(NetworkClient.unspawnHandlers);
        }
    }
}
