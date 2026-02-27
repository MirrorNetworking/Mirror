using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_InvokeUnSpawnHandler : NetworkClientTestsBase
    {
        [Test]
        public void ReturnsTrueAndInvokesHandlerWhenRegistered()
        {
            bool called = false;
            NetworkClient.unspawnHandlers[validPrefabAssetId] = _ => called = true;
            CreateGameObject(out GameObject go);

            bool result = NetworkClient.InvokeUnSpawnHandler(validPrefabAssetId, go);

            Assert.That(result, Is.True);
            Assert.That(called, Is.True);
        }

        [Test]
        public void ReturnsFalseWhenNoHandlerRegistered()
        {
            CreateGameObject(out GameObject go);
            bool result = NetworkClient.InvokeUnSpawnHandler(validPrefabAssetId, go);
            Assert.That(result, Is.False);
        }

        [Test]
        public void ReturnsFalseWhenHandlerIsNull()
        {
            NetworkClient.unspawnHandlers[validPrefabAssetId] = null;
            CreateGameObject(out GameObject go);
            bool result = NetworkClient.InvokeUnSpawnHandler(validPrefabAssetId, go);
            Assert.That(result, Is.False);
        }
    }
}