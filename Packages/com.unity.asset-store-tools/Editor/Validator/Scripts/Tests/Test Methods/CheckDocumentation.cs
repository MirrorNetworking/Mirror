using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System;
using System.IO;
using System.Linq;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckDocumentation : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var textFilePaths = AssetUtility.GetAssetPathsFromAssets(config.ValidationPaths, AssetType.Documentation).ToArray();
            var documentationFilePaths = textFilePaths.Where(CouldBeDocumentation).ToArray();

            if (textFilePaths.Length == 0)
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("No potential documentation files ('.txt', '.pdf', " +
                                  "'.html', '.rtf', '.md') found within the given path.");
            }
            else if (documentationFilePaths.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Warning;
                var textFileObjects = textFilePaths.Select(AssetUtility.AssetPathToObject).ToArray();
                result.AddMessage("The following files have been found to match the documentation file format," +
                    " but may not be documentation in content",
                    null, textFileObjects);
            }
            else
            {
                result.Result = TestResult.ResultStatus.Pass;
                var documentationFileObjects = documentationFilePaths.Select(AssetUtility.AssetPathToObject).ToArray();
                result.AddMessage("Found documentation files", null, documentationFileObjects);
            }

            return result;
        }

        private bool CouldBeDocumentation(string filePath)
        {
            if (filePath.EndsWith(".pdf"))
                return true;

            using (var fs = File.Open(filePath, FileMode.Open))
            using (var bs = new BufferedStream(fs))
            using (var sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var mentionsDocumentation = line.IndexOf("documentation", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (mentionsDocumentation)
                        return true;
                }
            }

            return false;
        }
    }
}
