using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class RemoveExecutableFiles : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var executables = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.Executable).ToArray();

            if (executables.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No executable files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.VariableSeverityIssue;
            result.AddMessage("The following executable files were found", null, executables);

            return result;
        }
    }
}
