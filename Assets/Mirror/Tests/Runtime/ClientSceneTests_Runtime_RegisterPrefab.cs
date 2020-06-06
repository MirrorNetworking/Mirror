using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class ClientSceneTests_Runtime_RegisterPrefab : ClientSceneTests_RegisterPrefabBase
    {
        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void Handler_AddsSpawnHandlerToDictionaryForRuntimeObject(RegisterPrefabOverload overload)
        {
            // setup
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            Debug.Assert(networkIdentity.sceneId == 0, "SceneId was not set to 0");
            Debug.Assert(runtimeObject.GetComponent<NetworkIdentity>().sceneId == 0, "SceneId was not set to 0");

            //test
            CallRegisterPrefab(runtimeObject, overload);

            Assert.IsTrue(spawnHandlers.ContainsKey(anotherGuid));

            // teardown
            GameObject.DestroyImmediate(runtimeObject);
        }
    }

}
