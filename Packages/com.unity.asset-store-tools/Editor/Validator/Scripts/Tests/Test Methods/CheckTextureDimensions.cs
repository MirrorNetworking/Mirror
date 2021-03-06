using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckTextureDimensions : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var textures = AssetUtility.GetObjectsFromAssets<Texture>(config.ValidationPaths, AssetType.Texture);
            var badTextures = new List<Texture>();

            foreach (var texture in textures)
            {
                if (Mathf.IsPowerOfTwo(texture.width) && Mathf.IsPowerOfTwo(texture.height))
                    continue;

                badTextures.Add(texture);
            }

            if (badTextures.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All texture dimensions are a power of 2!");
            }
            else
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following texture dimensions are not a power of 2:", null, badTextures.ToArray());
            }

            return result;
        }
    }
}
