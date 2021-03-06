using AssetStoreTools.Utility;
using AssetStoreTools.Validator.Categories;
using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Validator
{
    internal class PackageValidator
    {
#if UNITY_EDITOR_OSX
        private static readonly string UnityPath = Path.Combine(EditorApplication.applicationPath, "Contents", "MacOS", "Unity");
#else // Windows, Linux
        private static readonly string UnityPath = EditorApplication.applicationPath;
#endif

        private CategoryEvaluator _categoryEvaluator;


        public List<AutomatedTest> AutomatedTests { get; private set; }
        public string Category => _categoryEvaluator.GetCategory();

        private static PackageValidator s_instance;
        public static PackageValidator Instance => s_instance ?? (s_instance = new PackageValidator());

        private PackageValidator()
        {
            _categoryEvaluator = new CategoryEvaluator(ValidationState.Instance.ValidationStateData.SerializedCategory);
            CreateAutomatedTestCases();
        }

        private void CreateAutomatedTestCases()
        {
            var testData = ValidatorUtility.GetAutomatedTestCases(ValidatorUtility.SortType.Alphabetical);
            AutomatedTests = new List<AutomatedTest>();

            foreach (var t in testData)
            {
                var test = new AutomatedTest(t);

                if (!ValidationState.Instance.TestResults.ContainsKey(test.Id))
                    ValidationState.Instance.CreateTestContainer(test.Id);
                else
                    test.Result = ValidationState.Instance.TestResults[test.Id].Result;

                AutomatedTests.Add(test);
            }
        }

        public static ValidationResult ValidatePackage(ValidationSettings settings)
        {
           return Instance.RunAllTests(settings);
        }

        public ValidationResult RunAllTests(ValidationSettings settings)
        {
            if (settings == null || settings.ValidationPaths == null || settings.ValidationPaths.Count == 0)
                return new ValidationResult() { Status = ValidationStatus.Failed, Error = "No validation paths were provided" };

            var validationPaths = settings.ValidationPaths.ToArray();
            _categoryEvaluator.SetCategory(settings.Category);

            var hasCompilationErrors = EditorUtility.scriptCompilationFailed;
            ValidationState.Instance.SetCompilationState(hasCompilationErrors);

            ValidationState.Instance.SetValidationPaths(validationPaths);
            ValidationState.Instance.SetCategory(_categoryEvaluator.GetCategory());

            for (int i = 0; i < AutomatedTests.Count; i++)
            {
                var test = AutomatedTests[i];

                EditorUtility.DisplayProgressBar("Validating", $"Running validation: {i + 1} - {test.Title}", (float)i / AutomatedTests.Count);
                test.Run(new ValidationTestConfig() { ValidationPaths = validationPaths });

                // Adjust result based on categories
                var updatedStatus = _categoryEvaluator.Evaluate(test);
                test.Result.Result = updatedStatus;

                ValidationState.Instance.ChangeResult(test.Id, test.Result);
            }

            EditorUtility.UnloadUnusedAssetsImmediate();
            EditorUtility.ClearProgressBar();

            ValidationState.Instance.SaveJson();

            var projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            var result = new ValidationResult()
            {
                Status = ValidationStatus.RanToCompletion,
                ProjectPath = projectPath,
                AutomatedTests = AutomatedTests,
                HadCompilationErrors = hasCompilationErrors                
            };

            return result;
        }

        public static ValidationResult ValidatePreExportedPackage(ValidationSettings settings, bool interactiveMode)
        {
            var result = new ValidationResult();

            if (settings == null || settings.ValidationPaths == null || 
                settings.ValidationPaths.Count != 1 || string.IsNullOrEmpty(settings.ValidationPaths[0]))
            {
                result.Status = ValidationStatus.Failed;
                result.Error = "A single package path must be provided";
                return result;
            }

            try
            {
                // Step 1 - prepare a temporary project
                result = PrepareTemporaryValidationProject(settings, result, interactiveMode);

                // If preparation was cancelled or setting up project failed - return immediately
                if (result.Status == ValidationStatus.Cancelled || result.Status == ValidationStatus.Failed)
                    return result;

                // Step 2 - load the temporary project and validate the package
                result = ValidateTemporaryValidationProject(settings, result, interactiveMode);

                return result;
            }
            catch (Exception e)
            {
                result.Status = ValidationStatus.Failed;
                result.Error = e.Message;
                return result;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static ValidationResult PrepareTemporaryValidationProject(ValidationSettings settings, ValidationResult result, bool interactiveMode)
        {
            EditorUtility.DisplayProgressBar("Validating...", "Preparing the validation project. This may take a while.", 0.3f);

            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            var tempProjectPath = Path.Combine(rootProjectPath, "Temp", GUID.Generate().ToString()).Replace("\\", "/");
            result.ProjectPath = tempProjectPath;

            if (!Directory.Exists(tempProjectPath))
                Directory.CreateDirectory(tempProjectPath);

            // Cannot edit a package.json file that does not yet exist - copy over AST instead
            var tempPackagesPath = $"{tempProjectPath}/Packages";
            if (!Directory.Exists(tempPackagesPath))
                Directory.CreateDirectory(tempPackagesPath);
            var assetStoreToolsPath = PackageUtility.GetAllPackages().FirstOrDefault(x => x.name == "com.unity.asset-store-tools").resolvedPath.Replace("\\", "/");
            FileUtility.CopyDirectory(assetStoreToolsPath, $"{tempPackagesPath}/com.unity.asset-store-tools", true);

            var logFilePath = $"{tempProjectPath}/preparation.log";

            // Create the temporary project
            var processInfo = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = UnityPath,
                Arguments = $"-createProject \"{tempProjectPath}\" -logFile \"{logFilePath}\" -importpackage \"{Path.GetFullPath(settings.ValidationPaths[0])}\" -quit"
            };
            if (!interactiveMode)
                processInfo.Arguments += " -batchmode";

            var exitCode = 0;

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                while (!process.HasExited)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Validating...", "Preparing the validation project. This may take a while.", 0.3f))
                        process.Kill();

                    Thread.Sleep(10);
                }

                exitCode = process.ExitCode;

                // Windows and MacOS exit codes
                if (exitCode == -1 || exitCode == 137)
                {
                    result.Status = ValidationStatus.Cancelled;
                    return result;
                }
            }

            if (exitCode != 0)
            {
                result.Status = ValidationStatus.Failed;
                result.Error = $"Setting up the temporary project failed (exit code {exitCode})\n\nMore information can be found in the log file: {logFilePath}";
            }
            else
            {
                result.Status = ValidationStatus.RanToCompletion;
            }

            return result;
        }

        private static ValidationResult ValidateTemporaryValidationProject(ValidationSettings settings, ValidationResult result, bool interactiveMode)
        {
            EditorUtility.DisplayProgressBar("Validating...", "Performing validation...", 0.6f);

            var logFilePath = $"{result.ProjectPath}/validation.log";
            var processInfo = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = UnityPath,
                Arguments = $"-projectPath \"{result.ProjectPath}\" -logFile \"{logFilePath}\" -executeMethod AssetStoreTools.Validator.PackageValidator.ValidateProject -category \"{settings.Category}\""
            };

            if (!interactiveMode)
                processInfo.Arguments += " -batchmode -ignorecompilererrors";

            var exitCode = 0;

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            if (exitCode != 0)
            {
                result.Status = ValidationStatus.Failed;
                result.Error = $"Validating the temporary project failed (exit code {exitCode})\n\nMore information can be found in the log file: {logFilePath}";
            }
            else
            {
                result.Status = ValidationStatus.RanToCompletion;
            }

            return result;
        }
        
        // Invoked via Command Line Arguments
        private static void ValidateProject()
        {
            var exitCode = 0;
            try
            {
                // Determine whether to validate Assets folder or Packages folders
                var validationPaths = new List<string>();
                var packageDirectories = Directory.GetDirectories("Packages", "*", SearchOption.TopDirectoryOnly)
                    .Select(x => x.Replace("\\", "/"))
                    .Where(x => x != "Packages/com.unity.asset-store-tools").ToArray();

                if (packageDirectories.Length > 0)
                    validationPaths.AddRange(packageDirectories);
                else
                    validationPaths.Add("Assets");

                // Parse category
                var category = string.Empty;
                var args = Environment.GetCommandLineArgs().ToList();
                var categoryIndex = args.IndexOf("-category");
                if (categoryIndex != -1 && categoryIndex + 1 < args.Count)
                    category = args[categoryIndex + 1];

                // Run validation
                var validationSettings = new ValidationSettings()
                {
                    ValidationPaths = validationPaths,
                    Category = category
                };
                Instance.RunAllTests(validationSettings);

                AssetStoreTools.ShowAssetStoreToolsValidator();
                EditorUtility.DisplayDialog("Validation complete", "Package validation complete.\n\nTo resume work in the original project, close this Editor instance", "OK");
            }
            catch
            {
                exitCode = 1;
                throw;
            }
            finally
            {
                if (Application.isBatchMode)
                    EditorApplication.Exit(exitCode);
            }
        }
    }
}