using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveMixamoFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var mixamoFiles = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.Mixamo).ToArray();

            if (mixamoFiles.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No Mixamo files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following Mixamo files were found", null, mixamoFiles);

            return result;
        }
    }
}
