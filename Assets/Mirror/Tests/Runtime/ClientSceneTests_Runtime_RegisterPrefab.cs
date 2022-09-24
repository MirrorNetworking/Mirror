using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class ClientSceneTests_Runtime_RegisterPrefab : ClientSceneTests_RegisterPrefabBase
    {
        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void Handler_AddsSpawnHandlerToDictionaryForRuntimeObject(RegisterPrefabOverload overload)
        {
            // create a scene object
            CreateNetworked(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            Debug.Assert(networkIdentity.sceneId == 0, "SceneId was not set to 0");
            Debug.Assert(runtimeObject.GetComponent<NetworkIdentity>().sceneId == 0, "SceneId was not set to 0");

            //test
            CallRegisterPrefab(runtimeObject, overload);
            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(anotherAssetId));
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void ErrorForEmptyGuid(RegisterPrefabOverload overload)
        {
            // create a scene object
            CreateNetworked(out GameObject runtimeObject, out _);

            //test
            string msg = OverloadWithHandler(overload)
               ? $"Can not Register handler for '{runtimeObject.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead"
               : $"Can not Register '{runtimeObject.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead";

            LogAssert.Expect(LogType.Error, msg);
            CallRegisterPrefab(runtimeObject, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_AddsRuntimeObjectToDictionary(RegisterPrefabOverload overload)
        {
            // create a scene object
            CreateNetworked(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            //test
            CallRegisterPrefab(runtimeObject, overload);

            Assert.IsTrue(NetworkClient.prefabs.ContainsKey(anotherAssetId));
            Assert.AreEqual(NetworkClient.prefabs[anotherAssetId], runtimeObject);

            Assert.AreEqual(networkIdentity.assetId, anotherAssetId);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void Handler_AddsUnSpawnHandlerToDictionaryForRuntimeObject(RegisterPrefabOverload overload)
        {
            // create a scene object
            CreateNetworked(out GameObject runtimeObject, out _);

            //test
            CallRegisterPrefab(runtimeObject, overload);
            Assert.IsTrue(NetworkClient.unspawnHandlers.ContainsKey(anotherAssetId));
        }
    }
}
