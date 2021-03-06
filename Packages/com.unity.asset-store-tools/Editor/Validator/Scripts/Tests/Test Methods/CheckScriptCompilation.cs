using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using UnityEditor;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckScriptCompilation : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var hasCompilationErrors = EditorUtility.scriptCompilationFailed;

            if(hasCompilationErrors)
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("One or more scripts in the project failed to compile.\n" +
                    "Please check the Console window to see the list of errors");
            }
            else
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All scripts in the project compiled successfully!");
            }
            
            return result;
        }
    }
}
