using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckModelOrientation : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var models = AssetUtility.GetObjectsFromAssets<GameObject>(config.ValidationPaths, AssetType.Model);
            var badModels = new List<GameObject>();

            foreach (var m in models)
            {
                var meshes = MeshUtility.GetCustomMeshesInObject(m);
                var assetImporter = AssetUtility.GetAssetImporter(m);

                if (!(assetImporter is UnityEditor.ModelImporter modelImporter))
                    continue;

                var clips = modelImporter.clipAnimations.Length;

                // Only check if the model has meshes and no clips
                if (!meshes.Any() || clips != 0)
                    continue;

                Transform[] transforms = m.GetComponentsInChildren<Transform>(true);

                foreach (var t in transforms)
                {
                    var hasMeshComponent = t.TryGetComponent<MeshFilter>(out _) || t.TryGetComponent<SkinnedMeshRenderer>(out _);

                    if (t.localRotation == Quaternion.identity || !hasMeshComponent)
                        continue;

                    badModels.Add(m);
                    break;
                }
            }

            if (badModels.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All found models are facing the right way!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following models have incorrect rotation", null, badModels.ToArray());

            return result;
        }
    }
}
