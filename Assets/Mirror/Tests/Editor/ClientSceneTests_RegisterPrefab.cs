using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_RegisterPrefab : ClientSceneTestsBase
    {
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

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab, false)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId, false)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate, true)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, true)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate, true)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, true)]
        public void CheckOverloadWithHandler(RegisterPrefabOverload overload, bool expected)
        {
            // test to make sure OverloadWithHandler correctly works with flags
            Assert.That(OverloadWithHandler(overload), Is.EqualTo(expected));
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

            WithAssetId = Prefab_NewAssetId | Prefab_SpawnDelegate_NewAssetId | Prefab_SpawnHandlerDelegate_NewAssetId,
            WithHandler = Prefab_SpawnDelegate | Prefab_SpawnDelegate_NewAssetId | Prefab_SpawnHandlerDelegate | Prefab_SpawnHandlerDelegate_NewAssetId
        }

        static bool OverloadWithAssetId(RegisterPrefabOverload overload)
        {
            return (overload & RegisterPrefabOverload.WithAssetId) != 0;
        }

        static bool OverloadWithHandler(RegisterPrefabOverload overload)
        {
            return (overload & RegisterPrefabOverload.WithHandler) != 0;
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

        void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, Guid guid)
        {
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            SpawnHandlerDelegate spawnHandlerDelegate = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, guid);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, guid, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, guid, spawnHandlerDelegate, unspawnHandler);
                    break;

                case RegisterPrefabOverload.Prefab:
                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                    Debug.LogError("Overload did not have guid parameter");
                    break;
                default:
                    Debug.LogError("Overload not found");
                    break;
            }
        }

        void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, SpawnDelegate spawnHandler)
        {
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                    ClientScene.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, anotherGuid, spawnHandler, unspawnHandler);
                    break;

                case RegisterPrefabOverload.Prefab:
                case RegisterPrefabOverload.Prefab_NewAssetId:
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    Debug.LogError("Overload did not have SpawnDelegate parameter");
                    break;
                default:
                    Debug.LogError("Overload not found");
                    break;
            }
        }

        void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, SpawnHandlerDelegate spawnHandlerDelegate)
        {
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                    ClientScene.RegisterPrefab(prefab, spawnHandlerDelegate, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    ClientScene.RegisterPrefab(prefab, anotherGuid, spawnHandlerDelegate, unspawnHandler);
                    break;

                case RegisterPrefabOverload.Prefab:
                case RegisterPrefabOverload.Prefab_NewAssetId:
                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    Debug.LogError("Overload did not have SpawnHandlerDelegate parameter");
                    break;
                default:
                    Debug.LogError("Overload not found");
                    break;
            }
        }

        void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, UnSpawnDelegate unspawnHandler)
        {
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            SpawnHandlerDelegate spawnHandlerDelegate = new SpawnHandlerDelegate(x => null);

            switch (overload)
            {

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

                case RegisterPrefabOverload.Prefab:
                case RegisterPrefabOverload.Prefab_NewAssetId:
                    Debug.LogError("Overload did not have UnSpawnDelegate parameter");
                    break;
                default:
                    Debug.LogError("Overload not found");
                    break;
            }
        }

        Guid GuidForOverload(RegisterPrefabOverload overload) => OverloadWithAssetId(overload) ? anotherGuid : validPrefabGuid;

        static void CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity)
        {
            runtimeObject = new GameObject("Runtime GameObject");
            networkIdentity = runtimeObject.AddComponent<NetworkIdentity>();
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            networkIdentity.sceneId = 0;
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void Prefab_AddsPrefabToDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_ChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            Guid guid = anotherGuid;
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);

            NetworkIdentity netId = prefabs[guid].GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, guid);
        }

        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void HandlerNewGuid_ChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            Guid guid = anotherGuid;
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));
            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, guid);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void ErrorForNullPrefab(RegisterPrefabOverload overload)
        {
            string msg = OverloadWithHandler(overload)
                ? "Could not register handler for prefab because the prefab was null"
                : "Could not register prefab because it was null";

            LogAssert.Expect(LogType.Error, msg);
            CallRegisterPrefab(null, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void ErrorForPrefabWithoutNetworkIdentity(RegisterPrefabOverload overload)
        {
            string msg = OverloadWithHandler(overload)
                ? $"Could not register handler for '{invalidPrefab.name}' since it contains no NetworkIdentity component"
                : $"Could not register '{invalidPrefab.name}' since it contains no NetworkIdentity component";

            LogAssert.Expect(LogType.Error, msg);
            CallRegisterPrefab(invalidPrefab, overload);
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
        public void Handler_AddsSpawnHandlerToDictionaryForRuntimeObject(RegisterPrefabOverload overload)
        {
            // setup
            CreateSceneObject(out GameObject runtimeObject, out NetworkIdentity networkIdentity);

            //test
            CallRegisterPrefab(runtimeObject, overload);

            Assert.IsTrue(spawnHandlers.ContainsKey(anotherGuid));

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

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void NewGuid_ErrorForEmptyGuid(RegisterPrefabOverload overload)
        {
            string msg = OverloadWithHandler(overload)
                ? $"Could not register handler for '{validPrefab.name}' with new assetId because the new assetId was empty"
                : $"Could not register '{validPrefab.name}' with new assetId because the new assetId was empty";
            LogAssert.Expect(LogType.Error, msg);
            CallRegisterPrefab(validPrefab, overload, guid: new Guid());
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void ErrorIfPrefabHadSceneId(RegisterPrefabOverload overload)
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void WarningForNetworkIdentityInChildren(RegisterPrefabOverload overload)
        {
            LogAssert.Expect(LogType.Warning, $"Prefab '{prefabWithChildren.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            CallRegisterPrefab(prefabWithChildren, overload);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void Prefab_WarningForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Warning, $"Replacing existing prefab with assetId '{guid}'. Old prefab '{validPrefab.name}', New prefab '{validPrefab.name}'");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void Handler_ErrorForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}', unregister the prefab first before trying to add handler");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void WarningForAssetIdAlreadyExistingInHandlersDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            spawnHandlers.Add(guid, x => null);
            unspawnHandlers.Add(guid, x => { });

            string msg = OverloadWithHandler(overload)
                ? $"Replacing existing spawnHandlers for prefab '{validPrefab.name}' with assetId '{guid}'"
                : $"Adding prefab '{validPrefab.name}' with assetId '{guid}' when spawnHandlers with same assetId already exists.";

            LogAssert.Expect(LogType.Warning, msg);
            CallRegisterPrefab(validPrefab, overload);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        public void SpawnDelegate_AddsHandlerToSpawnHandlers(RegisterPrefabOverload overload)
        {
            int handlerCalled = 0;

            Guid guid = GuidForOverload(overload);
            SpawnDelegate handler = new SpawnDelegate((pos, rot) =>
            {
                handlerCalled++;
                return null;
            });

            CallRegisterPrefab(validPrefab, overload, spawnHandler: handler);


            Assert.IsTrue(spawnHandlers.ContainsKey(guid));

            // check spawnHandler above is called
            SpawnHandlerDelegate handlerInDictionary = spawnHandlers[guid];
            handlerInDictionary.Invoke(default);
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        public void SpawnDelegate_AddsHandlerToSpawnHandlersWithCorrectArguments(RegisterPrefabOverload overload)
        {
            int handlerCalled = 0;
            Vector3 somePosition = new Vector3(10, 20, 3);

            Guid guid = GuidForOverload(overload);
            SpawnDelegate handler = new SpawnDelegate((pos, assetId) =>
            {
                handlerCalled++;
                Assert.That(pos, Is.EqualTo(somePosition));
                Assert.That(assetId, Is.EqualTo(guid));
                return null;
            });

            CallRegisterPrefab(validPrefab, overload, spawnHandler: handler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));

            // check spawnHandler above is called
            SpawnHandlerDelegate handlerInDictionary = spawnHandlers[guid];
            handlerInDictionary.Invoke(new SpawnMessage { position = somePosition, assetId = guid });
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        public void SpawnDelegate_ErrorWhenSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, spawnHandler: null);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void SpawnHandleDelegate_AddsHandlerToSpawnHandlers(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            SpawnHandlerDelegate handler = new SpawnHandlerDelegate(x => null);

            CallRegisterPrefab(validPrefab, overload, spawnHandlerDelegate: handler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));
            Assert.AreEqual(spawnHandlers[guid], handler);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void SpawnHandleDelegate_ErrorWhenSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, spawnHandlerDelegate: null);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void Handler_ErrorWhenUnSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, unspawnHandler: null);
        }

    }
}
