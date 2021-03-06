using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckModelTypes : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var allowedExtensions = new string[] { ".fbx", ".dae", ".abc", ".obj" };
            // Should retrieve All assets and not models here since ADB will not recognize certain
            // types if appropriate software is not installed (e.g. .blend without Blender)
            var allAssetPaths = AssetUtility.GetAssetPathsFromAssets(config.ValidationPaths, AssetType.All);
            var badModels = new List<UnityObject>();

            foreach (var assetPath in allAssetPaths)
            {
                var importer = AssetUtility.GetAssetImporter(assetPath);
                if (importer == null || !(importer is ModelImporter))
                    continue;

                if (allowedExtensions.Any(x => importer.assetPath.ToLower().EndsWith(x)))
                    continue;

                badModels.Add(AssetUtility.AssetPathToObject(assetPath));
            }

            if (badModels.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All models are of allowed formats!");
            }
            else
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following models are of formats that should not be used for Asset Store packages:", null, badModels.ToArray());
            }

            return result;
        }
    }
}
