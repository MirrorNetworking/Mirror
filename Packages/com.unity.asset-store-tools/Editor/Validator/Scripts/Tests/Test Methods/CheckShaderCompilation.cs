using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if UNITY_2023_1_OR_NEWER
using UnityEngine.Rendering;
#endif

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckShaderCompilation : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var shaders = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.Shader);
            var badShaders = shaders.Where(ShaderHasError).ToArray();

            if (badShaders.Length > 0)
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following shader files have errors", null, badShaders);
            }
            else
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All found Shaders have no compilation errors!");
            }

            return result;
        }

        private bool ShaderHasError(Object obj)
        {
            switch (obj)
            {
                case Shader shader:
                    return ShaderUtil.ShaderHasError(shader);
                case ComputeShader shader:
                    return ShaderUtil.GetComputeShaderMessageCount(shader) > 0;
                case RayTracingShader shader:
                    return ShaderUtil.GetRayTracingShaderMessageCount(shader) > 0;
                default:
                    return false;
            }
        }
    }
}
