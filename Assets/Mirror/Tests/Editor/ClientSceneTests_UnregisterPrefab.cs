using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_UnregisterPrefab : ClientSceneTestsBase
    {
        [Test]
        public void RemovesPrefabFromDictionary()
        {
            NetworkClient.prefabs.Add(validPrefabGuid, validPrefab);
            NetworkClient.UnregisterPrefab(validPrefab);
            Assert.IsFalse(NetworkClient.prefabs.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void RemovesSpawnHandlerFromDictionary()
        {
            NetworkClient.spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));
            NetworkClient.UnregisterPrefab(validPrefab);
            Assert.IsFalse(NetworkClient.spawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void RemovesUnSpawnHandlerFromDictionary()
        {
            NetworkClient.unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => {}));
            NetworkClient.UnregisterPrefab(validPrefab);
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(validPrefabGuid));
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
