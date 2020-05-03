using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    [TestFixture]
    public class ClientSceneTests
    {
        // use guid to find asset so that the path does not matter
        const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
        const string PrefabWithChildrenAssetGuid = "a78e009e3f2dee44e8859516974ede43";
        const string InvalidPrefabAssetGuid = "78f0a3f755d35324e959f3ecdd993fb0";
        // random guid, not used anywhere
        const string AnotherGuidString = "5794128cdfda04542985151f82990d05";

        GameObject validPrefab;
        NetworkIdentity validPrefabNetId;
        GameObject prefabWithChildren;
        GameObject invalidPrefab;
        Guid validPrefabGuid;
        Guid anotherGuid;

        Dictionary<Guid, GameObject> prefabs => ClientScene.prefabs;
        Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers => ClientScene.spawnHandlers;
        Dictionary<Guid, UnSpawnDelegate> unspawnHandlers => ClientScene.unspawnHandlers;

        static GameObject LoadPrefab(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            validPrefab = LoadPrefab(ValidPrefabAssetGuid);
            validPrefabNetId = validPrefab.GetComponent<NetworkIdentity>();
            prefabWithChildren = LoadPrefab(PrefabWithChildrenAssetGuid);
            invalidPrefab = LoadPrefab(InvalidPrefabAssetGuid);
            validPrefabGuid = new Guid(ValidPrefabAssetGuid);
            anotherGuid = new Guid(AnotherGuidString);
        }

        [TearDown]
        public void TearDown()
        {
            ClientScene.Shutdown();
            // reset asset id incase they are changed by tests
            validPrefabNetId.assetId = validPrefabGuid;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            validPrefab = null;
            prefabWithChildren = null;
            invalidPrefab = null;
        }


        [Test]
        public void GetPrefab_ReturnsFalseForEmptyGuid()
        {
            bool result = ClientScene.GetPrefab(new Guid(), out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsFalseForPrefabNotFound()
        {
            Guid guid = Guid.NewGuid();
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsFalseForPrefabIsNull()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, null);
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GetPrefab_ReturnsTrueWhenPrefabIsFound()
        {
            prefabs.Add(validPrefabGuid, validPrefab);
            bool result = ClientScene.GetPrefab(validPrefabGuid, out GameObject prefab);

            Assert.IsTrue(result);
            Assert.NotNull(prefab);
        }

        [Test]
        public void GetPrefab_HasOutPrefabWithCorrectGuid()
        {
            prefabs.Add(validPrefabGuid, validPrefab);
            ClientScene.GetPrefab(validPrefabGuid, out GameObject prefab);


            Assert.NotNull(prefab);

            NetworkIdentity networkID = prefab.GetComponent<NetworkIdentity>();
            Assert.AreEqual(networkID.assetId, validPrefabGuid);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab, false)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId, true)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate, false)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, true)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate, false)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, true)]
        public void CheckOverloadWithAssetId(RegisterPrefabOverload overload, bool expected)
        {
            // test to make sure OverloadWithAssetId correctly works with flags
            Assert.That(OverloadWithAssetId(overload), Is.EqualTo(expected));
        }

        /// <summary>
        /// Allows TestCases to call different overloads for RegisterPrefab.
        /// Without this we would need duplicate tests for each overload
        /// </summary>
        [Flags]
        public enum RegisterPrefabOverload
        {
            Prefab = 1,
            Prefab_NewAssetId = 2,
            Prefab_SpawnDelegate = 4,
            Prefab_SpawnDelegate_NewAssetId = 8,
            Prefab_SpawnHandlerDelegate = 16,
            Prefab_SpawnHandlerDelegate_NewAssetId = 32,

            WithAssetId = Prefab_NewAssetId | Prefab_SpawnDelegate_NewAssetId | Prefab_SpawnHandlerDelegate_NewAssetId
        }

        static bool OverloadWithAssetId(RegisterPrefabOverload overload)
        {
            return (overload & RegisterPrefabOverload.WithAssetId) != 0;
        }

        void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload)
        {
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            SpawnHandlerDelegate spawnHandlerDelegate = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab:
                    ClientScene.RegisterPrefab(prefab);
                    break;
                case RegisterPrefabOverload.Prefab_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, anotherGuid);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                    ClientScene.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, anotherGuid, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                    ClientScene.RegisterPrefab(prefab, spawnHandlerDelegate, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, anotherGuid, spawnHandlerDelegate, unspawnHandler);
                    break;
                default:
                    Debug.LogError("Overload not found");
                    break;
            }
        }

        void CallRegisterPrefab_Handler(GameObject prefab, SpawnDelegate spawn, UnSpawnDelegate unspawn, RegisterPrefabOverload overload)
        {
            if (overload == RegisterPrefabOverload.Prefab_SpawnDelegate)
            {
                ClientScene.RegisterPrefab(prefab, spawn, unspawn);
            }
            else if (overload == RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)
            {
                ClientScene.RegisterPrefab(prefab, anotherGuid, spawn, unspawn);
            }
            else
            {
                Debug.LogError("Overload did not have SpawnDelegate");
            }
        }

        void CallRegisterPrefab_Handler(GameObject prefab, SpawnHandlerDelegate spawn, UnSpawnDelegate unspawn, RegisterPrefabOverload overload)
        {
            if (overload == RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)
            {
                ClientScene.RegisterPrefab(prefab, spawn, unspawn);
            }
            else if (overload == RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)
            {
                ClientScene.RegisterPrefab(prefab, anotherGuid, spawn, unspawn);
            }
            else
            {
                Debug.LogError("Overload did not have SpawnHandlerDelegate");
            }
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [Ignore("Ignoring this test till we know how to fix it, see https://github.com/vis2k/Mirror/issues/1831")]
        public void RegisterPrefab_Prefab_AddsPrefabToDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = OverloadWithAssetId(overload) ? anotherGuid : validPrefabGuid;

            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);
        }

        [Test]
        [Ignore("Ignoring this test till we know how to fix it, see https://github.com/vis2k/Mirror/issues/1831")]
        public void RegisterPrefab_PrefabNewGuid_ChangePrefabsAssetId()
        {
            Guid guid = anotherGuid;
            ClientScene.RegisterPrefab(validPrefab, guid);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);

            NetworkIdentity netId = prefabs[guid].GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, guid);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void RegisterPrefab_Prefab_ErrorForNullPrefab(RegisterPrefabOverload overload)
        {
            LogAssert.Expect(LogType.Error, "Could not register prefab because it was null");
            CallRegisterPrefab(null, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void RegisterPrefab_Prefab_ErrorForPrefabWithoutNetworkIdentity(RegisterPrefabOverload overload)
        {
            LogAssert.Expect(LogType.Error, $"Could not register '{invalidPrefab.name}' since it contains no NetworkIdentity component");
            CallRegisterPrefab(invalidPrefab, overload);
        }

        static void CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity)
        {
            runtimeObject = new GameObject("Runtime GameObject");
            networkIdentity = runtimeObject.AddComponent<NetworkIdentity>();
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            networkIdentity.sceneId = 0;
        }

        [Test]
        public void RegisterPrefab_Prefab_ErrorForEmptyGuid()
        {
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            //test
            LogAssert.Expect(LogType.Error, $"Can not Register '{runtimeObject.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
            ClientScene.RegisterPrefab(runtimeObject);

            // teardown
            GameObject.DestroyImmediate(runtimeObject);
        }

        [Test]
        public void RegisterPrefab_PrefabNewGuid_AddsRuntimeObjectToDictionary()
        {
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            Guid guid = anotherGuid;
            ClientScene.RegisterPrefab(runtimeObject, guid);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], runtimeObject);

            Assert.AreEqual(networkIdentity.assetId, guid);

            // teardown
            GameObject.DestroyImmediate(runtimeObject);
        }

        [Test]
        public void RegisterPrefab_PrefabNewGuid_ErrorForEmptyGuid()
        {
            LogAssert.Expect(LogType.Error, $"Could not register '{validPrefab.name}' with new assetId because the new assetId was empty");
            ClientScene.RegisterPrefab(validPrefab, new Guid());
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void RegisterPrefab_Prefab_ErrorIfPrefabHadSceneId(RegisterPrefabOverload overload)
        {
            GameObject clone = GameObject.Instantiate(validPrefab);
            NetworkIdentity netId = clone.GetComponent<NetworkIdentity>();
            // Scene Id needs to not be zero for this test
            netId.sceneId = 20;

            LogAssert.Expect(LogType.Error, $"Can not Register '{clone.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
            CallRegisterPrefab(clone, overload);

            GameObject.DestroyImmediate(clone);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void RegisterPrefab_Prefab_WarningForNetworkIdentityInChildren(RegisterPrefabOverload overload)
        {
            LogAssert.Expect(LogType.Warning, $"Prefab '{prefabWithChildren.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            CallRegisterPrefab(prefabWithChildren, overload);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [Ignore("Ignoring this test till we know how to fix it, see https://github.com/vis2k/Mirror/issues/1831")]
        public void RegisterPrefab_Prefab_WarningForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = OverloadWithAssetId(overload) ? anotherGuid : validPrefabGuid;

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Warning, $"Replacing existing prefab with assetId '{guid}'. Old prefab '{validPrefab.name}', New prefab '{validPrefab.name}'");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [Ignore("Ignoring this test till we know how to fix it, see https://github.com/vis2k/Mirror/issues/1831")]
        public void RegisterPrefab_Prefab_WarningForAssetIdAlreadyExistingInHandlersDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = OverloadWithAssetId(overload) ? anotherGuid : validPrefabGuid;

            spawnHandlers.Add(guid, x => null);
            unspawnHandlers.Add(guid, x => { });

            LogAssert.Expect(LogType.Warning, $"Adding prefab '{validPrefab.name}' with assetId '{guid}' when spawnHandlers with same assetId already exists.");
            CallRegisterPrefab(validPrefab, overload);
        }


        [Test]
        public void UnregisterPrefab_RemovesPrefabFromDictionary()
        {
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(prefabs.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterPrefab_RemovesSpawnHandlerFromDictionary()
        {
            spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(spawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterPrefab_RemovesUnSpawnHandlerFromDictionary()
        {
            unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => { }));

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterPrefab_ErrorWhenPrefabIsNull()
        {
            LogAssert.Expect(LogType.Error, "Could not unregister prefab because it was null");
            ClientScene.UnregisterPrefab(null);
        }

        [Test]
        public void UnregisterPrefab_ErrorWhenPrefabHasNoNetworkIdentity()
        {
            LogAssert.Expect(LogType.Error, $"Could not unregister '{invalidPrefab.name}' since it contains no NetworkIdentity component");
            ClientScene.UnregisterPrefab(invalidPrefab);
        }


        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_AddsHandlerToSpawnHandlers()
        {
            int handlerCalled = 0;

            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((pos, rot) =>
            {
                handlerCalled++;
                return null;
            });
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));

            // check spawnHandler above is called
            SpawnHandlerDelegate handler = spawnHandlers[guid];
            handler.Invoke(default);
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_AddsHandlerToSpawnHandlersWithCorrectArguments()
        {
            int handlerCalled = 0;
            Vector3 somePosition = new Vector3(10, 20, 3);

            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((pos, assetId) =>
            {
                handlerCalled++;
                Assert.That(pos, Is.EqualTo(somePosition));
                Assert.That(assetId, Is.EqualTo(guid));
                return null;
            });
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));

            // check spawnHandler above is called
            SpawnHandlerDelegate handler = spawnHandlers[guid];
            handler.Invoke(new SpawnMessage { position = somePosition, assetId = guid });
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_AddsHandlerToUnSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));
            Assert.AreEqual(unspawnHandlers[guid], unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_ErrorWhenAssetIdIsEmpty()
        {
            Guid guid = new Guid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty Guid");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            SpawnDelegate spawnHandler2 = new SpawnDelegate((x, y) => new GameObject());
            UnSpawnDelegate unspawnHandler2 = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Warning, $"Replacing existing spawnHandlers for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler2, unspawnHandler2);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, validPrefab);

            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}'");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }


        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_AddsHandlerToSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));
            Assert.AreEqual(spawnHandlers[guid], spawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_AddsHandlerToUnSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));
            Assert.AreEqual(unspawnHandlers[guid], unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_ErrorWhenAssetIdIsEmpty()
        {
            Guid guid = new Guid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty Guid");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            SpawnHandlerDelegate spawnHandler2 = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler2 = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Warning, $"Replacing existing spawnHandlers for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler2, unspawnHandler2);
        }

        [Test]
        public void RegisterSpawnHandler_SpawnHandlerDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, validPrefab);

            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}'");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }


        [Test]
        public void UnregisterSpawnHandler_RemovesSpawnHandlersFromDictionary()
        {
            spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterSpawnHandler_RemovesUnSpawnHandlersFromDictionary()
        {
            unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => { }));

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void UnregisterSpawnHandler_DoesNotRemovePrefabDictionary()
        {
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            // Should not be removed
            Assert.IsTrue(prefabs.ContainsKey(validPrefabGuid));
        }


        [Test]
        public void ClearSpawners_RemovesAllPrefabsFromDictionary()
        {
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(prefabs);
        }

        [Test]
        public void ClearSpawners_RemovesAllSpawnHandlersFromDictionary()
        {
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(spawnHandlers);
        }

        [Test]
        public void ClearSpawners_RemovesAllUnspawnHandlersFromDictionary()
        {
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(unspawnHandlers);
        }

        [Test]
        public void ClearSpawners_ClearsAllDictionary()
        {
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);

            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);

            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(prefabs);
            Assert.IsEmpty(spawnHandlers);
            Assert.IsEmpty(unspawnHandlers);
        }
    }
}
