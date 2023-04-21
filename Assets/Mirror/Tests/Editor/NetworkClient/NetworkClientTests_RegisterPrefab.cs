using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_RegisterPrefab : ClientSceneTests_RegisterPrefabBase
    {
        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        public void Prefab_AddsPrefabToDictionary(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);

            CallRegisterPrefab(validPrefab, overload);

            Assert.IsTrue(NetworkClient.prefabs.ContainsKey(assetId));
            Assert.AreEqual(NetworkClient.prefabs[assetId], validPrefab);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_ErrorDoesNotChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            uint assetId = anotherAssetId;

            LogAssert.Expect(LogType.Error, $"Could not register '{validPrefab.name}' to {assetId} because it already had an AssetId, Existing assetId {validPrefabAssetId}");
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsFalse(NetworkClient.prefabs.ContainsKey(assetId));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabAssetId);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void HandlerNewGuid_ErrorDoesNotChangePrefabsAssetId(RegisterPrefabOverload overload)
        {
            uint assetId = anotherAssetId;

            LogAssert.Expect(LogType.Error, $"Could not register Handler for '{validPrefab.name}' to {assetId} because it already had an AssetId, Existing assetId {validPrefabAssetId}");
            CallRegisterPrefab(validPrefab, overload);

            Assert.IsFalse(NetworkClient.spawnHandlers.ContainsKey(assetId));
            Assert.IsFalse(NetworkClient.unspawnHandlers.ContainsKey(assetId));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabAssetId);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_NewAssetId)]
        public void PrefabNewGuid_NoErrorWhenNewAssetIdIsSameAsCurrentPrefab(RegisterPrefabOverload overload)
        {
            uint assetId = validPrefabAssetId;

            CallRegisterPrefab(validPrefab, overload, assetId);

            Assert.IsTrue(NetworkClient.prefabs.ContainsKey(assetId));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabAssetId);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId)]
        public void HandlerNewGuid_NoErrorWhenAssetIdIsSameAsCurrentPrefab(RegisterPrefabOverload overload)
        {
            uint assetId = validPrefabAssetId;

            CallRegisterPrefab(validPrefab, overload, assetId);

            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));
            Assert.IsTrue(NetworkClient.unspawnHandlers.ContainsKey(assetId));

            NetworkIdentity netId = validPrefab.GetComponent<NetworkIdentity>();

            Assert.AreEqual(netId.assetId, validPrefabAssetId);
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
            CallRegisterPrefab(validPrefab, overload, 0);
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
        public void ErrorForNetworkIdentityInChildren(RegisterPrefabOverload overload)
        {
            LogAssert.Expect(LogType.Error, $"Prefab '{prefabWithChildren.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            CallRegisterPrefab(prefabWithChildren, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        public void Prefab_WarningForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);

            NetworkClient.prefabs.Add(assetId, validPrefab);

            LogAssert.Expect(LogType.Warning, $"Replacing existing prefab with assetId '{assetId}'. Old prefab '{validPrefab.name}', New prefab '{validPrefab.name}'");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void Handler_ErrorForAssetIdAlreadyExistingInPrefabsDictionary(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);

            NetworkClient.prefabs.Add(assetId, validPrefab);

            LogAssert.Expect(LogType.Error, $"assetId '{assetId}' is already used by prefab '{validPrefab.name}', unregister the prefab first before trying to add handler");
            CallRegisterPrefab(validPrefab, overload);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void WarningForAssetIdAlreadyExistingInHandlersDictionary(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);

            NetworkClient.spawnHandlers.Add(assetId, x => null);
            NetworkClient.unspawnHandlers.Add(assetId, x => {});

            string msg = OverloadWithHandler(overload)
                ? $"Replacing existing spawnHandlers for prefab '{validPrefab.name}' with assetId '{assetId}'"
                : $"Adding prefab '{validPrefab.name}' with assetId '{assetId}' when spawnHandlers with same assetId already exists..*";

            LogAssert.Expect(LogType.Warning, new Regex(msg));
            CallRegisterPrefab(validPrefab, overload);
        }


        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        public void SpawnDelegate_AddsHandlerToSpawnHandlers(RegisterPrefabOverload overload)
        {
            int handlerCalled = 0;

            uint assetId = AssetIdForOverload(overload);
            SpawnDelegate handler = new SpawnDelegate((pos, rot) =>
            {
                handlerCalled++;
                return null;
            });

            CallRegisterPrefab(validPrefab, overload, handler);


            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));

            // check spawnHandler above is called
            SpawnHandlerDelegate handlerInDictionary = NetworkClient.spawnHandlers[assetId];
            handlerInDictionary.Invoke(default);
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        public void SpawnDelegate_AddsHandlerToSpawnHandlersWithCorrectArguments(RegisterPrefabOverload overload)
        {
            int handlerCalled = 0;
            Vector3 somePosition = new Vector3(10, 20, 3);

            uint assetId = AssetIdForOverload(overload);
            SpawnDelegate handler = new SpawnDelegate((pos, id) =>
            {
                handlerCalled++;
                Assert.That(pos, Is.EqualTo(somePosition));
                Assert.That(id, Is.EqualTo(assetId));
                return null;
            });

            CallRegisterPrefab(validPrefab, overload, handler);

            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));

            // check spawnHandler above is called
            SpawnHandlerDelegate handlerInDictionary = NetworkClient.spawnHandlers[assetId];
            handlerInDictionary.Invoke(new SpawnMessage { position = somePosition, assetId = assetId });
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId)]
        public void SpawnDelegate_ErrorWhenSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {assetId}");
            CallRegisterPrefab(validPrefab, overload, spawnHandler: null);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void SpawnHandleDelegate_AddsHandlerToSpawnHandlers(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);

            SpawnHandlerDelegate handler = new SpawnHandlerDelegate(x => null);

            CallRegisterPrefab(validPrefab, overload, handler);

            Assert.IsTrue(NetworkClient.spawnHandlers.ContainsKey(assetId));
            Assert.AreEqual(NetworkClient.spawnHandlers[assetId], handler);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void SpawnHandleDelegate_ErrorWhenSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null SpawnHandler for {assetId}");
            CallRegisterPrefab(validPrefab, overload, spawnHandlerDelegate: null);
        }

        [Test]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnDelegate)]
        [TestCase(RegisterPrefabOverload.Prefab_SpawnHandlerDelegate)]
        public void Handler_ErrorWhenUnSpawnHandlerIsNull(RegisterPrefabOverload overload)
        {
            uint assetId = AssetIdForOverload(overload);
            LogAssert.Expect(LogType.Error, $"Can not Register null UnSpawnHandler for {assetId}");
            CallRegisterPrefab(validPrefab, overload, unspawnHandler: null);
        }
    }
}
