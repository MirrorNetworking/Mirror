using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{

    /// <summary>
    /// Used by both runtime and edit time tests
    /// </summary>
    [TestFixture]
    public abstract class NetworkClientTestsBase : MirrorEditModeTest
    {
        // use guid to find asset so that the path does not matter
        protected const string ValidPrefabAssetGuid = "33169286da0313d45ab5bfccc6cf3775";
        protected const string PrefabWithChildrenAssetGuid = "a78e009e3f2dee44e8859516974ede43";
        protected const string InvalidPrefabAssetGuid = "78f0a3f755d35324e959f3ecdd993fb0";
        // random guid, not used anywhere
        protected const string AnotherGuidString = "5794128cdfda04542985151f82990d05";

        protected GameObject validPrefab;
        protected NetworkIdentity validPrefabNetworkIdentity;
        protected GameObject prefabWithChildren;
        protected GameObject invalidPrefab;
        protected uint validPrefabAssetId;
        protected uint anotherAssetId;

        static GameObject LoadPrefab(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            validPrefab = LoadPrefab(ValidPrefabAssetGuid);
            validPrefabNetworkIdentity = validPrefab.GetComponent<NetworkIdentity>();
            
            // loading the prefab with multiple children *may* trigger OnValidate which will cause an error log
            // but also may not, so we can't use LogAssert.Expect here
            try
            {
                LogAssert.ignoreFailingMessages = true;
                prefabWithChildren = LoadPrefab(PrefabWithChildrenAssetGuid);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
            
            invalidPrefab = LoadPrefab(InvalidPrefabAssetGuid);
            validPrefabAssetId = (uint)(new Guid(ValidPrefabAssetGuid).GetHashCode());
            anotherAssetId = (uint)(new Guid(AnotherGuidString).GetHashCode());
        }

        [TearDown]
        public override void TearDown()
        {
            // reset asset id in case they are changed by tests
            validPrefabNetworkIdentity.assetId = validPrefabAssetId;

            base.TearDown();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            validPrefab = null;
            prefabWithChildren = null;
            invalidPrefab = null;
        }
    }
}
