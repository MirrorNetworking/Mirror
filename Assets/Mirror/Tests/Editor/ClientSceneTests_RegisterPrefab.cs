using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientSceneTests
{
    public class ClientSceneTests_RegisterPrefab : ClientSceneTests_RegisterPrefabBase
    {
        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        public void Prefab_AddsPrefabToDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(prefabs.ContainsKey(guid));
            Assert.AreEqual(prefabs[guid], validPrefab);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_ErrorDoesNotChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            Guid guid = anotherGuid;

            LogAssert.Expect(LogType.Error, $"Could not register '{validPrefab.name}' to {guid} because it already had an AssetId, Existing assetId {validPrefabGuid}");
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsFalse(prefabs.ContainsKey(guid));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabGuid);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void HandlerNewGuid_ErrorDoesNotChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            Guid guid = anotherGuid;

            LogAssert.Expect(LogType.Error, $"Could not register Handler for '{validPrefab.name}' to {guid} because it already had an AssetId, Existing assetId {validPrefabGuid}");
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsFalse(spawnHandlers.ContainsKey(guid));
            Assert.IsFalse(unspawnHandlers.ContainsKey(guid));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabGuid);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_NoErrorWhenNewAssetIdIsSameAsCurrentPrefab(RegisterPrefabOverload overload)
        {
            Guid guid = validPrefabGuid;

            CallRegisterPrefab(validPrefab, overload, guid);

            Assert.IsTrue(prefabs.ContainsKey(guid));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabGuid);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void HandlerNewGuid_NoErrorWhenAssetIdIsSameAsCurrentPrefab(RegisterPrefabOverload overload)
        {
            Guid guid = validPrefabGuid;

            CallRegisterPrefab(validPrefab, overload, guid);

            Assert.IsTrue(spawnHandlers.ContainsKey(guid));
            Assert.IsTrue(unspawnHandlers.ContainsKey(guid));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabGuid);
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
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
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void WarningForNetworkIdentityInChildren(RegisterPrefabOverload overload)
        {
            LogAssert.Expect(LogType.Warning, $"Prefab '{prefabWithChildren.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            CallRegisterPrefab(prefabWithChildren, overload);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        public void Prefab_WarningForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Warning, $"Replacing existing prefab with assetId '{guid}'. Old prefab '{validPrefab.name}', New prefab '{validPrefab.name}'");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void Handler_ErrorForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            prefabs.Add(guid, validPrefab);

            LogAssert.Expect(LogType.Error, $"assetId '{guid}' is already used by prefab '{validPrefab.name}', unregister the prefab first before trying to add handler");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void WarningForAssetIdAlreadyExistingInHandlersDictionary(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);

            spawnHandlers.Add(guid, x => null);
            unspawnHandlers.Add(guid, x => {});

            string msg = OverloadWithHandler(overload)
                ? $"Replacing existing spawnHandlers for prefab '{validPrefab.name}' with assetId '{guid}'"
                : $"Adding prefab '{validPrefab.name}' with assetId '{guid}' when spawnHandlers with same assetId already exists.";

            LogAssert.Expect(LogType.Warning, msg);
            CallRegisterPrefab(validPrefab, overload);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
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
        public void SpawnHandleDelegate_ErrorWhenSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, spawnHandlerDelegate: null);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void Handler_ErrorWhenUnSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            Guid guid = GuidForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {guid}");
            CallRegisterPrefab(validPrefab, overload, unspawnHandler: null);
        }
    }
}
