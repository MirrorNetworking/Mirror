using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveVideoFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var videos = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.Video).ToArray();

            if (videos.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No video files were found, looking good!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following video files were found", null, videos);

            return result;
        }
    }
}
