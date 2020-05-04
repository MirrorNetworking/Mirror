using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    [TestFixture]
    public abstract class BaseClientSceneTests
    {
        protected const string NewAssetIdIgnoreMessage = "Ignoring this test till we know how to fix it, see https://github.com/vis2k/Mirror/issues/1831";

        // use guid to find asset so that the path does not matter
        protected const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
        protected const string PrefabWithChildrenAssetGuid = "a78e009e3f2dee44e8859516974ede43";
        protected const string InvalidPrefabAssetGuid = "78f0a3f755d35324e959f3ecdd993fb0";
        // random guid, not used anywhere
        protected const string AnotherGuidString = "5794128cdfda04542985151f82990d05";

        protected GameObject validPrefab;
        protected NetworkIdentity validPrefabNetId;
        protected GameObject prefabWithChildren;
        protected GameObject invalidPrefab;
        protected Guid validPrefabGuid;
        protected Guid anotherGuid;

        protected Dictionary<Guid, GameObject> prefabs => ClientScene.prefabs;
        protected Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers => ClientScene.spawnHandlers;
        protected Dictionary<Guid, UnSpawnDelegate> unspawnHandlers => ClientScene.unspawnHandlers;

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
    }
    public class ClientSceneTests_GetPrefab : BaseClientSceneTests
    {
        [Test]
        public void ReturnsFalseForEmptyGuid()
        {
            bool result = ClientScene.GetPrefab(new Guid(), out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsFalseForPrefabNotFound()
        {
            Guid guid = Guid.NewGuid();
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsFalseForPrefabIsNull()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, null);
            bool result = ClientScene.GetPrefab(guid, out GameObject prefab);

            Assert.IsFalse(result);
            Assert.IsNull(prefab);
        }

        [Test]
        public void ReturnsTrueWhenPrefabIsFound()
        {
            prefabs.Add(validPrefabGuid, validPrefab);
            bool result = ClientScene.GetPrefab(validPrefabGuid, out GameObject prefab);

            Assert.IsTrue(result);
            Assert.NotNull(prefab);
        }

        [Test]
        public void HasOutPrefabWithCorrectGuid()
        {
            prefabs.Add(validPrefabGuid, validPrefab);
            ClientScene.GetPrefab(validPrefabGuid, out GameObject prefab);


            Assert.NotNull(prefab);

            NetworkIdentity networkID = prefab.GetComponent<NetworkIdentity>();
            Assert.AreEqual(networkID.assetId, validPrefabGuid);
        }
    }
    public class ClientSceneTests_RegisterPrefab : BaseClientSceneTests
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
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        public void Prefab_AddsPrefabToDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        public void NewGuid_ChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            Guid guid = anotherGuid;
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);

            NetworkIdentity netId = prefabs[guid].GetComponent<NetworkIdentity>();

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
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        public void Prefab_WarningForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Warning, $"Replacing existing prefab with assetId '{guid}'. Old prefab '{validPrefab.name}', New prefab '{validPrefab.name}'");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        public void Handler_ErrorForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}', unregister the prefab first before trying to add handler");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        public void SpawnHandleDelegate_ErrorWhenSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, spawnHandlerDelegate: null);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId, IgnoreReason = NewAssetIdIgnoreMessage)]
        public void Handler_ErrorWhenUnSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, unspawnHandler: null);
        }

    }
    public class ClientSceneTests_UnregisterPrefab : BaseClientSceneTests
    {
        [Test]
        public void RemovesPrefabFromDictionary()
        {
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(prefabs.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void RemovesSpawnHandlerFromDictionary()
        {
            spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(spawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void RemovesUnSpawnHandlerFromDictionary()
        {
            unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => { }));

            ClientScene.UnregisterPrefab(validPrefab);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
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
    public class ClientSceneTests_RegisterSpawnHandler : BaseClientSceneTests
    {
        [Test]
        public void SpawnDelegate_AddsHandlerToSpawnHandlers()
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
        public void SpawnDelegate_AddsHandlerToSpawnHandlersWithCorrectArguments()
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
        public void SpawnDelegate_AddsHandlerToUnSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));
            Assert.AreEqual(unspawnHandlers[guid], unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_ErrorWhenAssetIdIsEmpty()
        {
            Guid guid = new Guid();
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty Guid");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
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
        public void SpawnDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, validPrefab);

            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}'");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }


        [Test]
        public void SpawnHandlerDelegate_AddsHandlerToSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));
            Assert.AreEqual(spawnHandlers[guid], spawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_AddsHandlerToUnSpawnHandlers()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);

            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));
            Assert.AreEqual(unspawnHandlers[guid], unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = null;
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenUnSpawnHandlerIsNull()
        {
            Guid guid = Guid.NewGuid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = null;

            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_ErrorWhenAssetIdIsEmpty()
        {
            Guid guid = new Guid();
            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => { });

            LogAssert.Expect(LogType.Error, "Can not Register SpawnHandler for empty Guid");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

        [Test]
        public void SpawnHandlerDelegate_WarningWhenHandlerForGuidAlreadyExistsInHandlerDictionary()
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
        public void SpawnHandlerDelegate_ErrorWhenHandlerForGuidAlreadyExistsInPrefabDictionary()
        {
            Guid guid = Guid.NewGuid();
            prefabs.Add(guid, validPrefab);

            SpawnHandlerDelegate spawnHandler = new SpawnHandlerDelegate(x => new GameObject());
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => UnityEngine.Object.Destroy(x));

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}'");
            ClientScene.RegisterSpawnHandler(guid, spawnHandler, unspawnHandler);
        }

    }
    public class ClientSceneTests_UnregisterSpawnHandler : BaseClientSceneTests
    {
        [Test]
        public void RemovesSpawnHandlersFromDictionary()
        {
            spawnHandlers.Add(validPrefabGuid, new SpawnHandlerDelegate(x => null));

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void RemovesUnSpawnHandlersFromDictionary()
        {
            unspawnHandlers.Add(validPrefabGuid, new UnSpawnDelegate(x => { }));

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            Assert.IsFalse(unspawnHandlers.ContainsKey(validPrefabGuid));
        }

        [Test]
        public void DoesNotRemovePrefabDictionary()
        {
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.UnregisterSpawnHandler(validPrefabGuid);

            // Should not be removed
            Assert.IsTrue(prefabs.ContainsKey(validPrefabGuid));
        }

    }
    public class ClientSceneTests_ClearSpawners : BaseClientSceneTests
    {
        [Test]
        public void RemovesAllPrefabsFromDictionary()
        {
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);
            prefabs.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(prefabs);
        }

        [Test]
        public void RemovesAllSpawnHandlersFromDictionary()
        {
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);
            spawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(spawnHandlers);
        }

        [Test]
        public void RemovesAllUnspawnHandlersFromDictionary()
        {
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);
            unspawnHandlers.Add(Guid.NewGuid(), null);

            ClientScene.ClearSpawners();

            Assert.IsEmpty(unspawnHandlers);
        }

        [Test]
        public void ClearsAllDictionary()
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
