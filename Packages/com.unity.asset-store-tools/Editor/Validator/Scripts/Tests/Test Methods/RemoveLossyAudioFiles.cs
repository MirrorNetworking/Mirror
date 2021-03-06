using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveLossyAudioFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            string SanitizeForComparison(UnityObject o)
            {
                Regex alphanumericRegex = new Regex("[^a-zA-Z0-9]");
                string path = AssetUtility.ObjectToAssetPath(o);
                path = path.ToLower();

                int extensionIndex = path.LastIndexOf('.');
                string extension = path.Substring(extensionIndex + 1);
                string sanitized = path.Substring(0, extensionIndex);

                int separatorIndex = sanitized.LastIndexOf('/');
                sanitized = sanitized.Substring(separatorIndex);
                sanitized = alphanumericRegex.Replace(sanitized, String.Empty);
                sanitized = sanitized.Replace(extension, String.Empty);
                sanitized = sanitized.Trim();

                return sanitized;
            }

            var lossyAudioObjects = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.LossyAudio).ToArray();
            if (lossyAudioObjects.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No lossy audio files were found!");
                return result;
            }

            // Try to find and match variants
            var nonLossyAudioObjects = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.NonLossyAudio);
            HashSet<string> nonLossyPathSet = new HashSet<string>();
            foreach (var asset in nonLossyAudioObjects)
            {
                var path = SanitizeForComparison(asset);
                nonLossyPathSet.Add(path);
            }

            var unmatchedAssets = new List<UnityObject>();
            foreach (var asset in lossyAudioObjects)
            {
                var path = SanitizeForComparison(asset);
                if (!nonLossyPathSet.Contains(path))
                    unmatchedAssets.Add(asset);
            }

            if (unmatchedAssets.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No lossy audio files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following lossy audio files were found without identically named non-lossy variants:", null, unmatchedAssets.ToArray());
            return result;
        }
    }
}
