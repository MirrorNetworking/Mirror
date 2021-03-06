using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveJPGFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var jpgs = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.JPG).ToArray();

            if (jpgs.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No JPG/JPEG textures were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following textures are compressed as JPG/JPEG", null, jpgs);

            return result;
        }
    }
}
