using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckColliders : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };
            
            var prefabs = AssetUtility.GetObjectsFromAssets<GameObject>(config.ValidationPaths, AssetType.Prefab);
            var badPrefabs = new List<GameObject>();

            foreach (var p in prefabs)
            {
                var meshes = MeshUtility.GetCustomMeshesInObject(p);

                if (!p.isStatic || !meshes.Any())
                    continue;

                var colliders = p.GetComponentsInChildren<Collider>(true);
                if (!colliders.Any())
                    badPrefabs.Add(p);
            }

            if (badPrefabs.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All found prefabs have colliders!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following prefabs contain meshes, but colliders were not found", null, badPrefabs.ToArray());

            return result;
        }
    }
}
