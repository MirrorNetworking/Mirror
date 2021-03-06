using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckEmptyPrefabs : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var prefabs = AssetUtility.GetObjectsFromAssets<GameObject>(config.ValidationPaths, AssetType.Prefab);
            var badPrefabs = new List<GameObject>();

            foreach (var p in prefabs)
            {
                if (p.GetComponents<Component>().Length == 1 && p.transform.childCount == 0)
                    badPrefabs.Add(p);
            }

            if (badPrefabs.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No empty prefabs were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following prefabs are empty", null, badPrefabs.ToArray());

            return result;
        }
    }
}
