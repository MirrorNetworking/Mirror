using AssetStoreTools.Utility.Json;
using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckDemoScenes : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            TestResult result = new TestResult { Result = TestResult.ResultStatus.Undefined };

            var scenePaths = AssetUtility.GetAssetPathsFromAssets(config.ValidationPaths, AssetType.Scene).ToArray();

            if (scenePaths.Length == 0)
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("Could not find any Scenes in the selected folder.");
                result.AddMessage("Please make sure you have selected the correct main folder of your package " +
                                  "and it includes a Demo Scene.");

                // Hybrid packages may have hidden Samples~ folders that are not detected by the Asset Database

                foreach (var path in config.ValidationPaths)
                {
                    if (!File.Exists($"{path}/package.json"))
                        continue;

                    var packageJsonText = File.ReadAllText($"{path}/package.json");
                    var json = JSONParser.SimpleParse(packageJsonText);

                    if (!json.ContainsKey("samples") || !json["samples"].IsList() || json["samples"].AsList().Count == 0)
                        return result;

                    var hasScenes = false;
                    foreach (var sample in json["samples"].AsList())
                    {
                        var samplePath = sample["path"].AsString();
                        samplePath = $"{path}/{samplePath}";
                        if (!Directory.Exists(samplePath))
                            continue;

                        var files = Directory.GetFiles(samplePath, "*.unity", SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            hasScenes = true;
                            break;
                        }
                    }

                    if (hasScenes)
                        return new TestResult { Result = TestResult.ResultStatus.Pass };
                }

                return result;
            }

            var originalScenePath = SceneUtility.CurrentScenePath;
            var demoScenePaths = scenePaths.Where(CanBeDemoScene).ToArray();
            SceneUtility.OpenScene(originalScenePath);

            if (demoScenePaths.Length == 0)
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("Could not find any Demo Scenes in the selected folder.");
            }
            else
            {
                result.Result = TestResult.ResultStatus.Pass;
                var demoSceneObjects = demoScenePaths.Select(AssetUtility.AssetPathToObject).ToArray();
                result.AddMessage("Scenes found", null, demoSceneObjects);
                result.AddMessage("If these Scenes should not belong to your package, " +
                                  "make sure you have selected the correct main folder.");
            }

            return result;
        }

        private bool CanBeDemoScene(string scenePath)
        {
            SceneUtility.OpenScene(scenePath);
            var rootObjects = SceneUtility.GetRootGameObjects();
            var count = rootObjects.Length;

            if (count == 0)
                return false;

            if (count != 2)
                return true;

            var cameraGOUnchanged = rootObjects.Any(o => o.TryGetComponent<Camera>(out _) && o.GetComponents(typeof(Component)).Length == 3);
            var lightGOUnchanged = rootObjects.Any(o => o.TryGetComponent<Light>(out _) && o.GetComponents(typeof(Component)).Length == 2);

            return !cameraGOUnchanged || !lightGOUnchanged;
        }
    }
}
