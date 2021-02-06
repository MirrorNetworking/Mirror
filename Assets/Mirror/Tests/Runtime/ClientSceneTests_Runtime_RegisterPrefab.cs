using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class ClientSceneTests_Runtime_RegisterPrefab : ClientSceneTests_RegisterPrefabBase
    {
        /// <summary>
        /// Create scene objects, must be done at runtime so that sceneId isn't set when assetId.get is called
        /// </summary>
        /// <param name="runtimeObject"></param>
        /// <param name="networkIdentity"></param>
        protected static void CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity)
        {
            runtimeObject = new GameObject("Runtime GameObject");
            networkIdentity = runtimeObject.AddComponent<NetworkIdentity>();
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            networkIdentity.sceneId = 0;
        }

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

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void ErrorForEmptyGuid(RegisterPrefabOverload overload)
        {
            // setup
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            //test
            string msg = OverloadWithHandler(overload)
               ? $"Can not Register handler for '{runtimeObject.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead"
               : $"Can not Register '{runtimeObject.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead";

            LogAssert.Expect(LogType.Error, msg);
            CallRegisterPrefab(runtimeObject, overload);

            // teardown
            GameObject.DestroyImmediate(runtimeObject);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_AddsRuntimeObjectToDictionary(RegisterPrefabOverload overload)
        {
            // setup
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            //test
            CallRegisterPrefab(runtimeObject, overload);

            Assert.IsTrue(prefabs.ContainsKey(anotherGuid));
            Assert.AreEqual(prefabs[anotherGuid], runtimeObject);

            Assert.AreEqual(networkIdentity.assetId, anotherGuid);

            // teardown
            GameObject.DestroyImmediate(runtimeObject);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void Handler_AddsUnSpawnHandlerToDictionaryForRuntimeObject(RegisterPrefabOverload overload)
        {
            // setup
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            //test
            CallRegisterPrefab(runtimeObject, overload);

            Assert.IsTrue(unspawnHandlers.ContainsKey(anotherGuid));

            // teardown
            GameObject.DestroyImmediate(runtimeObject);
        }
    }
}
