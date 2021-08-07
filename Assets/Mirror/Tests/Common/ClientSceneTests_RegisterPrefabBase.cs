using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    /// <summary>
    /// Used by both runtime and edit time tests
    /// </summary>
    [TestFixture]
    public abstract class ClientSceneTests_RegisterPrefabBase : ClientSceneTestsBase
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

        protected static bool OverloadWithAssetId(RegisterPrefabOverload overload)
        {
            return (overload & RegisterPrefabOverload.WithAssetId) != 0;
        }

        protected static bool OverloadWithHandler(RegisterPrefabOverload overload)
        {
            return (overload & RegisterPrefabOverload.WithHandler) != 0;
        }

        protected void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload)
        {
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            SpawnHandlerDelegate spawnHandlerDelegate = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab:
                    NetworkClient.RegisterPrefab(prefab);
                    break;
                case RegisterPrefabOverload.Prefab_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                    NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                    NetworkClient.RegisterPrefab(prefab, spawnHandlerDelegate, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid, spawnHandlerDelegate, unspawnHandler);
                    break;

                default:
                    Debug.LogError("Overload not found");
                    break;
            }
        }

        protected void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, Guid guid)
        {
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            SpawnHandlerDelegate spawnHandlerDelegate = new SpawnHandlerDelegate(x => null);
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, guid);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, guid, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, guid, spawnHandlerDelegate, unspawnHandler);
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

        protected void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, SpawnDelegate spawnHandler)
        {
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                    NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid, spawnHandler, unspawnHandler);
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

        protected void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, SpawnHandlerDelegate spawnHandlerDelegate)
        {
            UnSpawnDelegate unspawnHandler = new UnSpawnDelegate(x => {});

            switch (overload)
            {
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                    NetworkClient.RegisterPrefab(prefab, spawnHandlerDelegate, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid, spawnHandlerDelegate, unspawnHandler);
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

        protected void CallRegisterPrefab(GameObject prefab, RegisterPrefabOverload overload, UnSpawnDelegate unspawnHandler)
        {
            SpawnDelegate spawnHandler = new SpawnDelegate((x, y) => null);
            SpawnHandlerDelegate spawnHandlerDelegate = new SpawnHandlerDelegate(x => null);

            switch (overload)
            {

                case RegisterPrefabOverload.Prefab_SpawnDelegate:
                    NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid, spawnHandler, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate:
                    NetworkClient.RegisterPrefab(prefab, spawnHandlerDelegate, unspawnHandler);
                    break;
                case RegisterPrefabOverload.Prefab_SpawnHandlerDelegate_NewAssetId:
                    NetworkClient.RegisterPrefab(prefab, anotherGuid, spawnHandlerDelegate, unspawnHandler);
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

        protected Guid GuidForOverload(RegisterPrefabOverload overload) => OverloadWithAssetId(overload) ? anotherGuid : validPrefabGuid;
    }
}
