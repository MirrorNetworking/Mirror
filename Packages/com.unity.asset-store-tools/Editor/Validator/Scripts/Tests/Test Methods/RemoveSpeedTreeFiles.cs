using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveSpeedTreeFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var speedtreeObjects = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.SpeedTree).ToArray();

            if (speedtreeObjects.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No SpeedTree assets have been found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following SpeedTree assets have been found", null, speedtreeObjects);

            return result;
        }
    }
}
