using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckCompressedFiles : ITestScript
    {
        private enum ArchiveResult
        {
            Allowed,
            NotAllowed,
            TarGzWithIssues,
            ZipWithIssues
        }

        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var checkedArchives = new Dictionary<UnityObject, ArchiveResult>();

            // Retrieving assets via GetObjectsFromAssets() is insufficient because
            // archives might be renamed and not use the expected extension
            var allAssetPaths = AssetUtility.GetAssetPathsFromAssets(config.ValidationPaths, AssetType.All);

            foreach (var assetPath in allAssetPaths)
            {
                FileSignatureUtility.ArchiveType archiveType;
                if ((archiveType = FileSignatureUtility.GetArchiveType(assetPath)) == FileSignatureUtility.ArchiveType.None)
                    continue;

                var archiveObj = AssetUtility.AssetPathToObject(assetPath);

                switch (archiveType)
                {
                    case FileSignatureUtility.ArchiveType.TarGz:
                        if (assetPath.ToLower().EndsWith(".unitypackage"))
                            checkedArchives.Add(archiveObj, ArchiveResult.Allowed);
                        else
                            checkedArchives.Add(archiveObj, ArchiveResult.TarGzWithIssues);
                        break;
                    case FileSignatureUtility.ArchiveType.Zip:
                        if (FileNameContainsKeyword(assetPath, "source") && assetPath.ToLower().EndsWith(".zip"))
                            checkedArchives.Add(archiveObj, ArchiveResult.Allowed);
                        else
                            checkedArchives.Add(archiveObj, ArchiveResult.ZipWithIssues);
                        break;
                    default:
                        checkedArchives.Add(archiveObj, ArchiveResult.NotAllowed);
                        break;
                }
            }

            if(checkedArchives.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No archives were found in the package content!");
                return result;
            }

            if(checkedArchives.Any(x => x.Value == ArchiveResult.Allowed))
            {
                result.Result = TestResult.ResultStatus.Warning;
                result.AddMessage("The following archives of allowed format were found in the package content.\n" +
                    "Please make sure they adhere to the nested archive guidelines:", null, 
                    checkedArchives.Where(x => x.Value == ArchiveResult.Allowed).Select(x => x.Key).ToArray());
            }

            if (checkedArchives.Any(x => x.Value == ArchiveResult.TarGzWithIssues))
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following .gz archives were found in the package content.\n" +
                    "• Gz archives are only allowed in form of '.unitypackage' files", null,
                    checkedArchives.Where(x => x.Value == ArchiveResult.TarGzWithIssues).Select(x => x.Key).ToArray());
            }

            if (checkedArchives.Any(x => x.Value == ArchiveResult.ZipWithIssues))
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following .zip archives were found in the package content.\n" +
                    "• Zip archives should contain the keyword 'source' in the file name", null,
                    checkedArchives.Where(x => x.Value == ArchiveResult.ZipWithIssues).Select(x => x.Key).ToArray());
            }

            if (checkedArchives.Any(x => x.Value == ArchiveResult.NotAllowed))
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following archives are using formats that are not allowed:", null,
                    checkedArchives.Where(x => x.Value == ArchiveResult.NotAllowed).Select(x => x.Key).ToArray());
            }

            return result;
        }

        private bool FileNameContainsKeyword(string filePath, string keyword)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
                return false;

            return fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length).ToLower().Contains(keyword.ToLower());
        }
    }
}
