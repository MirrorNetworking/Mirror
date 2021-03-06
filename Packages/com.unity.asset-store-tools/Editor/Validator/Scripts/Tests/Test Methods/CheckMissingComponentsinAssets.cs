using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckMissingComponentsinAssets : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var assets = GetAllAssetsWithMissingComponents(config.ValidationPaths);

            if (assets.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No assets have missing components!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following assets contain missing components", null, assets);

            return result;
        }

        private GameObject[] GetAllAssetsWithMissingComponents(string[] validationPaths)
        {
            var missingReferenceAssets = new List<GameObject>();
            var prefabObjects = AssetUtility.GetObjectsFromAssets<GameObject>(validationPaths, AssetType.Prefab);

            foreach (var p in prefabObjects)
            {
                if (p != null && IsMissingReference(p))
                    missingReferenceAssets.Add(p);
            }

            return missingReferenceAssets.ToArray();
        }

        private bool IsMissingReference(GameObject asset)
        {
            var components = asset.GetComponentsInChildren<Component>();

            foreach (var c in components)
            {
                if (!c)
                    return true;
            }

            return false;
        }
    }
}
