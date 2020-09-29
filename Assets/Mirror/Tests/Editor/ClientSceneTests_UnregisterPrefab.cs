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
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(prefabs.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void ErrorWhenPrefabIsNull()
        {
            LogAssert.Expect(LogType.Error, "Could not unregister prefab because it was null");
            ClientScene.UnregisterPrefab(null);
        }

        [Test]
        public void ErrorWhenPrefabHasNoNetworkIdentity()
        {
            LogAssert.Expect(LogType.Error, $"Could not unregister '{invalidPrefab.name}' since it contains no NetworkIdentity component");
            ClientScene.UnregisterPrefab(invalidPrefab);
        }

    }
}
