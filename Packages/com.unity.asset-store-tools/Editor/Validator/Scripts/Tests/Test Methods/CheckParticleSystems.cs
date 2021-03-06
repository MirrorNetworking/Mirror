using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckParticleSystems : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var originalScenePath = SceneUtility.CurrentScenePath;

            var scenePaths = AssetUtility.GetAssetPathsFromAssets(config.ValidationPaths, AssetType.Scene);

            foreach (var path in scenePaths)
            {
                var badParticleSystems = new List<ParticleSystem>();

                var scene = SceneUtility.OpenScene(path);

                if (!scene.IsValid())
                {
                    Debug.LogWarning("Unable to get Scene in " + path);
                    continue;
                }

                var particleSystems = GameObject.FindObjectsOfType<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    if (PrefabUtility.IsPartOfAnyPrefab(ps.gameObject))
                        continue;
                    badParticleSystems.Add(ps);
                }

                if (badParticleSystems.Count == 0)
                    continue;

                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                var message = $"Particle Systems not belonging to any Prefab were found in {path}.\n\nClick this message to open the Scene and see the affected Particle Systems:";
                result.AddMessage(message, new MessageActionOpenAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)), badParticleSystems.ToArray());
            }

            SceneUtility.OpenScene(originalScenePath);

            if (result.Result == TestResult.ResultStatus.Undefined)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No Particle Systems without Prefabs were found!");
            }

            return result;
        }
    }
}
