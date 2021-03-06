using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SceneAsset = UnityEditor.SceneAsset;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckMissingComponentsinScenes : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var originalScenePath = SceneUtility.CurrentScenePath;

            var scenePaths = AssetUtility.GetAssetPathsFromAssets(config.ValidationPaths, AssetType.Scene);
            foreach (var scenePath in scenePaths)
            {
                var missingComponentGOs = GetMissingComponentGOsInScene(scenePath);

                if (missingComponentGOs.Count == 0)
                    continue;

                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                var message = $"GameObjects with missing components or prefab references found in {scenePath}.\n\nClick this message to open the Scene and see the affected GameObjects:";
                result.AddMessage(message, new MessageActionOpenAsset(AssetUtility.AssetPathToObject<SceneAsset>(scenePath)), missingComponentGOs.ToArray());
            }

            SceneUtility.OpenScene(originalScenePath);

            if (result.Result == TestResult.ResultStatus.Undefined)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No missing components were found!");
            }

            return result;
        }

        private List<GameObject> GetMissingComponentGOsInScene(string path)
        {
            var missingComponentGOs = new List<GameObject>();

            var scene = SceneUtility.OpenScene(path);

            if (!scene.IsValid())
            {
                Debug.LogWarning("Unable to get Scene in " + path);
                return new List<GameObject>();
            }

            var rootObjects = scene.GetRootGameObjects();

            foreach (var obj in rootObjects)
            {
                missingComponentGOs.AddRange(GetMissingComponentGOs(obj));
            }

            return missingComponentGOs;
        }

        private List<GameObject> GetMissingComponentGOs(GameObject root)
        {
            var missingComponentGOs = new List<GameObject>();
            var rootComponents = root.GetComponents<Component>();

            if (UnityEditor.PrefabUtility.GetPrefabInstanceStatus(root) == UnityEditor.PrefabInstanceStatus.MissingAsset || rootComponents.Any(c => !c))
            {
                missingComponentGOs.Add(root);
            }

            foreach (Transform child in root.transform)
                missingComponentGOs.AddRange(GetMissingComponentGOs(child.gameObject));

            return missingComponentGOs;
        }
    }
}
