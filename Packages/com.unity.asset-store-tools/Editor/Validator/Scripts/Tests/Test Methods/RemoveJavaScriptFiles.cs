using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveJavaScriptFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var javascriptObjects = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.JavaScript).ToArray();

            if (javascriptObjects.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No UnityScript / JS files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following assets are UnityScript / JS files", null, javascriptObjects);

            return result;
        }
    }
}
